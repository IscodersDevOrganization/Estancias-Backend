using Commons.Models;
using DAL.Data;
using DAL.DTOs.Reportes;
using DAL.Models;
using EstanciasCore.Controllers;
using EstanciasCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Panel de control de la generación de resúmenes y de la cola de envío por mail.
/// Solo consulta y reprograma envíos: no manda mails. Los mails los manda el EnvioDeResumenWorker
/// únicamente si EnvioResumen:Modo lo permite.
/// </summary>
[Area("Reportes")]
public class ColaResumenesController : EstanciasCoreController
{
    private const int MaximoFilas = 5000;
    private readonly IConfiguration _configuration;

    public ColaResumenesController(EstanciasContext context, IConfiguration configuration) : base(context)
    {
        breadcumb.Add(new Message() { DisplayName = "Reportes" });
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(int? periodoId)
    {
        breadcumb.Add(new Message() { DisplayName = "Cola de Resúmenes" });
        ViewBag.Breadcrumb = breadcumb;

        var panel = new ColaResumenesPanelDTO();

        // --- Configuración actual ---
        var modo = (_configuration["EnvioResumen:Modo"] ?? "").Trim();
        if (!modo.Equals("Produccion", StringComparison.OrdinalIgnoreCase) && !modo.Equals("Prueba", StringComparison.OrdinalIgnoreCase))
            modo = "Deshabilitado";
        panel.ModoEnvio = modo;
        panel.EmailsPrueba = _configuration["EnvioResumen:EmailsPrueba"];

        var procedimientos = await _context.Procedimientos.AsNoTracking()
            .Where(p => p.Codigo == CodigosProceso.GenerarResumen || p.Codigo == CodigosProceso.EnvioResumen)
            .ToListAsync();
        var procGeneracion = procedimientos.FirstOrDefault(p => p.Codigo == CodigosProceso.GenerarResumen);
        var procEnvio = procedimientos.FirstOrDefault(p => p.Codigo == CodigosProceso.EnvioResumen);
        panel.GeneracionActiva = procGeneracion?.Activo ?? false;
        panel.DiaGeneracion = procGeneracion?.DiaEjecucion;
        panel.UltimaGeneracionExitosa = procGeneracion?.FechaUltimaEjecucionExitosa;
        panel.EnvioActivo = procEnvio?.Activo ?? false;

        // --- Períodos disponibles ---
        panel.Periodos = await _context.Periodo.AsNoTracking()
            .OrderByDescending(p => p.FechaVencimiento)
            .Take(24)
            .Select(p => new PeriodoOpcionDTO { Id = p.Id, Descripcion = p.Descripcion, FechaVencimiento = p.FechaVencimiento })
            .ToListAsync();

        if (!periodoId.HasValue)
        {
            // Por defecto: el último período con actividad en la cola o en la generación
            var ultimoConEnvios = await _context.EnvioResumenMail.AsNoTracking().OrderByDescending(e => e.Id).Select(e => (int?)e.PeriodoId).FirstOrDefaultAsync();
            var ultimoConGeneracion = await _context.ResumenGeneracion.AsNoTracking().OrderByDescending(e => e.Id).Select(e => (int?)e.PeriodoId).FirstOrDefaultAsync();
            periodoId = new[] { ultimoConEnvios, ultimoConGeneracion }.Where(x => x.HasValue).Select(x => x.Value).DefaultIfEmpty().Max();
            if (periodoId == 0) periodoId = panel.Periodos.FirstOrDefault()?.Id;
        }

        panel.PeriodoId = periodoId;
        if (!periodoId.HasValue) return View(panel);

        var periodo = await _context.Periodo.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodoId.Value);
        if (periodo != null)
        {
            panel.PeriodoDescripcion = periodo.Descripcion;
            panel.PeriodoVencimiento = periodo.FechaVencimiento;
            panel.PeriodoVencido = periodo.FechaVencimiento.Date < DateTime.Today;
            if (!panel.Periodos.Any(p => p.Id == periodo.Id))
                panel.Periodos.Insert(0, new PeriodoOpcionDTO { Id = periodo.Id, Descripcion = periodo.Descripcion, FechaVencimiento = periodo.FechaVencimiento });
        }

        // --- Generación ---
        var generacion = await _context.ResumenGeneracion.AsNoTracking()
            .Where(g => g.PeriodoId == periodoId.Value)
            .GroupBy(g => new { g.Estado, g.CodigoError })
            .Select(g => new { g.Key.Estado, g.Key.CodigoError, Cantidad = g.Count() })
            .ToListAsync();

        panel.Generados = generacion.Where(g => g.Estado == EstadosGeneracion.Generado).Sum(g => g.Cantidad);
        panel.FallidosGeneracion = generacion.Where(g => g.Estado == EstadosGeneracion.Fallido).Sum(g => g.Cantidad);
        panel.FallosGeneracionPorCodigo = generacion
            .Where(g => g.Estado == EstadosGeneracion.Fallido)
            .Select(g => new ConteoDTO { Clave = g.CodigoError ?? "(sin código)", Cantidad = g.Cantidad })
            .OrderByDescending(c => c.Cantidad).ToList();

        // --- Envío ---
        var envios = await _context.EnvioResumenMail.AsNoTracking()
            .Where(e => e.PeriodoId == periodoId.Value && !e.EsPrueba)
            .GroupBy(e => new { e.Estado, e.MotivoOmision, e.CodigoErrorProveedor })
            .Select(g => new { g.Key.Estado, g.Key.MotivoOmision, g.Key.CodigoErrorProveedor, Cantidad = g.Count() })
            .ToListAsync();

        int Cant(string estado) => envios.Where(e => e.Estado == estado).Sum(e => e.Cantidad);
        panel.Pendientes = Cant(EstadosEnvio.Pendiente);
        panel.Enviando = Cant(EstadosEnvio.Enviando);
        panel.Enviados = Cant(EstadosEnvio.Enviado);
        panel.Reintentando = Cant(EstadosEnvio.Fallido);
        panel.FallidosDefinitivos = Cant(EstadosEnvio.FallidoDefinitivo);
        panel.Omitidos = Cant(EstadosEnvio.Omitido);

        panel.OmitidosPorMotivo = envios
            .Where(e => e.Estado == EstadosEnvio.Omitido)
            .GroupBy(e => e.MotivoOmision ?? "(sin motivo)")
            .Select(g => new ConteoDTO { Clave = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .OrderByDescending(c => c.Cantidad).ToList();

        panel.ErroresPorCodigo = envios
            .Where(e => e.Estado == EstadosEnvio.Fallido || e.Estado == EstadosEnvio.FallidoDefinitivo)
            .GroupBy(e => e.CodigoErrorProveedor ?? "(sin código)")
            .Select(g => new ConteoDTO { Clave = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .OrderByDescending(c => c.Cantidad).ToList();

        panel.EnviosPrueba = await _context.EnvioResumenMail.AsNoTracking()
            .CountAsync(e => e.PeriodoId == periodoId.Value && e.EsPrueba);

        panel.ProximoReintento = await _context.EnvioResumenMail.AsNoTracking()
            .Where(e => e.PeriodoId == periodoId.Value && !e.EsPrueba && e.Estado == EstadosEnvio.Fallido && e.ProximoIntento != null)
            .OrderBy(e => e.ProximoIntento)
            .Select(e => e.ProximoIntento)
            .FirstOrDefaultAsync();

        panel.GeneracionEnCurso = await HayGeneracionEnCursoAsync(periodoId.Value);

        panel.Ejecuciones = await _context.EjecucionProceso.AsNoTracking()
            .Where(e => e.PeriodoId == periodoId.Value)
            .OrderByDescending(e => e.Id)
            .ToListAsync();

        return View(panel);
    }

    [HttpPost]
    public async Task<IActionResult> _ListadoGeneracion(int periodoId, string estado, string codigoError, string buscar)
    {
        var query = _context.ResumenGeneracion.AsNoTracking().Where(g => g.PeriodoId == periodoId);

        if (!string.IsNullOrEmpty(estado)) query = query.Where(g => g.Estado == estado);
        if (!string.IsNullOrEmpty(codigoError)) query = query.Where(g => g.CodigoError == codigoError);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.Trim();
            query = query.Where(g => g.Usuario.UserName.Contains(b) || g.Usuario.Personas.NroDocumento.Contains(b) || g.Usuario.Personas.NroTarjeta.Contains(b));
        }

        var items = await query
            .OrderByDescending(g => g.FechaUltimoIntento)
            .Take(MaximoFilas)
            .Select(g => new GeneracionItemDTO
            {
                Id = g.Id,
                UsuarioId = g.UsuarioId,
                Email = g.Usuario.UserName,
                NroDocumento = g.Usuario.Personas.NroDocumento,
                NroTarjeta = g.Usuario.Personas.NroTarjeta,
                Estado = g.Estado,
                CodigoError = g.CodigoError,
                Mensaje = g.Mensaje,
                Intentos = g.Intentos,
                FechaUltimoIntento = g.FechaUltimoIntento,
                ResumenTarjetaId = g.ResumenTarjetaId,
                EjecucionProcesoId = g.EjecucionProcesoId
            })
            .ToListAsync();

        ViewBag.MaximoFilas = MaximoFilas;
        return PartialView(items);
    }

    [HttpPost]
    public async Task<IActionResult> _ListadoEnvios(int periodoId, string estado, string codigoError, string buscar, bool incluirPruebas = false)
    {
        var query = _context.EnvioResumenMail.AsNoTracking().Where(e => e.PeriodoId == periodoId);

        if (!incluirPruebas) query = query.Where(e => !e.EsPrueba);
        if (!string.IsNullOrEmpty(estado)) query = query.Where(e => e.Estado == estado);
        if (!string.IsNullOrEmpty(codigoError)) query = query.Where(e => e.CodigoErrorProveedor == codigoError || e.MotivoOmision == codigoError);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.Trim();
            query = query.Where(e => e.EmailCliente.Contains(b) || e.EmailDestino.Contains(b) || e.Usuario.Personas.NroDocumento.Contains(b));
        }

        var items = await query
            .OrderByDescending(e => e.Id)
            .Take(MaximoFilas)
            .Select(ProyeccionEnvio())
            .ToListAsync();

        ViewBag.MaximoFilas = MaximoFilas;
        return PartialView(items);
    }

    [HttpGet]
    public async Task<IActionResult> _DetalleGeneracion(int id)
    {
        var item = await _context.ResumenGeneracion.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new GeneracionItemDTO
            {
                Id = g.Id,
                UsuarioId = g.UsuarioId,
                Email = g.Usuario.UserName,
                NroDocumento = g.Usuario.Personas.NroDocumento,
                NroTarjeta = g.Usuario.Personas.NroTarjeta,
                Estado = g.Estado,
                CodigoError = g.CodigoError,
                Mensaje = g.Mensaje,
                Detalle = g.Detalle,
                Intentos = g.Intentos,
                FechaUltimoIntento = g.FechaUltimoIntento,
                ResumenTarjetaId = g.ResumenTarjetaId,
                EjecucionProcesoId = g.EjecucionProcesoId
            })
            .FirstOrDefaultAsync();

        if (item == null) return NotFound("No se encontró el registro de generación.");
        return PartialView(item);
    }

    /// <summary>
    /// Devuelve el PDF guardado de un resumen (ResumenTarjeta) para verlo dentro de la app o descargarlo.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> VerPdf(int id, bool descargar = false)
    {
        var resumen = await _context.ResumenTarjeta.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.Adjunto, Documento = r.Usuario.Personas.NroDocumento, r.FechaVencimiento })
            .FirstOrDefaultAsync();

