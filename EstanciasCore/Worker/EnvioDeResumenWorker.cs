using DAL.Data;
using DAL.DTOs.Reportes;
using DAL.Models;
using EstanciasCore.Interface;
using EstanciasCore.Services;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

/// <summary>
/// Despacha la cola de envío de resúmenes (tabla EnvioResumenMail).
///
/// SEGURIDAD — el envío a clientes se controla con "EnvioResumen:Modo" en appsettings:
///   - "Deshabilitado" (valor por defecto si falta o es inválido): NO envía nada. Los resúmenes quedan Pendientes en la cola.
///   - "Prueba": envía solo a las casillas de "EnvioResumen:EmailsPrueba" una muestra de "EnvioResumen:CantidadPrueba"
///               resúmenes por período. Las filas reales de los clientes NO se tocan (siguen Pendientes).
///   - "Produccion": envía a los clientes.
/// Además, en Produccion, si Procedimientos (Codigo = "EnvioResumen") está inactivo, el envío queda en pausa.
/// El modo se relee en cada ciclo, así que se puede cambiar sin reiniciar la app.
/// </summary>
public class EnvioDeResumenWorker : BackgroundService
{
    private const string ModoDeshabilitado = "Deshabilitado";
    private const string ModoPrueba = "Prueba";
    private const string ModoProduccion = "Produccion";

    private readonly ILogger<EnvioDeResumenWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ColaEnvioResumenSignal _colaSignal;

    private DateTime _ultimoAvisoModo = DateTime.MinValue;

    public EnvioDeResumenWorker(ILogger<EnvioDeResumenWorker> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration, ColaEnvioResumenSignal colaSignal)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _colaSignal = colaSignal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de Envío de Resúmenes iniciado. Modo: {Modo}.", LeerModo());

