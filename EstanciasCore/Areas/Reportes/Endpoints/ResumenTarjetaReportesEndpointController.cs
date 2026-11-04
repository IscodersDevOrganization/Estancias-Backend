using Castle.Core.Internal;
using DAL.Data;
using DAL.DTOs.Reportes;
using DAL.Models;
using EstanciasCore.API.Filters;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Areas.Reportes.Endpoints
{
    [Area("Reportes")]
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [ApiController]
    [Route("reportes/endpoint/resumen-tarjeta-reportes")]
    public class ResumenTarjetaReportesEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;
        private readonly IDatosTarjetaService _datosServices;
        private readonly ICompositeViewEngine _viewEngine;

        public ResumenTarjetaReportesEndpointController(
            EstanciasContext context,
            IDatosTarjetaService datosServices,
            ICompositeViewEngine viewEngine)
        {
            _context = context;
            _datosServices = datosServices;
            _viewEngine = viewEngine;
        }

        // GET: /reportes/endpoint/resumen-tarjeta-reportes/simular/periodos
        [HttpGet("simular/periodos")]
        public async Task<IActionResult> PeriodosParaSimular()
        {
            var hoy = DateTime.Today;
            var opciones = new List<PeriodoSimulacionResumenDTO>();

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
                    opciones.Add(new PeriodoSimulacionResumenDTO
                    {
                        Value = "nuevo:" + venc.ToString("yyyy-MM-dd"),
                        Text = $"{calculado.Descripcion} · vence {venc:dd/MM/yyyy} · nuevo"
                    });
                }
            }

            opciones.AddRange(existentes.Select(p => new PeriodoSimulacionResumenDTO
            {
                Value = "id:" + p.Id,
                Text = $"{p.Descripcion} · vence {p.FechaVencimiento:dd/MM/yyyy}"
            }));

            // Por defecto queda elegido el próximo período a generarse
            var porDefecto = opciones.FirstOrDefault(o => o.Value == "nuevo:" + proximo.ToString("yyyy-MM-dd"))
                             ?? opciones.FirstOrDefault(o => existentes.Any(p => p.FechaVencimiento.Date == proximo.Date && o.Value == "id:" + p.Id))
                             ?? opciones.FirstOrDefault();
            if (porDefecto != null) porDefecto.Selected = true;
            return Ok(new { ok = true, data = opciones });
        }

        // POST: /reportes/endpoint/resumen-tarjeta-reportes/simular
        [HttpPost("simular")]
        public async Task<IActionResult> SimularResumen(
            [FromBody] SimularResumenTarjetaRequestDTO request,
            [FromServices] IResumenTarjetaService resumenService)
        {
            if (request == null)
            {
                return BadRequest(new { ok = false, mensaje = "Los datos de la simulación son obligatorios." });
            }

            var documentoOTarjeta = request.DocumentoOTarjeta;
            var periodo = request.Periodo;
            var fechaGeneracion = request.FechaGeneracion;

            try
            {
                var buscado = (documentoOTarjeta ?? "").Trim();
                if (string.IsNullOrEmpty(buscado))
                    return Ok(new { ok = false, mensaje = "Ingresá el DNI o el número de tarjeta del cliente." });

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
                    return Ok(new { ok = false, mensaje = "Elegí un período válido." });

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
                    return Ok(new { ok = false, mensaje = $"No se encontró ningún cliente con DNI o tarjeta '{buscado}' que tenga tarjeta y documento cargados." });

                var usuario = usuarios.First();

                // Por defecto se simula como si se generara el día 1 del mes de vencimiento (igual que la generación automática)
                var fecha = (fechaGeneracion ?? new DateTime(periodoSimulado.FechaVencimiento.Year, periodoSimulado.FechaVencimiento.Month, 1)).Date;

                var leyenda = $"SIMULACIÓN - NO VÁLIDO COMO COMPROBANTE - generado el {DateTime.Now:dd/MM/yyyy HH:mm} por {User?.Identity?.Name}";
                var r = await resumenService.SimularResumenAsync(usuario, periodoSimulado, fecha, leyenda);

                var culturaAR = new CultureInfo("es-AR");
                return Ok(new
                {
                    ok = r.Ok,
                    mensaje = r.Mensaje,
                    codigo = r.CodigoError,
                    detalle = r.Detalle,
                    resultadoWs = r.ResultadoWs,
                    variosClientes = usuarios.Count > 1,
                    diagnosticoConcepto = r.DiagnosticoConcepto,
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
                return Ok(new { ok = false, mensaje = "Error al simular el resumen: " + ex.Message, detalle = ex.ToString() });
            }
        }

        // GET: /reportes/endpoint/resumen-tarjeta-reportes/filtros
        [HttpGet("filtros")]
        public IActionResult Filtros()
        {
            return Ok(new FiltroResumenTarjetaRequestDTO
            {
                NroTarjetaFiltro = "",
                NroDocumentoFiltro = "",
                Buscar = "",
                Pagina = 1,
                Cantidad = 50
            });
        }

        // GET: /reportes/endpoint/resumen-tarjeta-reportes/todos
        [HttpGet("todos")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                DateTime fechaActual = DateTime.Now.AddMonths(1);

                var datos = await _context.ResumenTarjeta
                    .AsNoTracking()
                    .Where(x =>
                        x.Usuario != null &&
                        x.Usuario.Personas != null &&
                        x.Periodo != null &&
                        x.Periodo.FechaHasta < fechaActual
                    )
                    .GroupBy(x => new
                    {
                        UsuarioId = x.Usuario.Id,
                        PeriodoId = x.Periodo.Id,
                        NroTarjeta = x.Usuario.Personas.NroTarjeta,
                        Periodo = x.Periodo.Descripcion,
                        FechaVencimiento = x.Periodo.FechaVencimiento
                    })
                    .Select(g => new
                    {
                        UsuarioId = g.Key.UsuarioId,
                        PeriodoId = g.Key.PeriodoId,
                        NroTarjeta = g.Key.NroTarjeta,
                        Periodo = g.Key.Periodo,
                        FechaVencimiento = g.Key.FechaVencimiento,
                        Monto = g.Sum(x => x.Monto),
                        Punitorios = g.Sum(x => x.MontoAdeudado)
                    })
                    .OrderByDescending(x => x.FechaVencimiento)
                    .ToListAsync();

                List<ResumenTarjetaDTO> resumenes = datos
                    .Select(x =>
                    {
                        decimal montoTotal = x.Monto + x.Punitorios;

                        string descargarResumenUrl =
                            "/reportes/endpoint/resumen-tarjeta-reportes/descargar-resumen?Id=" +
                            x.PeriodoId + "," + x.UsuarioId;

                        string descargarArchivoUrl =
                            "/reportes/endpoint/resumen-tarjeta-reportes/descargar-resumen-archivo?Id=" +
                            x.PeriodoId + "," + x.UsuarioId;

                        string verComprobanteUrl =
                            "/reportes/endpoint/resumen-tarjeta-reportes/ver-comprobante?Id=" +
                            x.PeriodoId + "," + x.UsuarioId;

                        return new ResumenTarjetaDTO
                        {
                            Id = x.PeriodoId,
                            NroTarjeta = x.NroTarjeta ?? "",
                            UsuarioId = x.UsuarioId,
                            PeriodoId = x.PeriodoId,
                            Periodo = x.Periodo ?? "",
                            FechaVencimiento = x.FechaVencimiento.ToString("dd/MM/yyyy"),

                            Monto = x.Monto,
                            Punitorios = x.Punitorios,
                            MontoTotal = montoTotal,

                            MontoTexto = x.Monto.ToString("C2"),
                            PunitoriosTexto = x.Punitorios.ToString("C2"),
                            MontoTotalTexto = montoTotal.ToString("C2"),

                            DescargarResumenUrl = descargarResumenUrl,
                            DescargarResumenArchivoUrl = descargarArchivoUrl,
                            VerComprobanteUrl = verComprobanteUrl,

                            Accion =
                                "<a href=\"" + verComprobanteUrl +
                                "\" class=\"btn btn-warning btn-xs\" target=\"_blank\">" +
                                "<i class=\"fa fa-file-pdf-o\"></i></a>"
                        };
                    })
                    .ToList();

                return Ok(new
                {
                    Status = 200,
                    Mensaje = "Listado completo de resúmenes de tarjeta obtenido correctamente.",
                    TotalRegistros = resumenes.Count,
                    Data = resumenes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = 500,
                    Mensaje = "Se produjo un error al obtener el listado completo de resúmenes de tarjeta: " + ex.Message,
                    TotalRegistros = 0,
                    Data = new List<ResumenTarjetaDTO>()
                });
            }
        }

        // POST JSON
        [HttpPost("listado-resumenes")]
        [Consumes("application/json")]
        public async Task<IActionResult> ListadoResumenesJson(
            [FromBody] FiltroResumenTarjetaRequestDTO filtros)
        {
            return await ProcesarListadoResumenes(filtros);
        }

        // POST FORM
        [HttpPost("listado-resumenes")]
        [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
        public async Task<IActionResult> ListadoResumenesForm(
            [FromForm] FiltroResumenTarjetaRequestDTO filtros)
        {
            return await ProcesarListadoResumenes(filtros);
        }

        // GET QUERY
        [HttpGet("listado-resumenes")]
        public async Task<IActionResult> ListadoResumenesGet(
            [FromQuery] FiltroResumenTarjetaRequestDTO filtros)
        {
            return await ProcesarListadoResumenes(filtros);
        }

        private async Task<IActionResult> ProcesarListadoResumenes(
            FiltroResumenTarjetaRequestDTO filtros)
        {
            try
            {
                if (filtros == null)
                {
                    filtros = new FiltroResumenTarjetaRequestDTO();
                }

                string nroTarjetaFiltro = PrimerValor(
                    filtros.NroTarjetaFiltro,
                    filtros.NroTarjeta,
                    filtros.Tarjeta
                );

                string nroDocumentoFiltro = PrimerValor(
                    filtros.NroDocumentoFiltro,
                    filtros.NroDocumento,
                    filtros.Documento,
                    filtros.Dni,
                    filtros.DNI,
                    filtros.Cuit,
                    filtros.CUIT,
                    filtros.Cuil,
                    filtros.CUIL
                );

                string busquedaGeneral = PrimerValor(
                    filtros.Buscar,
                    filtros.Busqueda
                );

                bool sinFiltros =
                    string.IsNullOrWhiteSpace(nroTarjetaFiltro) &&
                    string.IsNullOrWhiteSpace(nroDocumentoFiltro) &&
                    string.IsNullOrWhiteSpace(busquedaGeneral);

                if (sinFiltros)
                {
                    return Ok(new ResumenTarjetaListadoResponseDTO
                    {
                        Status = 200,
                        Mensaje = "",
                        UsuarioId = "",
                        Data = new List<ResumenTarjetaDTO>()
                    });
                }

                IQueryable<Usuario> query = _context.Usuarios
                    .Include(x => x.Personas)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(nroTarjetaFiltro))
                {
                    query = AplicarFiltroTarjeta(query, nroTarjetaFiltro);
                }

                if (!string.IsNullOrWhiteSpace(nroDocumentoFiltro))
                {
                    query = AplicarFiltroDocumento(query, nroDocumentoFiltro);
                }

                if (
                    !string.IsNullOrWhiteSpace(busquedaGeneral) &&
                    string.IsNullOrWhiteSpace(nroTarjetaFiltro) &&
                    string.IsNullOrWhiteSpace(nroDocumentoFiltro)
                )
                {
                    query = AplicarBusquedaGeneral(query, busquedaGeneral);
                }

                Usuario usuario = await query.FirstOrDefaultAsync();

                if (usuario == null)
                {
                    return Ok(new ResumenTarjetaListadoResponseDTO
                    {
                        Status = 404,
                        Mensaje = "No se encontró ningún usuario con los datos proporcionados.",
                        UsuarioId = "",
                        Data = new List<ResumenTarjetaDTO>()
                    });
                }

                DateTime fechaActual = DateTime.Now.AddMonths(1);

                List<ResumenTarjeta> movimientos = await _context.ResumenTarjeta
                    .Include(x => x.Periodo)
                    .Where(x =>
                        x.UsuarioId == usuario.Id &&
                        x.Periodo != null &&
                        x.Periodo.FechaHasta < fechaActual
                    )
                    .ToListAsync();

                List<ResumenTarjetaDTO> resumenes = movimientos
                    .Where(x => x.Periodo != null)
                    .GroupBy(x => new
                    {
                        x.Periodo.Id,
                        x.Periodo.Descripcion,
                        x.Periodo.FechaVencimiento
                    })
                    .OrderByDescending(g => g.Key.FechaVencimiento)
                    .Select(g =>
                    {
                        decimal monto = g.Sum(m => m.Monto);
                        decimal punitorios = g.Sum(m => m.MontoAdeudado);
                        decimal montoTotal = monto + punitorios;

                        int periodoId = g.Key.Id;
                        string usuarioId = usuario.Id;

                        string descargarResumenUrl =
                            $"/reportes/endpoint/resumen-tarjeta-reportes/descargar-resumen?Id={periodoId},{usuarioId}";

                        string descargarArchivoUrl =
                            $"/reportes/endpoint/resumen-tarjeta-reportes/descargar-resumen-archivo?Id={periodoId},{usuarioId}";

                        string verComprobanteUrl =
                            $"/reportes/endpoint/resumen-tarjeta-reportes/ver-comprobante?Id={periodoId},{usuarioId}";

                        return new ResumenTarjetaDTO
                        {
                            Id = periodoId,
                            NroTarjeta = usuario.Personas != null
                                ? usuario.Personas.NroTarjeta ?? ""
                                : "",

                            UsuarioId = usuarioId,
                            PeriodoId = periodoId,
                            Periodo = g.Key.Descripcion ?? "",
                            FechaVencimiento = g.Key.FechaVencimiento.ToString("dd/MM/yyyy"),

                            Monto = monto,
                            Punitorios = punitorios,
                            MontoTotal = montoTotal,

                            MontoTexto = monto.ToString("C2"),
                            PunitoriosTexto = punitorios.ToString("C2"),
                            MontoTotalTexto = montoTotal.ToString("C2"),

                            DescargarResumenUrl = descargarResumenUrl,
                            DescargarResumenArchivoUrl = descargarArchivoUrl,
                            VerComprobanteUrl = verComprobanteUrl,

                            Accion =
                                $"<a href=\"{verComprobanteUrl}\" class=\"btn btn-warning btn-xs\" target=\"_blank\">" +
                                "<i class=\"fa fa-file-pdf-o\"></i></a>"
                        };
                    })
                    .ToList();

                return Ok(new ResumenTarjetaListadoResponseDTO
                {
                    Status = 200,
                    Mensaje = "",
                    UsuarioId = usuario.Id,
                    Data = resumenes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ResumenTarjetaListadoResponseDTO
                {
                    Status = 500,
                    Mensaje = "Se produjo un error al procesar la solicitud: " + ex.Message,
                    UsuarioId = "",
                    Data = new List<ResumenTarjetaDTO>()
                });
            }
        }

        // GET: /reportes/endpoint/resumen-tarjeta-reportes/descargar-resumen?Id=PeriodoId,UsuarioId
        [HttpGet("descargar-resumen")]
        public async Task<IActionResult> DescargarResumen(string Id)
        {
            int periodoIdFinal;
            string usuarioIdFinal;
            string error;

            bool idValido = ResolverIdResumen(
                Id,
                null,
                null,
                out periodoIdFinal,
                out usuarioIdFinal,
                out error
            );

            if (!idValido)
            {
                return BadRequest(new ResumenArchivoDTO
                {
                    Status = 400,
                    Mensaje = error,
                    PeriodoId = 0,
                    UsuarioId = "",
                    Base64 = "",
                    ContentType = "",
                    FileName = ""
                });
            }

            try
            {
                ResumenTarjeta resumen = await _context.ResumenTarjeta
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.UsuarioId == usuarioIdFinal &&
                        x.PeriodoId == periodoIdFinal &&
                        x.Adjunto != null
                    );

                if (resumen == null)
                {
                    return NotFound(new ResumenArchivoDTO
                    {
                        Status = 404,
                        Mensaje = "No se encontró un resumen para el período y usuario especificados.",
                        PeriodoId = periodoIdFinal,
                        UsuarioId = usuarioIdFinal,
                        Base64 = "",
                        ContentType = "",
                        FileName = ""
                    });
                }

                if (resumen.Adjunto == null || resumen.Adjunto.Length == 0)
                {
                    return NotFound(new ResumenArchivoDTO
                    {
                        Status = 404,
                        Mensaje = "El resumen fue encontrado pero no contiene un archivo adjunto.",
                        PeriodoId = periodoIdFinal,
                        UsuarioId = usuarioIdFinal,
                        Base64 = "",
                        ContentType = "",
                        FileName = ""
                    });
                }

                return Ok(new ResumenArchivoDTO
                {
                    Status = 200,
                    Mensaje = "",
                    PeriodoId = periodoIdFinal,
                    UsuarioId = usuarioIdFinal,
                    Base64 = Convert.ToBase64String(resumen.Adjunto),
                    ContentType = "application/pdf",
                    FileName = $"ResumenTarjeta_{periodoIdFinal}_{usuarioIdFinal}.pdf"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ResumenArchivoDTO
                {
                    Status = 500,
                    Mensaje = "Ocurrió un error interno al procesar la solicitud: " + ex.Message,
                    PeriodoId = periodoIdFinal,
                    UsuarioId = usuarioIdFinal,
                    Base64 = "",
                    ContentType = "",
                    FileName = ""
                });
            }
        }

        // GET: /reportes/endpoint/resumen-tarjeta-reportes/ver-comprobante?Id=PeriodoId,UsuarioId
        [HttpGet("ver-comprobante")]
        public async Task<IActionResult> VerComprobante(
            [FromQuery] string Id,
            [FromQuery] int? periodoId,
            [FromQuery] string usuarioId)
        {
            int periodoIdFinal;
            string usuarioIdFinal;
            string error;

            bool idValido = ResolverIdResumen(
                Id,
                periodoId,
                usuarioId,
                out periodoIdFinal,
                out usuarioIdFinal,
                out error
            );

            if (!idValido)
            {
                return BadRequest(error);
            }

            return await DevolverComprobantePdf(
                periodoIdFinal,
                usuarioIdFinal,
                true
            );
        }

        // GET: /reportes/endpoint/resumen-tarjeta-reportes/ver-comprobante/PeriodoId/UsuarioId
        [HttpGet("ver-comprobante/{periodoId:int}/{usuarioId}")]
        public async Task<IActionResult> VerComprobanteRuta(
            int periodoId,
            string usuarioId)
        {
            return await DevolverComprobantePdf(periodoId, usuarioId, true);
        }

        // GET: /reportes/endpoint/resumen-tarjeta-reportes/descargar-resumen-archivo?Id=PeriodoId,UsuarioId
        [HttpGet("descargar-resumen-archivo")]
        public async Task<IActionResult> DescargarResumenArchivo(string Id)
        {
            int periodoIdFinal;
            string usuarioIdFinal;
            string error;

            bool idValido = ResolverIdResumen(
                Id,
                null,
                null,
                out periodoIdFinal,
                out usuarioIdFinal,
                out error
            );

            if (!idValido)
            {
                return BadRequest(error);
            }

            return await DevolverComprobantePdf(
                periodoIdFinal,
                usuarioIdFinal,
                false
            );
        }

        private async Task<IActionResult> DevolverComprobantePdf(
            int periodoId,
            string usuarioId,
            bool inline)
        {
            try
            {
                ResumenTarjeta resumen = await _context.ResumenTarjeta
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.UsuarioId == usuarioId &&
                        x.PeriodoId == periodoId &&
                        x.Adjunto != null
                    );

                if (resumen == null)
                {
                    return NotFound("No se encontró un comprobante para el período y usuario especificados.");
                }

                if (resumen.Adjunto == null || resumen.Adjunto.Length == 0)
                {
                    return NotFound("El comprobante fue encontrado pero no contiene archivo adjunto.");
                }

                string fileName = $"ResumenTarjeta_{periodoId}_{usuarioId}.pdf";

                if (inline)
                {
                    Response.Headers["Content-Disposition"] =
                        $"inline; filename=\"{fileName}\"";

                    return File(resumen.Adjunto, "application/pdf");
                }

                return File(
                    resumen.Adjunto,
                    "application/pdf",
                    fileName
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    "Ocurrió un error interno al obtener el comprobante: " + ex.Message
                );
            }
        }

        private static IQueryable<Usuario> AplicarFiltroTarjeta(
            IQueryable<Usuario> query,
            string nroTarjetaFiltro)
        {
            string tarjetaRaw = nroTarjetaFiltro.Trim();
            string tarjetaNumerica = SoloNumeros(tarjetaRaw);

            if (!string.IsNullOrWhiteSpace(tarjetaNumerica))
            {
                return query.Where(x =>
                    x.Personas != null &&
                    (
                        (x.Personas.NroTarjeta ?? "").Contains(tarjetaRaw) ||

                        (x.Personas.NroTarjeta ?? "")
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("-", "")
                            .Replace("/", "")
                            .Contains(tarjetaNumerica)
                    )
                );
            }

            return query.Where(x =>
                x.Personas != null &&
                (x.Personas.NroTarjeta ?? "").Contains(tarjetaRaw)
            );
        }

        private static IQueryable<Usuario> AplicarFiltroDocumento(
            IQueryable<Usuario> query,
            string nroDocumentoFiltro)
        {
            string documentoRaw = nroDocumentoFiltro.Trim();
            string documentoNumerico = SoloNumeros(documentoRaw);
            string documentoCentral = DocumentoCentralDesdeCuil(documentoRaw);

            if (!string.IsNullOrWhiteSpace(documentoNumerico))
            {
                return query.Where(x =>
                    x.Personas != null &&
                    (
                        (x.Personas.NroDocumento ?? "").Contains(documentoRaw) ||

                        (x.Personas.NroDocumento ?? "")
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("-", "")
                            .Replace("/", "")
                            .Contains(documentoNumerico) ||

                        (x.Personas.NroDocumento ?? "")
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("-", "")
                            .Replace("/", "")
                            .Contains(documentoCentral)
                    )
                );
            }

            return query.Where(x =>
                x.Personas != null &&
                (x.Personas.NroDocumento ?? "").Contains(documentoRaw)
            );
        }

        private static IQueryable<Usuario> AplicarBusquedaGeneral(
            IQueryable<Usuario> query,
            string busquedaGeneral)
        {
            string buscarRaw = busquedaGeneral.Trim();
            string buscarNumerico = SoloNumeros(buscarRaw);
            string buscarDocumentoCentral = DocumentoCentralDesdeCuil(buscarRaw);

            if (!string.IsNullOrWhiteSpace(buscarNumerico))
            {
                return query.Where(x =>
                    x.Personas != null &&
                    (
                        (x.Personas.NroTarjeta ?? "").Contains(buscarRaw) ||
                        (x.Personas.NroDocumento ?? "").Contains(buscarRaw) ||

                        (x.Personas.NroTarjeta ?? "")
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("-", "")
                            .Replace("/", "")
                            .Contains(buscarNumerico) ||

                        (x.Personas.NroDocumento ?? "")
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("-", "")
                            .Replace("/", "")
                            .Contains(buscarNumerico) ||

                        (x.Personas.NroDocumento ?? "")
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("-", "")
                            .Replace("/", "")
                            .Contains(buscarDocumentoCentral)
                    )
                );
            }

            return query.Where(x =>
                x.Personas != null &&
                (
                    (x.Personas.NroTarjeta ?? "").Contains(buscarRaw) ||
                    (x.Personas.NroDocumento ?? "").Contains(buscarRaw)
                )
            );
        }

        private static bool ResolverIdResumen(
            string Id,
            int? periodoId,
            string usuarioId,
            out int periodoIdFinal,
            out string usuarioIdFinal,
            out string error)
        {
            periodoIdFinal = 0;
            usuarioIdFinal = "";
            error = "";

            if (!string.IsNullOrWhiteSpace(Id))
            {
                string[] partes = Id.Split(',');

                if (partes.Length != 2)
                {
                    error = "El formato del ID es incorrecto. Se esperaba 'PeriodoId,UsuarioId'.";
                    return false;
                }

                if (!int.TryParse(partes[0], out periodoIdFinal))
                {
                    error = "El PeriodoId proporcionado no es un número válido.";
                    return false;
                }

                usuarioIdFinal = partes[1];

                if (string.IsNullOrWhiteSpace(usuarioIdFinal))
                {
                    error = "El UsuarioId no puede ser nulo o vacío.";
                    return false;
                }

                return true;
            }

            if (!periodoId.HasValue || periodoId.Value <= 0)
            {
                error = "Debe enviar un PeriodoId válido.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(usuarioId))
            {
                error = "Debe enviar un UsuarioId válido.";
                return false;
            }

            periodoIdFinal = periodoId.Value;
            usuarioIdFinal = usuarioId.Trim();

            return true;
        }

        private static string PrimerValor(params string[] valores)
        {
            if (valores == null)
            {
                return "";
            }

            foreach (string valor in valores)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                {
                    return valor.Trim();
                }
            }

            return "";
        }

        private static string SoloNumeros(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return "";
            }

            return new string(valor.Where(char.IsDigit).ToArray());
        }

        private static string DocumentoCentralDesdeCuil(string valor)
        {
            string soloNumeros = SoloNumeros(valor);

            // Si viene CUIT / CUIL tipo 20-12345678-9,
            // devuelve solamente el DNI del medio.
            if (soloNumeros.Length == 11)
            {
                return soloNumeros.Substring(2, 8);
            }

            return soloNumeros;
        }
    }
}