        if (resumen == null) return NotFound("No se encontró el resumen.");
        if (resumen.Adjunto == null || resumen.Adjunto.Length == 0) return NotFound("El resumen no tiene PDF guardado.");

        var nombre = $"Resumen_{resumen.Documento}_{resumen.FechaVencimiento:yyyyMM}.pdf";
        if (descargar) return File(resumen.Adjunto, "application/pdf", nombre);

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{nombre}\"";
        return File(resumen.Adjunto, "application/pdf");
    }

    [HttpGet]
    public async Task<IActionResult> _DetalleEnvio(int id)
    {
        var envio = await _context.EnvioResumenMail.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(ProyeccionEnvio())
            .FirstOrDefaultAsync();

        if (envio == null) return NotFound("No se encontró el envío.");

        var detalle = new EnvioDetalleDTO
        {
            Envio = envio,
            HistorialIntentos = await _context.EnvioResumenMailIntento.AsNoTracking()
                .Where(i => i.EnvioResumenMailId == id)
                .OrderByDescending(i => i.Id)
                .ToListAsync()
        };
        return PartialView(detalle);
    }

    /// <summary>
    /// Vuelve a dejar un envío como Pendiente (no lo manda: lo despacha el worker según el Modo de envío).
    /// Toma el mail actual del usuario por si fue corregido.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReintentarEnvio(int id)
    {
        var envio = await _context.EnvioResumenMail.FirstOrDefaultAsync(e => e.Id == id);
        if (envio == null) return Json(new { ok = false, mensaje = "No se encontró el envío." });
        if (envio.EsPrueba) return Json(new { ok = false, mensaje = "Los envíos de prueba no se reintentan." });
        if (envio.Estado != EstadosEnvio.Fallido && envio.Estado != EstadosEnvio.FallidoDefinitivo)
            return Json(new { ok = false, mensaje = $"Solo se pueden reintentar envíos fallidos (estado actual: {envio.Estado})." });

        await ReprogramarAsync(new List<EnvioResumenMail> { envio });
        return Json(new { ok = true, mensaje = "El envío quedó Pendiente. Se va a mandar cuando el modo de envío lo permita." });
    }

    /// <summary>Vuelve a dejar como Pendiente todos los envíos fallidos definitivos del período.</summary>
    [HttpPost]
    public async Task<IActionResult> ReintentarFallidos(int periodoId)
    {
        var envios = await _context.EnvioResumenMail
            .Where(e => e.PeriodoId == periodoId && !e.EsPrueba && e.Estado == EstadosEnvio.FallidoDefinitivo)
            .ToListAsync();

        if (!envios.Any()) return Json(new { ok = false, mensaje = "No hay envíos fallidos para reintentar en este período." });

        await ReprogramarAsync(envios);
        return Json(new { ok = true, mensaje = $"{envios.Count} envíos quedaron Pendientes. Se van a mandar cuando el modo de envío lo permita." });
    }

    /// <summary>
    /// PRUEBA CONTROLADA: toma resúmenes reales del período y los manda ÚNICAMENTE a las casillas de
    /// EnvioResumen:EmailsPrueba (appsettings). Nunca usa el mail del cliente como destino.
    /// Cada envío queda registrado como EsPrueba = true, con su historial y error exacto.
    /// Funciona aunque el Modo de envío esté Deshabilitado, porque no toca la cola real.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EnviarPrueba(int periodoId, [FromServices] EnvioResumenService envioService, int cantidad = 1)
    {
        var emailsPrueba = LeerEmailsPrueba();
        if (!emailsPrueba.Any())
            return Json(new { ok = false, mensaje = "No hay casillas de prueba configuradas en EnvioResumen:EmailsPrueba." });

        cantidad = Math.Max(1, Math.Min(cantidad, 3));

        var resumenes = await _context.ResumenTarjeta.AsNoTracking()
            .Where(r => r.PeriodoId == periodoId && r.Adjunto != null && (r.Monto + r.MontoAdeudado) > 0)
            .OrderByDescending(r => r.Id)
            .Take(cantidad)
            .Select(r => new { r.Id, r.UsuarioId, Email = r.Usuario.UserName })
            .ToListAsync();

        if (!resumenes.Any())
            return Json(new { ok = false, mensaje = "El período seleccionado no tiene resúmenes con PDF y saldo para usar en la prueba." });

        var resultados = new List<ResultadoPruebaDTO>();
        foreach (var resumen in resumenes)
        {
            foreach (var destino in emailsPrueba)
            {
                var prueba = new EnvioResumenMail
                {
                    ResumenTarjetaId = resumen.Id,
                    PeriodoId = periodoId,
                    UsuarioId = resumen.UsuarioId,
                    EmailCliente = resumen.Email,
                    EmailDestino = destino,
                    EsPrueba = true,
                    Estado = EstadosEnvio.Pendiente,
                    FechaCreacion = DateTime.Now
                };
                _context.EnvioResumenMail.Add(prueba);
                await _context.SaveChangesAsync();

                // Un solo intento: en prueba no se reintenta
                var resultado = await envioService.EnviarAsync(_context, prueba, destino, maxIntentos: 1);

                resultados.Add(new ResultadoPruebaDTO
                {
                    id = prueba.Id,
                    cliente = resumen.Email,
                    destino = destino,
                    ok = resultado.Exito,
                    estado = prueba.Estado,
                    codigo = resultado.CodigoError,
                    http = resultado.StatusCode,
                    error = resultado.Error,
                    messageId = resultado.MessageId
                });
            }
        }

        var enviados = resultados.Count(r => r.ok);
        return Json(new
        {
            ok = true,
            mensaje = $"{enviados} de {resultados.Count} mails de prueba enviados.",
            resultados
        });
    }

    /// <summary>
    /// Vuelve a generar, en segundo plano, SOLO los resúmenes que quedaron con error en el período.
    /// No envía mails: los resúmenes que se generen entran a la cola como cualquier otro.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegenerarConError(int periodoId,
        [FromServices] Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
        [FromServices] Microsoft.AspNetCore.Hosting.IApplicationLifetime lifetime)
    {
        var periodo = await _context.Periodo.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo == null)
            return Json(new { ok = false, mensaje = "No se encontró el período." });

        if (periodo.FechaVencimiento.Date < DateTime.Today)
            return Json(new { ok = false, mensaje = "El período ya venció: no se regeneran resúmenes de períodos vencidos." });

        if (await HayGeneracionEnCursoAsync(periodoId))
            return Json(new { ok = false, mensaje = "Ya hay una generación en curso para este período. Esperá a que termine." });

        var conError = await _context.ResumenGeneracion.AsNoTracking()
            .CountAsync(g => g.PeriodoId == periodoId && g.Estado == EstadosGeneracion.Fallido);
        if (conError == 0)
            return Json(new { ok = false, mensaje = "No hay resúmenes con error en este período." });

        var usuario = User?.Identity?.Name;
        var token = lifetime.ApplicationStopping;

        // Corre en segundo plano con su propio scope: la generación puede tardar y no debe depender del request
        _ = Task.Run(async () =>
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<ColaResumenesController>>();
                try
                {
                    logger?.LogInformation("Regeneración de resúmenes con error del período {PeriodoId} lanzada por {Usuario}.", periodoId, usuario);
                    var servicio = scope.ServiceProvider.GetRequiredService<EstanciasCore.Interface.IResumenTarjetaService>();
                    await servicio.RegenerarFallidosAsync(periodoId, token);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Falló la regeneración de resúmenes con error del período {PeriodoId}.", periodoId);
                }
            }
        });

        return Json(new { ok = true, mensaje = $"Se lanzó la regeneración de {conError} resúmenes con error. Vas a ver el avance en la pestaña Ejecuciones." });
    }

    private Task<bool> HayGeneracionEnCursoAsync(int periodoId)
    {
        return _context.EjecucionProceso.AsNoTracking().AnyAsync(e =>
            e.PeriodoId == periodoId &&
            e.Estado == EstadosEjecucion.EnCurso &&
            (e.Codigo == CodigosProceso.GenerarResumen || e.Codigo == CodigosProceso.RegenerarFallidos));
    }

    private List<string> LeerEmailsPrueba()
    {
        return (_configuration["EnvioResumen:EmailsPrueba"] ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Contains("@"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ReprogramarAsync(List<EnvioResumenMail> envios)
    {
        var usuarioIds = envios.Select(e => e.UsuarioId).Distinct().ToList();
        var mails = await _context.Usuarios.AsNoTracking()
            .Where(u => usuarioIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(u => u.Id, u => u.UserName);

        foreach (var envio in envios)
        {
            if (mails.TryGetValue(envio.UsuarioId, out var mailActual) && !string.IsNullOrWhiteSpace(mailActual))
                envio.EmailCliente = mailActual.Trim();

            envio.Estado = EstadosEnvio.Pendiente;
            envio.Intentos = 0;              // El historial de intentos anteriores se conserva en EnvioResumenMailIntento
            envio.ProximoIntento = DateTime.Now;
        }

        await _context.SaveChangesAsync();
    }

    private static System.Linq.Expressions.Expression<Func<EnvioResumenMail, EnvioItemDTO>> ProyeccionEnvio()
    {
        return e => new EnvioItemDTO
        {
            Id = e.Id,
            ResumenTarjetaId = e.ResumenTarjetaId,
            PeriodoId = e.PeriodoId,
            UsuarioId = e.UsuarioId,
            EmailCliente = e.EmailCliente,
            EmailDestino = e.EmailDestino,
            NroDocumento = e.Usuario.Personas.NroDocumento,
            EsPrueba = e.EsPrueba,
            Estado = e.Estado,
            MotivoOmision = e.MotivoOmision,
            Proveedor = e.Proveedor,
            HttpStatus = e.HttpStatus,
            CodigoErrorProveedor = e.CodigoErrorProveedor,
            Error = e.Error,
            ProveedorMessageId = e.ProveedorMessageId,
            Intentos = e.Intentos,
            FechaUltimoIntento = e.FechaUltimoIntento,
            ProximoIntento = e.ProximoIntento,
            FechaEnvio = e.FechaEnvio,
            FechaCreacion = e.FechaCreacion
        };
    }
}

/// <summary>Resultado de cada mail de la prueba controlada (se devuelve como JSON al panel).</summary>
public class ResultadoPruebaDTO
{
    public int id { get; set; }
    public string cliente { get; set; }
    public string destino { get; set; }
    public bool ok { get; set; }
    public string estado { get; set; }
    public string codigo { get; set; }
    public int? http { get; set; }
    public string error { get; set; }
    public string messageId { get; set; }
}