        while (!stoppingToken.IsCancellationRequested)
        {
            bool hayMasPendientes = false;
            try
            {
                hayMasPendientes = await CicloAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo del worker de envío de resúmenes.");
            }

            // Si el lote vino lleno seguimos de inmediato; si no, esperamos el intervalo
            // o el aviso de que se generó un resumen nuevo.
            if (!hayMasPendientes)
            {
                var intervalo = TimeSpan.FromMinutes(_configuration.GetValue<int>("EnvioResumen:IntervaloMinutos", 2));
                await _colaSignal.EsperarAsync(intervalo, stoppingToken);
            }
        }
    }

    private string LeerModo()
    {
        var modo = (_configuration["EnvioResumen:Modo"] ?? "").Trim();
        if (modo.Equals(ModoProduccion, StringComparison.OrdinalIgnoreCase)) return ModoProduccion;
        if (modo.Equals(ModoPrueba, StringComparison.OrdinalIgnoreCase)) return ModoPrueba;
        return ModoDeshabilitado; // Cualquier otro valor (o ausente) = no se envía nada
    }

    /// <returns>true si quedaron más pendientes listos para enviar (lote lleno).</returns>
    private async Task<bool> CicloAsync(CancellationToken stoppingToken)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();
            var modo = LeerModo();

            await RecuperarEnviosInterrumpidosAsync(context);

            if (modo == ModoDeshabilitado)
            {
                await AvisarModoAsync(context, "Envío de resúmenes DESHABILITADO (EnvioResumen:Modo). No se envía nada; {Pendientes} resúmenes esperan en la cola.");
                return false;
            }

            if (modo == ModoPrueba)
            {
                await EnviarPruebaAsync(scope, context, stoppingToken);
                return false;
            }

            // --- MODO PRODUCCION ---
            var procedimiento = await context.Procedimientos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Codigo == CodigosProceso.EnvioResumen, stoppingToken);

            if (procedimiento == null || !procedimiento.Activo)
            {
                await AvisarModoAsync(context, "Envío de resúmenes EN PAUSA (Procedimientos 'EnvioResumen' inactivo o inexistente). {Pendientes} resúmenes esperan en la cola.");
                return false;
            }

            await OmitirPeriodosVencidosAsync(context);
            bool hayMas = await DespacharLoteAsync(scope, context, stoppingToken);
            await CerrarPeriodosTerminadosAsync(scope, context);
            return hayMas;
        }
    }

    // ------------------------------------------------------------------
    // PRODUCCIÓN
    // ------------------------------------------------------------------

    private async Task<bool> DespacharLoteAsync(IServiceScope scope, EstanciasContext context, CancellationToken stoppingToken)
    {
        var tamanoLote = _configuration.GetValue<int>("EnvioResumen:TamanoLote", 50);
        var pausaMs = _configuration.GetValue<int>("EnvioResumen:PausaEntreEnviosMs", 600);
        var ahora = DateTime.Now;

        var ids = await context.EnvioResumenMail.AsNoTracking()
            .Where(e => !e.EsPrueba
                     && (e.Estado == EstadosEnvio.Pendiente || e.Estado == EstadosEnvio.Fallido)
                     && (e.ProximoIntento == null || e.ProximoIntento <= ahora))
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .Take(tamanoLote)
            .ToListAsync(stoppingToken);

        if (ids.Count == 0) return false;

        await AbrirEjecucionesEnvioAsync(context, ids);

        foreach (var id in ids)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var envio = await context.EnvioResumenMail.FirstOrDefaultAsync(e => e.Id == id);
            if (envio == null) continue;

            await scope.ServiceProvider.GetRequiredService<EnvioResumenService>().EnviarAsync(context, envio, envio.EmailCliente);

            // Errores de la cuenta (cuota agotada, API key, dominio no verificado): afectan a todos los mails.
            // Cortamos el lote para no gastar los intentos de todos los clientes; se retoma en el próximo ciclo.
            if (EsErrorDeCuenta(envio))
            {
                _logger.LogError("Envío de resúmenes detenido por un error de la cuenta de mail ({Codigo}, HTTP {Status}). Se retoma en el próximo ciclo. Detalle: {Error}",
                    envio.CodigoErrorProveedor, envio.HttpStatus, envio.Error);
                return false;
            }

            // Si Resend pidió bajar la velocidad, esperamos más antes del siguiente
            var pausa = envio.CodigoErrorProveedor == "rate_limit_exceeded" ? Math.Max(pausaMs, 2000) : pausaMs;
            await Task.Delay(pausa, stoppingToken); // Respeta el límite de envíos por segundo del proveedor
        }

        return ids.Count == tamanoLote;
    }

    private static bool EsErrorDeCuenta(EnvioResumenMail envio)
    {
        if (envio.Estado == EstadosEnvio.Enviado) return false;
        var codigo = envio.CodigoErrorProveedor;
        return codigo == "daily_quota_exceeded"
            || codigo == "monthly_quota_exceeded"
            || codigo == "sin_configuracion"
            || envio.HttpStatus == 401
            || envio.HttpStatus == 403;
    }


    /// <summary>
    /// Si la app se cortó mientras un mail estaba "Enviando", no sabemos si salió.
    /// Lo pasamos a Fallido para que se reintente. El reintento usa la misma clave de idempotencia,
    /// así que si el mail ya había salido Resend no lo vuelve a mandar (dentro de las 24 h).
    /// </summary>
    private async Task RecuperarEnviosInterrumpidosAsync(EstanciasContext context)
    {
        var limite = DateTime.Now.AddMinutes(-15);
        var colgados = await context.EnvioResumenMail
            .Where(e => e.Estado == EstadosEnvio.Enviando && e.FechaUltimoIntento < limite)
            .ToListAsync();

        foreach (var e in colgados)
        {
            const string mensaje = "Interrumpido: la aplicación se detuvo durante el envío y no se obtuvo respuesta del proveedor.";
            e.Estado = e.EsPrueba ? EstadosEnvio.FallidoDefinitivo : EstadosEnvio.Fallido;
            e.Error = mensaje;
            e.CodigoErrorProveedor = "interrumpido";
            e.ProximoIntento = DateTime.Now;

            context.EnvioResumenMailIntento.Add(new EnvioResumenMailIntento
            {
                EnvioResumenMailId = e.Id,
                NroIntento = e.Intentos,
                Fecha = DateTime.Now,
                EmailDestino = e.EmailDestino,
                Exito = false,
                CodigoErrorProveedor = "interrumpido",
                Error = mensaje,
                IdempotencyKey = EnvioResumenService.ClaveIdempotencia(e, e.EmailDestino),
                Reintentable = !e.EsPrueba
            });
        }

        if (colgados.Any()) await context.SaveChangesAsync();
    }

    /// <summary>No se mandan resúmenes de períodos cuyo vencimiento ya pasó.</summary>
    private async Task OmitirPeriodosVencidosAsync(EstanciasContext context)
    {
        var hoy = DateTime.Today;
        var vencidos = await context.EnvioResumenMail
            .Where(e => !e.EsPrueba
                     && (e.Estado == EstadosEnvio.Pendiente || e.Estado == EstadosEnvio.Fallido)
                     && e.Periodo.FechaVencimiento < hoy)
            .ToListAsync();

        foreach (var e in vencidos)
        {
            e.Estado = EstadosEnvio.Omitido;
            e.MotivoOmision = MotivosOmision.PeriodoVencido;
            e.ProximoIntento = null;
        }

        if (vencidos.Any())
        {
            await context.SaveChangesAsync();
            _logger.LogWarning("Se omitieron {Cantidad} envíos porque su período ya venció.", vencidos.Count);
        }
    }

    /// <summary>Crea la ejecución de envío "EnCurso" del período si todavía no hay una abierta.</summary>
    private async Task AbrirEjecucionesEnvioAsync(EstanciasContext context, List<int> ids)
    {
        var periodos = await context.EnvioResumenMail.AsNoTracking()
            .Where(e => ids.Contains(e.Id)).Select(e => e.PeriodoId).Distinct().ToListAsync();

        foreach (var periodoId in periodos)
        {
            bool abierta = await context.EjecucionProceso.AnyAsync(e =>
                e.Codigo == CodigosProceso.EnvioResumen && e.PeriodoId == periodoId && e.Estado == EstadosEjecucion.EnCurso);

            if (!abierta)
            {
                context.EjecucionProceso.Add(new EjecucionProceso
                {
                    Codigo = CodigosProceso.EnvioResumen,
                    PeriodoId = periodoId,
                    FechaInicio = DateTime.Now,
                    Estado = EstadosEjecucion.EnCurso,
                    Servidor = Environment.MachineName
                });
            }
        }
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Cierra la ejecución de envío de un período cuando ya no queda nada por mandar
    /// y la generación de ese período terminó. Manda el resumen final a los administradores.
    /// </summary>
    private async Task CerrarPeriodosTerminadosAsync(IServiceScope scope, EstanciasContext context)
    {
        var abiertas = await context.EjecucionProceso
            .Where(e => e.Codigo == CodigosProceso.EnvioResumen && e.Estado == EstadosEjecucion.EnCurso)
            .ToListAsync();

        foreach (var ejecucion in abiertas)
        {
            var periodoId = ejecucion.PeriodoId;

            bool generacionEnCurso = await context.EjecucionProceso.AnyAsync(e =>
                e.Codigo == CodigosProceso.GenerarResumen && e.PeriodoId == periodoId && e.Estado == EstadosEjecucion.EnCurso);
            if (generacionEnCurso) continue;

            var conteo = await context.EnvioResumenMail.AsNoTracking()
                .Where(e => e.PeriodoId == periodoId && !e.EsPrueba)
                .GroupBy(e => e.Estado)
                .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                .ToListAsync();

            int Cant(string estado) => conteo.Where(c => c.Estado == estado).Sum(c => c.Cantidad);

            // Los Fallido con reintento programado siguen abiertos hasta agotar intentos
            if (Cant(EstadosEnvio.Pendiente) + Cant(EstadosEnvio.Enviando) + Cant(EstadosEnvio.Fallido) > 0) continue;

            ejecucion.Exitosos = Cant(EstadosEnvio.Enviado);
            ejecucion.Fallidos = Cant(EstadosEnvio.FallidoDefinitivo);
            ejecucion.Omitidos = Cant(EstadosEnvio.Omitido);
            ejecucion.Total = ejecucion.Exitosos + ejecucion.Fallidos + ejecucion.Omitidos;
            ejecucion.Estado = ejecucion.Fallidos == 0 ? EstadosEjecucion.Completado : EstadosEjecucion.CompletadoConErrores;
            ejecucion.FechaFin = DateTime.Now;
            await context.SaveChangesAsync();

            _logger.LogInformation("Envío de resúmenes del período {PeriodoId} cerrado: {Enviados} enviados, {Fallidos} fallidos, {Omitidos} omitidos.",
                periodoId, ejecucion.Exitosos, ejecucion.Fallidos, ejecucion.Omitidos);

            await EnviarNotificacionAdminAsync(scope,
                $"Envío de Resúmenes: {ejecucion.Estado}",
                $"Terminó el envío de resúmenes del período {periodoId} a las {DateTime.Now:G}.<br/><br/>" +
                $"<strong>Enviados:</strong> {ejecucion.Exitosos}<br/>" +
                $"<strong>Fallidos (sin más reintentos):</strong> {ejecucion.Fallidos}<br/>" +
                $"<strong>Omitidos (no recibe / sin saldo / sin mail / vencido):</strong> {ejecucion.Omitidos}<br/><br/>" +
                "El detalle por cliente está en la tabla EnvioResumenMail.");
        }
    }

    // ------------------------------------------------------------------
    // PRUEBA
    // ------------------------------------------------------------------

    /// <summary>
    /// Manda a las casillas de prueba una muestra de resúmenes reales de la cola.
    /// Las filas de los clientes no se modifican; cada envío de prueba queda como fila aparte (EsPrueba = true).
    /// Se limita a CantidadPrueba resúmenes por período, así no se repite en cada ciclo.
    /// </summary>
    private async Task EnviarPruebaAsync(IServiceScope scope, EstanciasContext context, CancellationToken stoppingToken)
    {
        var emailsPrueba = (_configuration["EnvioResumen:EmailsPrueba"] ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Contains("@"))
            .ToList();

        if (emailsPrueba.Count == 0)
        {
            await AvisarModoAsync(context, "Modo Prueba sin casillas en EnvioResumen:EmailsPrueba: no se envía nada. {Pendientes} resúmenes esperan en la cola.");
            return;
        }

        var cantidadPrueba = _configuration.GetValue<int>("EnvioResumen:CantidadPrueba", 3);

        var periodosConPendientes = await context.EnvioResumenMail.AsNoTracking()
            .Where(e => !e.EsPrueba && e.Estado == EstadosEnvio.Pendiente)
            .Select(e => e.PeriodoId).Distinct().ToListAsync(stoppingToken);

        foreach (var periodoId in periodosConPendientes)
        {
            var yaProbados = await context.EnvioResumenMail.AsNoTracking()
                .Where(e => e.EsPrueba && e.PeriodoId == periodoId)
                .Select(e => e.ResumenTarjetaId).Distinct().ToListAsync(stoppingToken);

            var faltan = cantidadPrueba - yaProbados.Count;
            if (faltan <= 0) continue;

            var muestra = await context.EnvioResumenMail.AsNoTracking()
                .Where(e => !e.EsPrueba && e.Estado == EstadosEnvio.Pendiente && e.PeriodoId == periodoId && !yaProbados.Contains(e.ResumenTarjetaId))
                .OrderBy(e => e.Id)
                .Take(faltan)
                .ToListAsync(stoppingToken);

            foreach (var real in muestra)
            {
                foreach (var emailPrueba in emailsPrueba)
                {
                    if (stoppingToken.IsCancellationRequested) return;

                    var prueba = new EnvioResumenMail
                    {
                        ResumenTarjetaId = real.ResumenTarjetaId,
                        PeriodoId = real.PeriodoId,
                        UsuarioId = real.UsuarioId,
                        EmailCliente = real.EmailCliente,
                        EmailDestino = emailPrueba,
                        EsPrueba = true,
                        Estado = EstadosEnvio.Pendiente,
                        FechaCreacion = DateTime.Now
                    };
                    context.EnvioResumenMail.Add(prueba);
                    await context.SaveChangesAsync();

                    // MaxIntentos = 1 en prueba: si falla queda FallidoDefinitivo y no se reintenta
                    await scope.ServiceProvider.GetRequiredService<EnvioResumenService>().EnviarAsync(context, prueba, emailPrueba, maxIntentos: 1);
                    if (prueba.Estado == EstadosEnvio.Fallido)
                    {
                        prueba.Estado = EstadosEnvio.FallidoDefinitivo;
                        prueba.ProximoIntento = null;
                        await context.SaveChangesAsync();
                    }

                    _logger.LogInformation("Envío de PRUEBA del resumen {ResumenId} (cliente {Cliente}) a {Destino}: {Estado}.",
                        real.ResumenTarjetaId, real.EmailCliente, emailPrueba, prueba.Estado);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // AUXILIARES
    // ------------------------------------------------------------------

    /// <summary>Deja constancia en el log (como máximo una vez por hora) de que el envío está apagado.</summary>
    private async Task AvisarModoAsync(EstanciasContext context, string mensaje)
    {
        if (DateTime.Now - _ultimoAvisoModo < TimeSpan.FromHours(1)) return;
        _ultimoAvisoModo = DateTime.Now;

        var pendientes = await context.EnvioResumenMail.CountAsync(e => !e.EsPrueba && e.Estado == EstadosEnvio.Pendiente);
        _logger.LogWarning(mensaje, pendientes);
    }

    private async Task EnviarNotificacionAdminAsync(IServiceScope scope, string asunto, string cuerpoHTML)
    {
        var emails = _configuration["NotificationSettings:AdminEmails"];
        var adminEmails = emails?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
        if (adminEmails.Length == 0) return;

        var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();
        foreach (var emailDestino in adminEmails)
        {
            var resultado = await mailService.EnviarConResultadoAsync(new MailAPI { Mail = emailDestino.Trim(), Titulo = asunto, Html = cuerpoHTML });
            if (!resultado.Exito)
                _logger.LogWarning("No se pudo enviar la notificación '{Asunto}' a {Email}: {Error}", asunto, emailDestino, resultado.Error);
        }
    }

}
