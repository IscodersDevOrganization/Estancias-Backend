using DAL.Data;
using DAL.DTOs.Reportes;
using DAL.Models;
using EstanciasCore.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

namespace EstanciasCore.Services
{
    /// <summary>
    /// Envía UN resumen por mail y deja registrado el resultado (fila de la cola + historial de intentos).
    /// Lo usan el EnvioDeResumenWorker (envío real y modo prueba) y el panel Cola de Resúmenes (prueba manual).
    /// No decide a quién mandar: el destino lo recibe por parámetro.
    /// </summary>
    public class EnvioResumenService
    {
        private static readonly TimeSpan[] EsperaEntreReintentos =
        {
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(12)
        };

        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EnvioResumenService> _logger;

        public EnvioResumenService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<EnvioResumenService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Envía un resumen y deja registrado el resultado en la fila de la cola.
        /// </summary>
        public async Task<ResultadoEnvio> EnviarAsync(EstanciasContext context, EnvioResumenMail envio, string destino, int? maxIntentos = null)
        {
            var limiteIntentos = maxIntentos ?? _configuration.GetValue<int>("EnvioResumen:MaxIntentos", 5);

            // 1. Marcamos "Enviando" antes de mandar, para detectar cortes a mitad de camino
            envio.Estado = EstadosEnvio.Enviando;
            envio.Intentos++;
            envio.FechaUltimoIntento = DateTime.Now;
            envio.EmailDestino = destino;
            await context.SaveChangesAsync();

            var cronometro = System.Diagnostics.Stopwatch.StartNew();
            var idempotencyKey = ClaveIdempotencia(envio, destino);
            ResultadoEnvio resultado;

            try
            {
                var datos = await context.ResumenTarjeta.AsNoTracking()
                    .Where(r => r.Id == envio.ResumenTarjetaId)
                    .Select(r => new
                    {
                        r.Monto,
                        r.MontoAdeudado,
                        r.Adjunto,
                        PeriodoVencimiento = r.Periodo.FechaVencimiento,
                        PeriodoHasta = r.Periodo.FechaHasta
                    })
                    .FirstOrDefaultAsync();

                if (datos == null || datos.Adjunto == null || datos.Adjunto.Length == 0)
                {
                    envio.Estado = EstadosEnvio.Omitido;
                    envio.MotivoOmision = MotivosOmision.SinPdf;
                    envio.ProximoIntento = null;
                    await context.SaveChangesAsync();
                    return ResultadoEnvio.Falla("INTERNO", "El resumen no tiene PDF adjunto: se omite el envío.", reintentable: false, codigoError: MotivosOmision.SinPdf);
                }

                var viewEngine = _serviceProvider.GetRequiredService<ICompositeViewEngine>();
                var mailService = _serviceProvider.GetRequiredService<IMailService>();

                var modelo = new DetallesCuotasResumenDTO
                {
                    Fecha = new DateTime(datos.PeriodoVencimiento.Year, datos.PeriodoVencimiento.Month, 10).ToString("dd/MM"),
                    Monto = datos.Monto + datos.MontoAdeudado,
                };
                var mesNombre = new CultureInfo("es-AR").DateTimeFormat.GetMonthName(datos.PeriodoHasta.Month);
                var html = await RenderViewToString(viewEngine, _serviceProvider, "Home/MailResumen", modelo, mesNombre);

                var titulo = envio.EsPrueba
                    ? $"[PRUEBA - cliente {envio.EmailCliente}] Tu resumen de Tarjeta Estancias ya está disponible"
                    : "Tu resumen de Tarjeta Estancias ya está disponible";

                resultado = await mailService.EnviarConResultadoAsync(new MailAPI { Mail = destino, Titulo = titulo, Html = html }, datos.Adjunto, "Resumen.pdf", idempotencyKey);
            }
            catch (Exception ex)
            {
                // Falla antes de llegar al proveedor (render de la vista, lectura del PDF, etc.)
                resultado = ResultadoEnvio.Falla("INTERNO", ex.ToString(), reintentable: true, codigoError: "excepcion_interna");
            }
            cronometro.Stop();

            // Historial: cada intento queda guardado con su respuesta exacta
            context.EnvioResumenMailIntento.Add(new EnvioResumenMailIntento
            {
                EnvioResumenMailId = envio.Id,
                NroIntento = envio.Intentos,
                Fecha = DateTime.Now,
                DuracionMs = cronometro.ElapsedMilliseconds,
                EmailDestino = destino,
                Proveedor = resultado.Proveedor,
                Exito = resultado.Exito,
                HttpStatus = resultado.StatusCode,
                CodigoErrorProveedor = resultado.CodigoError,
                Error = resultado.Error,
                ProveedorMessageId = resultado.MessageId,
                IdempotencyKey = idempotencyKey,
                Reintentable = resultado.EsReintentable
            });

            envio.Proveedor = resultado.Proveedor;
            envio.HttpStatus = resultado.StatusCode;
            envio.ProveedorMessageId = resultado.MessageId;

            if (resultado.Exito)
            {
                envio.Estado = EstadosEnvio.Enviado;
                envio.FechaEnvio = DateTime.Now;
                envio.Error = null;
                envio.CodigoErrorProveedor = null;
                envio.ProximoIntento = null;
            }
            else
            {
                RegistrarFallo(envio, resultado.Error, resultado.CodigoError, resultado.EsReintentable, limiteIntentos);
            }

            await context.SaveChangesAsync();
            return resultado;
        }

