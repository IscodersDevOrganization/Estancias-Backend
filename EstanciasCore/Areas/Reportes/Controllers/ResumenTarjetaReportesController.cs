using Commons.Models;
using DAL.Data;
using DAL.DTOs.Reportes;
using DAL.Models;
using EstanciasCore.Controllers; // Tu controlador base
using EstanciasCore.Interface; // Para IDatosTarjetaService
using EstanciasCore.Services;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


[Area("Reportes")]
public class ResumenTarjetaReportesController : EstanciasCoreController
{
    private readonly IDatosTarjetaService _datosServices;
    private readonly ICompositeViewEngine _viewEngine;

    public ResumenTarjetaReportesController(EstanciasContext context, IDatosTarjetaService datosServices, ICompositeViewEngine viewEngine) : base(context)
    {
        breadcumb.Add(new Message() { DisplayName = "Reportes" });
        _datosServices = datosServices;
        _viewEngine = viewEngine;
    }

    public IActionResult Index()
    {
        breadcumb.Add(new Message() { DisplayName = "Resumen de Tarjeta" });
        ViewBag.Breadcrumb = breadcumb;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> _ListadoResumenes(string nroTarjetaFiltro, string nroDocumentoFiltro)
    {
        try
        {
            if (string.IsNullOrEmpty(nroTarjetaFiltro) && string.IsNullOrEmpty(nroDocumentoFiltro))
                return PartialView(new List<ResumenTarjetaDTO>());

            IQueryable<Usuario> query = _context.Usuarios;
            if (!string.IsNullOrEmpty(nroTarjetaFiltro))
                query = query.Where(x => x.Personas.NroTarjeta == nroTarjetaFiltro);
            if (!string.IsNullOrEmpty(nroDocumentoFiltro))
                query = query.Where(x => x.Personas.NroDocumento == nroDocumentoFiltro);

            var usuario = await query.FirstOrDefaultAsync();

            if (usuario == null)
            {
                ViewBag.ErrorMessage = "No se encontró ningún usuario con los datos proporcionados.";
                return PartialView(new List<ResumenTarjetaDTO>());
            }

            DateTime fechaActual = DateTime.Now.AddMonths(1);

            List<ResumenTarjeta> movimientos = _context.ResumenTarjeta.Where(x => x.Usuario.Id == usuario.Id && x.Periodo != null).Where(e => e.Periodo.FechaHasta<fechaActual).ToList();

            List<ResumenTarjetaDTO> resumenes = movimientos.GroupBy(g => g.Periodo)
                .Select(g => new ResumenTarjetaDTO
                {
                    UsuarioId = usuario.Id,
                    PeriodoId = g.Key.Id,
                    Periodo = g.Key.Descripcion,
                    FechaVencimiento = g.Key.FechaVencimiento.ToString("dd/MM/yyyy"),
                    Monto = g.Sum(m => m.Monto),
                    Punitorios = g.Sum(m => m.MontoAdeudado)
                })
                .OrderByDescending(r => r.FechaVencimiento)
                .ToList();

            ViewBag.UsuarioId = usuario.Id;
            return PartialView(resumenes);
        }
        catch (Exception ex)
        {
            return Json(new { error = "Se produjo un error al procesar la solicitud: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DescargarResumen(string Id)
    {
        if (string.IsNullOrEmpty(Id))
        {
            return BadRequest("El ID no puede ser nulo o vacío.");
        }

        string[] partes = Id.Split(',');
        if (partes.Length != 2)
        {
            return BadRequest("El formato del ID es incorrecto. Se esperaba 'PeriodoId,UsuarioId'.");
        }

        if (!int.TryParse(partes[0], out int periodoId))
        {
            return BadRequest("El PeriodoId proporcionado no es un número válido.");
        }

        string usuarioId = partes[1];

        try
        {
            var resumen = await _context.ResumenTarjeta
                .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.PeriodoId == periodoId);

            if (resumen == null)
            {
                return NotFound("No se encontró un resumen para el período y usuario especificados.");
            }

            if (resumen.Adjunto == null || resumen.Adjunto.Length == 0)
            {
                return NotFound("El resumen fue encontrado pero no contiene un archivo adjunto.");
            }

            string base64String = Convert.ToBase64String(resumen.Adjunto);
            return PartialView("_VerResumen", base64String);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    [HttpGet]
    public IActionResult _Filtros()
    {
        return PartialView();
    }

    // ------------------------------------------------------------------
    // SIMULAR RESUMEN: genera el resumen con la lógica real, sin guardar nada
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> _SimularResumen()
    {
        var hoy = DateTime.Today;
        var opciones = new List<SelectListItem>();

        // Períodos que todavía no existen (el del mes y los dos siguientes): se arman en memoria con la misma regla de la generación
        var existentes = await _context.Periodo.AsNoTracking()
            .OrderByDescending(p => p.FechaVencimiento)
            .Take(12)
            .ToListAsync();

        var vencimientoDelMes = new DateTime(hoy.Year, hoy.Month, 15);
        var proximo = vencimientoDelMes.AddMonths(1);   // el próximo a generarse (día 1 del mes que viene)
        foreach (var venc in new[] { vencimientoDelMes.AddMonths(2), proximo, vencimientoDelMes })
        {
            if (!existentes.Any(p => p.FechaVencimiento.Date == venc.Date))
            {
                var calculado = ResumenTarjetaService.CalcularPeriodo(venc);
                opciones.Add(new SelectListItem
                {
                    Value = "nuevo:" + venc.ToString("yyyy-MM-dd"),
                    Text = $"{calculado.Descripcion} · vence {venc:dd/MM/yyyy} · nuevo"
                });
            }
        }

        opciones.AddRange(existentes.Select(p => new SelectListItem
        {
            Value = "id:" + p.Id,
            Text = $"{p.Descripcion} · vence {p.FechaVencimiento:dd/MM/yyyy}"
        }));

        // Por defecto queda elegido el próximo período a generarse
        var porDefecto = opciones.FirstOrDefault(o => o.Value == "nuevo:" + proximo.ToString("yyyy-MM-dd"))
                         ?? opciones.FirstOrDefault(o => existentes.Any(p => p.FechaVencimiento.Date == proximo.Date && o.Value == "id:" + p.Id))
                         ?? opciones.FirstOrDefault();
        if (porDefecto != null) porDefecto.Selected = true;
        ViewBag.Periodos = opciones;
        return PartialView();
    }

    /// <summary>
    /// Genera el resumen de un cliente para un período con la misma lógica que la generación mensual.
    /// NO guarda nada en la base: devuelve el PDF con marca de agua "SIMULACIÓN" para verlo o descargarlo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SimularResumen(string documentoOTarjeta, string periodo, DateTime? fechaGeneracion, [FromServices] IResumenTarjetaService resumenService)
    {
        try
        {
            var buscado = (documentoOTarjeta ?? "").Trim();
            if (string.IsNullOrEmpty(buscado))
                return Json(new { ok = false, mensaje = "Ingresá el DNI o el número de tarjeta del cliente." });

            // --- Período: uno existente o uno nuevo armado en memoria (no se guarda) ---
            Periodo periodoSimulado = null;
            if (!string.IsNullOrEmpty(periodo) && periodo.StartsWith("id:") && int.TryParse(periodo.Substring(3), out int periodoId))
            {
                periodoSimulado = await _context.Periodo.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodoId);
            }
            else if (!string.IsNullOrEmpty(periodo) && periodo.StartsWith("nuevo:") &&
                     DateTime.TryParseExact(periodo.Substring(6), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime venc))
            {
                periodoSimulado = ResumenTarjetaService.CalcularPeriodo(venc);
            }
            if (periodoSimulado == null)
                return Json(new { ok = false, mensaje = "Elegí un período válido." });

            // --- Cliente por DNI o por número de tarjeta ---
            var usuarios = await _context.Usuarios.AsNoTracking()
                .Where(u => u.Personas != null &&
                            !string.IsNullOrEmpty(u.Personas.NroTarjeta) &&
                            !string.IsNullOrEmpty(u.Personas.NroDocumento) &&
                            (u.Personas.NroDocumento == buscado || u.Personas.NroTarjeta == buscado))
                .Select(u => new DAL.DTOs.Servicios.UsuarioParaProcesarDTO
                {
                    Id = u.Id,
                    NombreCompleto = u.Personas.GetNombreCompleto(),
                    NroDocumento = u.Personas.NroDocumento,
                    NroTarjeta = u.Personas.NroTarjeta,
                    UserName = u.UserName
                })
                .Take(5)
                .ToListAsync();

            if (!usuarios.Any())
                return Json(new { ok = false, mensaje = $"No se encontró ningún cliente con DNI o tarjeta '{buscado}' que tenga tarjeta y documento cargados." });

            var usuario = usuarios.First();

            // Por defecto se simula como si se generara el día 1 del mes de vencimiento (igual que la generación automática)
            var fecha = (fechaGeneracion ?? new DateTime(periodoSimulado.FechaVencimiento.Year, periodoSimulado.FechaVencimiento.Month, 1)).Date;

            var leyenda = $"SIMULACIÓN - NO VÁLIDO COMO COMPROBANTE - generado el {DateTime.Now:dd/MM/yyyy HH:mm} por {User?.Identity?.Name}";
            var r = await resumenService.SimularResumenAsync(usuario, periodoSimulado, fecha, leyenda);

            var culturaAR = new CultureInfo("es-AR");
            return Json(new
            {
                ok = r.Ok,
                mensaje = r.Mensaje,
                codigo = r.CodigoError,
                detalle = r.Detalle,
                resultadoWs = r.ResultadoWs,
                variosClientes = usuarios.Count > 1,
                nombre = r.Nombre,
                documento = r.NroDocumento,
                tarjeta = r.NroTarjeta,
                mail = r.Mail,
                periodo = r.Periodo,
                periodoDesde = r.PeriodoDesde.ToString("dd/MM/yyyy"),
                periodoHasta = r.PeriodoHasta.ToString("dd/MM/yyyy"),
                vencimiento = r.Vencimiento.ToString("dd/MM/yyyy"),
                fechaGeneracion = r.FechaGeneracion.ToString("dd/MM/yyyy"),
                saldoPendiente = r.SaldoPendiente.ToString("C", culturaAR),
                saldoActual = r.SaldoActual.ToString("C", culturaAR),
                saldoTotal = r.SaldoTotal.ToString("C", culturaAR),
                consumosAnteriores = r.CantidadConsumosAnteriores,
                consumosDelMes = r.CantidadConsumosDelMes,
                pdf = r.Pdf != null ? Convert.ToBase64String(r.Pdf) : null,
                nombreArchivo = $"SIMULACION_Resumen_{r.NroDocumento}_{r.Vencimiento:yyyyMM}.pdf"
            });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, mensaje = "Error al simular el resumen: " + ex.Message, detalle = ex.ToString() });
        }
    }

    
}