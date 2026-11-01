using DAL.Data;
using DAL.DTOs.Reportes;
using DAL.DTOs.Servicios;
using DAL.Mobile;
using DAL.Models;
using DAL.Models.Core;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using iText.Html2pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ResumenTarjetaService : IResumenTarjetaService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResumenTarjetaService> _logger;
    private readonly ColaEnvioResumenSignal _colaSignal;

    private enum ResultadoUsuario { Generado, Fallido }

    private class UsuarioPendiente
    {
        public UsuarioParaProcesarDTO Datos { get; set; }
        public bool RecibirResumen { get; set; }
    }

    private class ResultadoProceso
    {
        public ResultadoUsuario Resultado { get; set; }
        public bool Encolado { get; set; }
    }

    public ResumenTarjetaService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ResumenTarjetaService> logger, ColaEnvioResumenSignal colaSignal)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _colaSignal = colaSignal;
    }

    public Task<ResultadoGeneracionResumen> GenerarResumenTarjetas(CancellationToken cancellationToken = default(CancellationToken))
    {
        // Generación mensual: período del mes (se crea si no existe), todos los usuarios sin resumen, "hoy" = ahora
        return EjecutarGeneracionAsync(CodigosProceso.GenerarResumen, null, soloFallidos: false, cancellationToken);
    }

    public Task<ResultadoGeneracionResumen> RegenerarFallidosAsync(int periodoId, CancellationToken cancellationToken = default(CancellationToken))
    {
        // Regeneración manual: solo los usuarios con la generación en estado Fallido de ese período
        return EjecutarGeneracionAsync(CodigosProceso.RegenerarFallidos, periodoId, soloFallidos: true, cancellationToken);
    }

    private async Task<ResultadoGeneracionResumen> EjecutarGeneracionAsync(string codigoProceso, int? periodoIdFijo, bool soloFallidos, CancellationToken cancellationToken)
    {
        var resultado = new ResultadoGeneracionResumen();
        int? ejecucionId = null;

        try
        {
            var tamanoLote = _configuration.GetValue<int>("ProcesoResumen:TamanoLote", 100);
            var limiteConcurrencia = _configuration.GetValue<int>("ProcesoResumen:LimiteConcurrencia", 10);

            DatosEstructura datosEstructura;
            Periodo periodo;

            // --- 1. PERÍODO ACTUAL (vence el 15 de este mes) Y ALTA DE LA EJECUCIÓN ---
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();

                datosEstructura = await context.DatosEstructura.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
                periodo = periodoIdFijo.HasValue
                    ? await context.Periodo.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodoIdFijo.Value, cancellationToken)
                    : await ObtenerOCrearPeriodoAsync(context, cancellationToken);

                if (periodo == null)
                    throw new InvalidOperationException($"No existe el período {periodoIdFijo}.");

                var ejecucion = new EjecucionProceso
                {
                    Codigo = codigoProceso,
                    PeriodoId = periodo.Id,
                    FechaInicio = DateTime.Now,
                    Estado = EstadosEjecucion.EnCurso,
                    Servidor = Environment.MachineName
                };
                context.EjecucionProceso.Add(ejecucion);
                await context.SaveChangesAsync(cancellationToken);
                ejecucionId = ejecucion.Id;
            }

            resultado.EjecucionId = ejecucionId;
            resultado.PeriodoId = periodo.Id;

            // --- 2. USUARIOS PENDIENTES: los que todavía no tienen resumen en este período ---
            List<UsuarioPendiente> pendientes;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();
                var periodoId = periodo.Id;
                var usuariosConResumen = context.ResumenTarjeta.Where(r => r.PeriodoId == periodoId).Select(r => r.UsuarioId);

                var query = context.Usuarios
                    .Include(u => u.Personas)
                    .Where(u => u.Personas != null &&
                                !string.IsNullOrEmpty(u.Personas.NroTarjeta) &&
                                !string.IsNullOrEmpty(u.Personas.NroDocumento) &&
                                !usuariosConResumen.Contains(u.Id));

                if (soloFallidos)
                {
                    var usuariosFallidos = context.ResumenGeneracion
                        .Where(g => g.PeriodoId == periodoId && g.Estado == EstadosGeneracion.Fallido)
                        .Select(g => g.UsuarioId);
                    query = query.Where(u => usuariosFallidos.Contains(u.Id));
                }

                pendientes = await query
                    .OrderBy(u => u.Id)
                    .Select(u => new UsuarioPendiente
                    {
                        Datos = new UsuarioParaProcesarDTO
                        {
                            Id = u.Id,
                            NombreCompleto = u.Personas.GetNombreCompleto(),
                            NroDocumento = u.Personas.NroDocumento,
                            NroTarjeta = u.Personas.NroTarjeta,
                            UserName = u.UserName
                        },
                        RecibirResumen = u.RecibirResumen
                    })
                    .ToListAsync(cancellationToken);
            }

            var ahora = DateTime.Now;
            var fechaGeneracion = (ahora.Year == periodo.FechaVencimiento.Year && ahora.Month == periodo.FechaVencimiento.Month)
                ? ahora
                : new DateTime(periodo.FechaVencimiento.Year, periodo.FechaVencimiento.Month, 1);

            resultado.Total = pendientes.Count;
            _logger.LogInformation("{Proceso} período {PeriodoId}: {Cantidad} usuarios pendientes.", codigoProceso, periodo.Id, pendientes.Count);

            if (datosEstructura == null)
            {
                throw new InvalidOperationException("No se encontraron los datos de estructura (credenciales del WS) en la tabla DatosEstructura.");
            }

            // --- 3. PROCESAMIENTO EN LOTES PARALELOS. CADA USUARIO SE GUARDA APENAS TERMINA ---
            using (var semaphore = new SemaphoreSlim(limiteConcurrencia))
            {
                foreach (var lote in pendientes.Select((u, i) => new { u, i }).GroupBy(x => x.i / tamanoLote, x => x.u))
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var tareas = lote.Select(u => ProcesarUsuarioAsync(u, semaphore, datosEstructura, periodo, fechaGeneracion, ejecucionId.Value, cancellationToken)).ToList();
                    var resultadosLote = await Task.WhenAll(tareas);

                    resultado.Generados += resultadosLote.Count(r => r.Resultado == ResultadoUsuario.Generado);
                    resultado.Fallidos += resultadosLote.Count(r => r.Resultado == ResultadoUsuario.Fallido);
                    resultado.Encolados += resultadosLote.Count(r => r.Resultado == ResultadoUsuario.Generado && r.Encolado);
                    resultado.OmitidosEnvio += resultadosLote.Count(r => r.Resultado == ResultadoUsuario.Generado && !r.Encolado);

                    await ActualizarEjecucionAsync(ejecucionId.Value, resultado, EstadosEjecucion.EnCurso, null);
                }
            }

            if (cancellationToken.IsCancellationRequested)
                resultado.Estado = EstadosEjecucion.Cancelado;
            else
                resultado.Estado = resultado.Fallidos == 0 ? EstadosEjecucion.Completado : EstadosEjecucion.CompletadoConErrores;

            await ActualizarEjecucionAsync(ejecucionId.Value, resultado, resultado.Estado, null, finalizar: true);

            _logger.LogInformation("Generación de resúmenes período {PeriodoId} terminó en {Estado}. Generados: {Generados}, Fallidos: {Fallidos}, Encolados: {Encolados}.",
                periodo.Id, resultado.Estado, resultado.Generados, resultado.Fallidos, resultado.Encolados);

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error general en la generación de resúmenes.");
            resultado.Estado = ex is OperationCanceledException ? EstadosEjecucion.Cancelado : EstadosEjecucion.Fallido;
            resultado.Error = ex.Message;

            if (ejecucionId.HasValue)
            {
                try { await ActualizarEjecucionAsync(ejecucionId.Value, resultado, resultado.Estado, ex.ToString(), finalizar: true); }
                catch (Exception exLog) { _logger.LogError(exLog, "No se pudo registrar el error de la ejecución {EjecucionId}.", ejecucionId); }
            }

            return resultado;
        }
    }

    private static async Task<Periodo> ObtenerOCrearPeriodoAsync(EstanciasContext context, CancellationToken cancellationToken)
    {
        var calculado = CalcularPeriodo(new DateTime(DateTime.Now.Year, DateTime.Now.Month, 15));

        var periodo = await context.Periodo.FirstOrDefaultAsync(p => p.FechaVencimiento.Date == calculado.FechaVencimiento, cancellationToken);
        if (periodo != null) return periodo;

        context.Add(calculado);
        await context.SaveChangesAsync(cancellationToken);
        return calculado;
    }

    /// <summary>
    /// Arma (sin guardar) el período que vence en fechaVencimiento: del 26 de hace dos meses al 25 del mes anterior.
    /// Es la misma regla que usa la generación mensual al crear el período.
    /// </summary>
    public static Periodo CalcularPeriodo(DateTime fechaVencimiento)
    {
        var vencimiento = new DateTime(fechaVencimiento.Year, fechaVencimiento.Month, 15);
        var mesDesde = vencimiento.AddMonths(-2);
        var mesHasta = vencimiento.AddMonths(-1);
        var fechaHasta = new DateTime(mesHasta.Year, mesHasta.Month, 25);

        return new Periodo
        {
            Descripcion = fechaHasta.ToString("MMMM yyyy", new CultureInfo("es-ES")),
            FechaDesde = new DateTime(mesDesde.Year, mesDesde.Month, 26),
            FechaHasta = fechaHasta,
            Activo = true,
            FechaVencimiento = vencimiento
        };
    }

    private class ResumenConstruido
    {
        public bool Ok { get; set; }
        public string CodigoError { get; set; }
        public string Mensaje { get; set; }
        public TempalteResumenDTO Datos { get; set; }
        public byte[] Pdf { get; set; }
        public string NroComprobante { get; set; }
        public DateTime FechaResumen { get; set; }
        public string ResultadoWs { get; set; }
    }

    /// <summary>
    /// Arma el resumen de UN usuario (WS, cuotas, punitorios y PDF) SIN guardar nada.
    /// Lo usan la generación mensual (con fechaGeneracion = ahora, igual que siempre) y la simulación.
    /// fechaGeneracion define hasta qué vencimientos entran (fin de ese mes). Los importes y punitorios los calcula LOAN al momento de la consulta.
    /// </summary>
    private static async Task<ResumenConstruido> ConstruirResumenAsync(IServiceProvider serviceProvider, UsuarioParaProcesarDTO usuario, DatosEstructura datosEstructura, Periodo periodo, DateTime fechaGeneracion)
    {
        var documentoLimpio = DatosTarjetaService.NormalizarDocumento(usuario.NroDocumento);
        if (documentoLimpio == null)
        {
            return new ResumenConstruido
            {
                CodigoError = CodigosErrorGeneracion.DocumentoInvalido,
                Mensaje = $"El DNI cargado '{usuario.NroDocumento}' no es válido: {DatosTarjetaService.DescribirCaracteresInvalidos(usuario.NroDocumento)}. Corregilo en los datos del cliente."
            };
        }

        var numeroTarjetaLimpio = (usuario.NroTarjeta ?? "").Trim();
        if (!long.TryParse(numeroTarjetaLimpio, out long numeroTarjetaConvertido))
        {
            return new ResumenConstruido { CodigoError = CodigosErrorGeneracion.TarjetaInvalida, Mensaje = $"El Nro. de Tarjeta '{numeroTarjetaLimpio}' no tiene un formato numérico válido." };
        }

        if (datosEstructura == null)
        {
            return new ResumenConstruido { CodigoError = CodigosErrorGeneracion.SinConfiguracion, Mensaje = "No se encontraron los datos de estructura (credenciales del WS) en la tabla DatosEstructura." };
        }

        var fechaMesActualCuotas = new DateTime(fechaGeneracion.Year, fechaGeneracion.Month, 1);

        var datosServices = serviceProvider.GetRequiredService<IDatosTarjetaService>();

        // --- LÓGICA DE NEGOCIO ORIGINAL ---
        var datosMovimientos = await datosServices.ConsultarMovimientos(datosEstructura.UsernameWS, datosEstructura.PasswordWS, documentoLimpio, numeroTarjetaConvertido, 100, 1);

        if (datosMovimientos?.Detalle == null || datosMovimientos.Detalle.Resultado != "EXITO")
        {
            return new ResumenConstruido
            {
                CodigoError = CodigosErrorGeneracion.WsSinExito,
                ResultadoWs = datosMovimientos?.Detalle?.Resultado,
                Mensaje = $"El WS respondió '{datosMovimientos?.Detalle?.Resultado ?? "(sin respuesta)"}': {datosMovimientos?.Detalle?.Mensaje}" +
                          $" | Datos enviados al WS: documento '{documentoLimpio}', tarjeta '{numeroTarjetaConvertido}'"
            };
        }

        CultureInfo.CurrentCulture = new CultureInfo("es-AR");

        // --- SALDO A PAGAR: misma fuente y mismo criterio que /Core/MovimientoTarjeta ---
        // Cuotas pendientes (cobranzas) de la API de consulta de LOAN, con el importe que calcula LOAN
        // (capital + punitorios + cargos), que vencen hasta el último día del mes de generación.
        // Ya no se usa la fórmula propia de punitorios (TNA 98% fija con corte al día 15).
        DAL.DTOs.ApiCpeCreditos.ResponseObtenerConsultaDTO consulta;
        try
        {
            consulta = await datosServices.ObtenerConsultaCobranzasAsync(documentoLimpio);
        }
        catch (Exception ex)
        {
            return new ResumenConstruido
            {
                CodigoError = CodigosErrorGeneracion.ConsultaLoan,
                Mensaje = "No se pudo consultar la deuda en la API de consulta de LOAN: " + (ex.InnerException?.Message ?? ex.Message),
                ResultadoWs = "EXITO"
            };
        }

        if (consulta == null)
        {
            return new ResumenConstruido { CodigoError = CodigosErrorGeneracion.ConsultaLoan, Mensaje = "La API de consulta de LOAN no devolvió datos.", ResultadoWs = "EXITO" };
        }

        var culturaAR = new CultureInfo("es-AR");
        var cobranzas = DatosTarjetaService.CobranzasDelSaldo(consulta, fechaGeneracion);
        var solicitudesWs = datosMovimientos.DetallesSolicitud ?? new List<SolicitudDetail>();

        var cuotasResumen = cobranzas
            .OrderBy(c => c.numero)
            .ThenBy(c => c.fechaVencimiento)
            .Select(c =>
            {
                // Del SOAP solo se toman el nombre del comercio y la cantidad total de cuotas de la solicitud
                var solicitud = solicitudesWs.FirstOrDefault(s => s.NumeroSolicitud == c.numero.ToString());
                int totalCuotas = (solicitud?.DetallesCuota != null && solicitud.DetallesCuota.Any())
                    ? solicitud.DetallesCuota.Max(d => int.TryParse(d.NumeroCuota, out var n) ? n : 0)
                    : cobranzas.Where(x => x.numero == c.numero).Max(x => x.numeroCuota);

                return new ResultadoCuotasDTO
                {
                    Codigo = c.numero.ToString(),
                    Descripcion = !string.IsNullOrWhiteSpace(solicitud?.NombreComercio) ? solicitud.NombreComercio : (c.detalle ?? ""),
                    Fecha = c.fechaVencimiento.ToString("dd/MM/yyyy"),
                    NumeroCuota = c.numeroCuota.ToString(),
                    NumeroCuotaTotal = Math.Max(totalCuotas, c.numeroCuota).ToString(),
                    Monto = c.importe.ToString(culturaAR)
                };
            })
            .ToList();

        var datosParaResumenDTO = (TempalteResumenDTO)await datosServices.PrepararDatosResumen(datosMovimientos, cuotasResumen, periodo, usuario);
        if (datosParaResumenDTO == null)
        {
            return new ResumenConstruido { CodigoError = CodigosErrorGeneracion.DatosNulos, Mensaje = "PrepararDatosResumen no devolvió datos para el usuario.", ResultadoWs = "EXITO" };
        }

        var html = await datosServices.RenderViewToStringAsync("ResumenBancarioTemplate", datosParaResumenDTO);

        byte[] pdfBytes;
        using (var memoryStream = new MemoryStream())
        {
            HtmlConverter.ConvertToPdf(html, memoryStream);
            pdfBytes = memoryStream.ToArray();
        }

        return new ResumenConstruido
        {
            Ok = true,
            ResultadoWs = "EXITO",
            Datos = datosParaResumenDTO,
            Pdf = pdfBytes,
            FechaResumen = fechaMesActualCuotas,
            NroComprobante = common.Encrypt(datosParaResumenDTO.NroDocumento, fechaMesActualCuotas.ToString("ddMMyyyy"))
        };
    }

    private async Task<ResultadoProceso> ProcesarUsuarioAsync(UsuarioPendiente pendiente, SemaphoreSlim semaphore, DatosEstructura datosEstructura, Periodo periodo, DateTime fechaGeneracion, int ejecucionId, CancellationToken cancellationToken)
    {
        var usuario = pendiente.Datos;
        await semaphore.WaitAsync();
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();

                var construido = await ConstruirResumenAsync(scope.ServiceProvider, usuario, datosEstructura, periodo, fechaGeneracion);
                if (!construido.Ok)
                {
                    await RegistrarFalloAsync(usuario.Id, periodo.Id, ejecucionId, construido.CodigoError, construido.Mensaje, null);
                    return new ResultadoProceso { Resultado = ResultadoUsuario.Fallido };
                }

                var resumen = new ResumenTarjeta
                {
                    Fecha = construido.FechaResumen,
                    FechaVencimiento = periodo.FechaVencimiento,
                    Monto = construido.Datos.SaldoActual,
                    MontoAdeudado = construido.Datos.SaldoPendiente,
                    NroComprobante = construido.NroComprobante,
                    Adjunto = construido.Pdf,
                    PeriodoId = periodo.Id,
                    UsuarioId = usuario.Id
                };

                // --- GUARDADO ATÓMICO: resumen + estado de generación + fila en la cola de envío ---
                var ahora = DateTime.Now;
                context.ResumenTarjeta.Add(resumen);

                var generacion = await context.ResumenGeneracion
                    .FirstOrDefaultAsync(g => g.PeriodoId == periodo.Id && g.UsuarioId == usuario.Id, cancellationToken);
                if (generacion == null)
                {
                    generacion = new ResumenGeneracion { PeriodoId = periodo.Id, UsuarioId = usuario.Id, FechaCreacion = ahora };
                    context.ResumenGeneracion.Add(generacion);
                }
                generacion.EjecucionProcesoId = ejecucionId;
                generacion.Estado = EstadosGeneracion.Generado;
                generacion.CodigoError = null;
                generacion.Mensaje = null;
                generacion.Detalle = null;
                generacion.Intentos++;
                generacion.FechaUltimoIntento = ahora;
                generacion.ResumenTarjeta = resumen;

                var envio = CrearFilaCola(resumen, periodo.Id, usuario, pendiente.RecibirResumen, ahora);
                context.EnvioResumenMail.Add(envio);

                await context.SaveChangesAsync(cancellationToken);

                bool encolado = envio.Estado == EstadosEnvio.Pendiente;
                if (encolado) _colaSignal.Notificar();

                return new ResultadoProceso { Resultado = ResultadoUsuario.Generado, Encolado = encolado };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló la generación del resumen del usuario {UsuarioId}.", usuario.Id);
            await RegistrarFalloAsync(usuario.Id, periodo.Id, ejecucionId, CodigosErrorGeneracion.Excepcion, ex.Message, ex.ToString());
            return new ResultadoProceso { Resultado = ResultadoUsuario.Fallido };
        }
        finally
        {
            semaphore.Release();
        }
    }

    // ------------------------------------------------------------------
    // SIMULACIÓN (no guarda nada)
    // ------------------------------------------------------------------

    public async Task<ResultadoSimulacionResumen> SimularResumenAsync(UsuarioParaProcesarDTO usuario, Periodo periodo, DateTime fechaGeneracion, string leyenda)
    {
        var resultado = new ResultadoSimulacionResumen
        {
            Nombre = usuario.NombreCompleto,
            NroDocumento = usuario.NroDocumento,
            NroTarjeta = usuario.NroTarjeta,
            Mail = usuario.UserName,
            Periodo = periodo.Descripcion,
            PeriodoDesde = periodo.FechaDesde,
            PeriodoHasta = periodo.FechaHasta,
            Vencimiento = periodo.FechaVencimiento,
            FechaGeneracion = fechaGeneracion
        };

        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();
                var datosEstructura = await context.DatosEstructura.AsNoTracking().FirstOrDefaultAsync();

                var construido = await ConstruirResumenAsync(scope.ServiceProvider, usuario, datosEstructura, periodo, fechaGeneracion);
                resultado.ResultadoWs = construido.ResultadoWs;

                if (!construido.Ok)
                {
                    resultado.CodigoError = construido.CodigoError;
                    resultado.Mensaje = construido.Mensaje;
                    return resultado;
                }

                var d = construido.Datos;
                resultado.Nombre = string.IsNullOrWhiteSpace(d.Nombre) ? resultado.Nombre : d.Nombre;
                resultado.SaldoPendiente = d.SaldoPendiente;
                resultado.SaldoActual = d.SaldoActual;
                resultado.SaldoTotal = d.SaldoTotal;
                resultado.CantidadConsumosAnteriores = d.ConsumosAnteriores?.Count ?? 0;
                resultado.CantidadConsumosDelMes = d.ConsumosDelMes?.Count ?? 0;
                resultado.Pdf = AgregarMarcaDeAgua(construido.Pdf, "SIMULACIÓN", leyenda);
                resultado.Ok = true;
                return resultado;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló la simulación del resumen de {Documento}.", usuario.NroDocumento);
            resultado.CodigoError = CodigosErrorGeneracion.Excepcion;
            resultado.Mensaje = ex.Message;
            resultado.Detalle = ex.ToString();
            return resultado;
        }
    }

    /// <summary>
    /// Estampa en cada página una marca de agua diagonal semitransparente y una leyenda arriba.
    /// </summary>
    private static byte[] AgregarMarcaDeAgua(byte[] pdf, string texto, string leyenda)
    {
        using (var entrada = new MemoryStream(pdf))
        using (var salida = new MemoryStream())
        {
            using (var documento = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(entrada), new iText.Kernel.Pdf.PdfWriter(salida)))
            {
                var fuente = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
                var fuenteLeyenda = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
                var rojo = new iText.Kernel.Colors.DeviceRgb(220, 38, 38);

                for (int i = 1; i <= documento.GetNumberOfPages(); i++)
                {
                    var pagina = documento.GetPage(i);
                    var tamano = pagina.GetPageSize();
                    var pdfCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pagina.NewContentStreamAfter(), pagina.GetResources(), documento);

                    // Marca diagonal semitransparente
                    pdfCanvas.SaveState();
                    pdfCanvas.SetExtGState(new iText.Kernel.Pdf.Extgstate.PdfExtGState().SetFillOpacity(0.15f));
                    new iText.Layout.Canvas(pdfCanvas, documento, tamano)
                        .SetFont(fuente)
                        .SetFontSize(tamano.GetWidth() / 6.5f)
                        .SetFontColor(rojo)
                        .ShowTextAligned(texto, tamano.GetWidth() / 2, tamano.GetHeight() / 2,
                            iText.Layout.Properties.TextAlignment.CENTER, iText.Layout.Properties.VerticalAlignment.MIDDLE, (float)(Math.PI / 4));
                    pdfCanvas.RestoreState();

                    // Leyenda arriba, legible
                    if (!string.IsNullOrEmpty(leyenda))
                    {
                        pdfCanvas.SaveState();
                        pdfCanvas.SetExtGState(new iText.Kernel.Pdf.Extgstate.PdfExtGState().SetFillOpacity(0.85f));
                        new iText.Layout.Canvas(pdfCanvas, documento, tamano)
                            .SetFont(fuenteLeyenda)
                            .SetFontSize(8)
                            .SetFontColor(rojo)
                            .ShowTextAligned(leyenda, tamano.GetWidth() / 2, tamano.GetHeight() - 12,
                                iText.Layout.Properties.TextAlignment.CENTER, iText.Layout.Properties.VerticalAlignment.TOP, 0);
                        pdfCanvas.RestoreState();
                    }
                }
            }
            return salida.ToArray();
        }
    }

    /// <summary>
    /// Arma la fila de la cola de envío. Si el resumen no se debe enviar,
    /// queda registrada como Omitido con el motivo (así se sabe por qué no le llegó).
    /// </summary>
    private static EnvioResumenMail CrearFilaCola(ResumenTarjeta resumen, int periodoId, UsuarioParaProcesarDTO usuario, bool recibirResumen, DateTime ahora)
    {
        var email = (usuario.UserName ?? "").Trim();

        string motivo = null;
        if (!recibirResumen) motivo = MotivosOmision.NoRecibeResumen;
        else if (resumen.Monto + resumen.MontoAdeudado <= 0) motivo = MotivosOmision.SinSaldo;
        else if (string.IsNullOrEmpty(email) || !email.Contains("@")) motivo = MotivosOmision.SinEmail;
        else if (resumen.Adjunto == null || resumen.Adjunto.Length == 0) motivo = MotivosOmision.SinPdf;

        return new EnvioResumenMail
        {
            ResumenTarjeta = resumen,
            PeriodoId = periodoId,
            UsuarioId = usuario.Id,
            EmailCliente = email,
            EmailDestino = email,
            EsPrueba = false,
            Estado = motivo == null ? EstadosEnvio.Pendiente : EstadosEnvio.Omitido,
            MotivoOmision = motivo,
            Intentos = 0,
            ProximoIntento = motivo == null ? ahora : (DateTime?)null,
            FechaCreacion = ahora
        };
    }

    private async Task RegistrarFalloAsync(string usuarioId, int periodoId, int ejecucionId, string codigoError, string mensaje, string detalle)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();
                var ahora = DateTime.Now;

                var generacion = await context.ResumenGeneracion
                    .FirstOrDefaultAsync(g => g.PeriodoId == periodoId && g.UsuarioId == usuarioId);
                if (generacion == null)
                {
                    generacion = new ResumenGeneracion { PeriodoId = periodoId, UsuarioId = usuarioId, FechaCreacion = ahora };
                    context.ResumenGeneracion.Add(generacion);
                }

                generacion.EjecucionProcesoId = ejecucionId;
                generacion.Estado = EstadosGeneracion.Fallido;
                generacion.CodigoError = codigoError;
                generacion.Mensaje = mensaje;
                generacion.Detalle = detalle;
                generacion.Intentos++;
                generacion.FechaUltimoIntento = ahora;

                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Si ni siquiera se puede registrar el fallo, al menos queda en el log
            _logger.LogError(ex, "No se pudo registrar el fallo del usuario {UsuarioId} ({CodigoError}: {Mensaje}).", usuarioId, codigoError, mensaje);
        }
    }

    private async Task ActualizarEjecucionAsync(int ejecucionId, ResultadoGeneracionResumen resultado, string estado, string errorGeneral, bool finalizar = false)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();
            var ejecucion = await context.EjecucionProceso.FirstOrDefaultAsync(e => e.Id == ejecucionId);
            if (ejecucion == null) return;

            ejecucion.Estado = estado;
            ejecucion.Total = resultado.Total;
            ejecucion.Exitosos = resultado.Generados;
            ejecucion.Fallidos = resultado.Fallidos;
            ejecucion.Omitidos = resultado.OmitidosEnvio;
            if (errorGeneral != null) ejecucion.ErrorGeneral = errorGeneral;
            if (finalizar) ejecucion.FechaFin = DateTime.Now;

            await context.SaveChangesAsync();
        }
    }
}