        /// <summary>
        /// Clave de idempotencia para Resend: mismo resumen + mismo destino = misma clave.
        /// Si un reintento repite un envío que en realidad ya había salido (por ejemplo, la app se cortó
        /// justo después de mandarlo), Resend no lo manda de nuevo durante 24 h.
        /// </summary>
        public static string ClaveIdempotencia(EnvioResumenMail envio, string destino)
        {
            string hashDestino;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes((destino ?? "").Trim().ToLowerInvariant()));
                hashDestino = BitConverter.ToString(bytes, 0, 8).Replace("-", "").ToLowerInvariant();
            }

            return envio.EsPrueba
                ? $"estancias-prueba-{envio.Id}-{hashDestino}"
                : $"estancias-resumen-{envio.ResumenTarjetaId}-{hashDestino}";
        }

        private void RegistrarFallo(EnvioResumenMail envio, string error, string codigoError, bool reintentable, int maxIntentos)
        {
            envio.Error = error;
            envio.CodigoErrorProveedor = codigoError;

            if (reintentable && envio.Intentos < maxIntentos)
            {
                var espera = EsperaEntreReintentos[Math.Min(envio.Intentos - 1, EsperaEntreReintentos.Length - 1)];
                envio.Estado = EstadosEnvio.Fallido;
                envio.ProximoIntento = DateTime.Now.Add(espera);
            }
            else
            {
                envio.Estado = EstadosEnvio.FallidoDefinitivo;
                envio.ProximoIntento = null;
            }

            _logger.LogWarning("Falló el envío del resumen {ResumenId} a {Email} (intento {Intento}, estado {Estado}): {Error}",
                envio.ResumenTarjetaId, envio.EmailDestino, envio.Intentos, envio.Estado, error);
        }

        private async Task<string> RenderViewToString(ICompositeViewEngine viewEngine, IServiceProvider serviceProvider, string viewName, DetallesCuotasResumenDTO model, string mesNombre)
        {
            var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            using (var sw = new StringWriter())
            {
                var viewResult = viewEngine.FindView(actionContext, viewName, false);
                if (viewResult.View == null)
                {
                    throw new InvalidOperationException($"No se pudo encontrar la vista '{viewName}'");
                }

                var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(actionContext.HttpContext, serviceProvider.GetRequiredService<ITempDataProvider>()),
                    sw,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                var culturaAR = new CultureInfo("es-AR");

                return sw.ToString()
                    .Replace("TextoFechaReemplazar", model.Fecha)
                    .Replace("TextoMontoReemplazar", model.Monto.ToString("N2", culturaAR))
                    .Replace("TextoMesEscritoReemplazar", mesNombre);
            }
        }
    }
}
