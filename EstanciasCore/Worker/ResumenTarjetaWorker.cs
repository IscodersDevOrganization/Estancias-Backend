using DAL.Data;
using DAL.Models;
using DAL.Models.Core;
using EstanciasCore.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

/// <summary>
/// Genera los resúmenes del mes el día configurado en Procedimientos (Codigo = "GenerarResumen").
/// Cada resumen generado queda encolado para envío; este worker NO envía mails a clientes.
/// Si hay fallos, en la hora siguiente reintenta solo los usuarios que faltan (hasta MaxIntentosPorDia).
/// </summary>
public class ResumenMensualWorker : BackgroundService
{
    private readonly ILogger<ResumenMensualWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public ResumenMensualWorker(ILogger<ResumenMensualWorker> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de Resúmenes Mensuales iniciado.");

        await MarcarEjecucionesInterrumpidasAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();

                    var procedimiento = await context.Procedimientos
                        .FirstOrDefaultAsync(p => p.Codigo == CodigosProceso.GenerarResumen && p.Activo, stoppingToken);

                    if (procedimiento != null && await DebeEjecutarHoyAsync(context, procedimiento, stoppingToken))
                    {
                        await EjecutarAsync(scope, context, procedimiento, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error en el ciclo del worker de generación de resúmenes.");
                await EnviarNotificacionAsync("Error en Worker de Generación de Resúmenes", $"El worker falló de forma inesperada a las {DateTime.Now:G}.<br/><br/>{ex.Message}");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task EjecutarAsync(IServiceScope scope, EstanciasContext context, Procedimientos procedimiento, CancellationToken stoppingToken)
    {
        var resumenService = scope.ServiceProvider.GetRequiredService<IResumenTarjetaService>();
        var resultado = await resumenService.GenerarResumenTarjetas(stoppingToken);

        // Solo se marca como exitoso si no quedó ningún usuario con error.
        // Si hubo errores, el próximo ciclo reintenta solo los que faltan.
        if (resultado.Estado == EstadosEjecucion.Completado)
        {
            procedimiento.FechaUltimaEjecucionExitosa = DateTime.Now;
            await context.SaveChangesAsync();
        }

        await EnviarNotificacionAsync(
            $"Generación de Resúmenes: {resultado.Estado}",
            $"La generación terminó a las {DateTime.Now:G}.<br/><br/>" +
            $"<strong>Ejecución:</strong> {resultado.EjecucionId} &nbsp; <strong>Período:</strong> {resultado.PeriodoId}<br/>" +
            $"<strong>Usuarios pendientes procesados:</strong> {resultado.Total}<br/>" +
            $"<strong>Generados:</strong> {resultado.Generados}<br/>" +
            $"<strong>Con error:</strong> {resultado.Fallidos}<br/>" +
            $"<strong>Encolados para envío:</strong> {resultado.Encolados}<br/>" +
            $"<strong>Sin envío (no recibe / sin saldo / sin mail):</strong> {resultado.OmitidosEnvio}<br/>" +
            (string.IsNullOrEmpty(resultado.Error) ? "" : $"<br/><strong>Error general:</strong> {resultado.Error}<br/>") +
            "<br/>El detalle por usuario está en la tabla ResumenGeneracion."
        );
    }

    private async Task<bool> DebeEjecutarHoyAsync(EstanciasContext context, Procedimientos procedimiento, CancellationToken stoppingToken)
    {
        var hoy = DateTime.Today;

        if (hoy.Day != procedimiento.DiaEjecucion) return false;
        if (procedimiento.FechaUltimaEjecucionExitosa.HasValue && procedimiento.FechaUltimaEjecucionExitosa.Value.Date == hoy) return false;

        // Intentos contados en la base (antes estaban en memoria y se perdían al reiniciar la app)
        var maxIntentos = _configuration.GetValue<int>("ProcesoResumen:MaxIntentosPorDia", 3);
        var intentosHoy = await context.EjecucionProceso
            .CountAsync(e => e.Codigo == CodigosProceso.GenerarResumen && e.FechaInicio >= hoy, stoppingToken);

        if (intentosHoy >= maxIntentos)
        {
            _logger.LogWarning("Generación de resúmenes: se alcanzó el máximo de {Max} intentos para hoy.", maxIntentos);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Si la app se reinició en medio de una generación, esa ejecución quedó "EnCurso".
    /// La marcamos como Cancelado para que no bloquee el cierre del período.
    /// </summary>
    private async Task MarcarEjecucionesInterrumpidasAsync()
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EstanciasContext>();
                var colgadas = await context.EjecucionProceso
                    .Where(e => e.Codigo == CodigosProceso.GenerarResumen && e.Estado == EstadosEjecucion.EnCurso)
                    .ToListAsync();

                foreach (var e in colgadas)
                {
                    e.Estado = EstadosEjecucion.Cancelado;
                    e.FechaFin = DateTime.Now;
                    e.ErrorGeneral = "Interrumpida: la aplicación se reinició durante la generación.";
                }

                if (colgadas.Any())
                {
                    await context.SaveChangesAsync();
                    _logger.LogWarning("Se marcaron {Cantidad} ejecuciones de generación interrumpidas.", colgadas.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron revisar las ejecuciones interrumpidas.");
        }
    }

    /// <summary>Avisos internos a los administradores (NotificationSettings:AdminEmails).</summary>
    private async Task EnviarNotificacionAsync(string asunto, string cuerpoHTML)
    {
        var emails = _configuration["NotificationSettings:AdminEmails"];
        var adminEmails = emails?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];

        if (adminEmails.Length == 0)
        {
            _logger.LogWarning("No hay emails de administrador configurados. Se omite la notificación '{Asunto}'.", asunto);
            return;
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();
            foreach (var emailDestino in adminEmails)
            {
                var resultado = await mailService.EnviarConResultadoAsync(new MailAPI { Mail = emailDestino.Trim(), Titulo = asunto, Html = cuerpoHTML });
                if (!resultado.Exito)
                    _logger.LogWarning("No se pudo enviar la notificación '{Asunto}' a {Email}: {Error}", asunto, emailDestino, resultado.Error);
            }
        }
    }
}
