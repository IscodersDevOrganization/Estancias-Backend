using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using DAL.Models.Core;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EstanciasCore.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/pago-tarjeta-historico")]
    [ApiController]
    public class PagoTarjetaHistoricoEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;

        public PagoTarjetaHistoricoEndpointController(
            EstanciasContext context)
        {
            _context = context;
        }

        // GET: endpoint/pago-tarjeta-historico
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string buscar = null,
            [FromQuery] int? estado = null,
            [FromQuery] DateTime? fecha = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1)
                page = 1;

            if (pageSize < 1)
                pageSize = 10;

            if (pageSize > 100)
                pageSize = 100;

            var today = DateTime.Today.AddDays(-7);

            var query = _context.PagoTarjeta
                .AsNoTracking()
                .Include(p => p.Persona)
                .Where(p => p.FechaComprobante < today)
                .AsQueryable();

            // Filtro barra de búsqueda: cliente o documento
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var texto = buscar.Trim().ToLower();

                query = query.Where(p =>
                    p.Persona != null &&
                    (
                        ((p.Persona.Apellido ?? "") + " " +
                         (p.Persona.Nombres ?? ""))
                            .ToLower()
                            .Contains(texto)

                        ||

                        ((p.Persona.Nombres ?? "") + " " +
                         (p.Persona.Apellido ?? ""))
                            .ToLower()
                            .Contains(texto)

                        ||

                        (p.Persona.NroDocumento ?? "")
                            .ToLower()
                            .Contains(texto)
                    )
                );
            }

            // Filtro por estado
            if (estado.HasValue)
            {
                query = query.Where(p =>
                    (int)p.EstadoPago == estado.Value
                );
            }

            // Filtro por fecha de comprobante
            if (fecha.HasValue)
            {
                var desde = fecha.Value.Date;
                var hasta = desde.AddDays(1);

                query = query.Where(p =>
                    p.FechaComprobante.HasValue &&
                    p.FechaComprobante.Value >= desde &&
                    p.FechaComprobante.Value < hasta
                );
            }

            var total = await query.CountAsync();

            var pagos = await query
                .OrderByDescending(p => p.FechaComprobante)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PagoTarjetaDataTableDTO
                {
                    Id = p.Id,

                    Persona = p.Persona != null
                        ? p.Persona.Apellido + " " +
                          p.Persona.Nombres
                        : "---",

                    NroDocumento = p.Persona != null
                        ? p.Persona.NroDocumento
                        : "---",

                    Usuario = p.Persona != null
                        ? p.Persona.Email
                        : "---",

                    NroTarjeta = p.Persona != null
                        ? p.Persona.NroTarjeta
                        : "---",

                    MontoAdeudado = p.MontoAdeudado,

                    MontoInformado = p.MontoInformado,

                    FechaVencimiento = p.FechaVencimiento.HasValue
                        ? p.FechaVencimiento.Value
                            .ToString("dd/MM/yyyy")
                        : "",

                    FechaComprobante = p.FechaComprobante.HasValue
                        ? p.FechaComprobante.Value
                            .ToString("dd/MM/yyyy")
                        : "",

                    FechaDePago = p.FechaDePago.HasValue
                        ? p.FechaDePago.Value
                            .ToString("dd/MM/yyyy")
                        : "",

                    FechaPagoProximaCuota =
                        p.FechaPagoProximaCuota.HasValue
                            ? p.FechaPagoProximaCuota.Value
                                .ToString("dd/MM/yyyy")
                            : "",

                    EstadoPago = p.EstadoPago.ToString(),

                    EstadoPagoId = (int)p.EstadoPago,

                    // Importante: NO traer el byte[]
                    ComprobantePago =
                        p.ComprobantePago != null,

                    FechaOrden = p.FechaComprobante.HasValue
                        ? Convert.ToInt32(
                            p.FechaComprobante.Value
                                .ToString("yyyyMMdd"))
                        : 0,

                    Observacion = p.Observacion ?? ""
                })
                .ToListAsync();

            return Ok(new
            {
                ok = true,
                total,
                page,
                pageSize,
                totalPages =
                    (int)Math.Ceiling(
                        total / (double)pageSize),
                data = pagos
            });
        }

        // GET: endpoint/pago-tarjeta-historico/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var pago = await _context.PagoTarjeta
                .AsNoTracking()
                .Include(p => p.Persona)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pago == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró el pago."
                });
            }

            var dto = new PagoTarjetaDTO
            {
                Id = pago.Id,

                Persona = pago.Persona != null
                    ? pago.Persona.Apellido + " " +
                      pago.Persona.Nombres
                    : "---",

                NroDocumento = pago.Persona != null
                    ? pago.Persona.NroDocumento
                    : "---",

                Usuario = pago.Persona != null
                    ? pago.Persona.Email
                    : "---",

                NroTarjeta = pago.Persona != null
                    ? pago.Persona.NroTarjeta
                    : "---",

                FechaVencimiento =
                    pago.FechaVencimiento ??
                    DateTime.MinValue,

                FechaPagoProximaCuota =
                    pago.FechaPagoProximaCuota ??
                    DateTime.MinValue,

                MontoAdeudado = pago.MontoAdeudado,

                EstadoPago = pago.EstadoPago,

                // Acá sí se puede devolver porque es detalle.
                ComprobantePago = pago.ComprobantePago
            };

            return Ok(new
            {
                ok = true,
                data = dto
            });
        }

        // GET: endpoint/pago-tarjeta-historico/{id}/comprobante
        [HttpGet("{id}/comprobante")]
        public async Task<IActionResult> VerComprobante(
            int id)
        {
            var pagoTarjeta = await _context.PagoTarjeta
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pagoTarjeta == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró el pago."
                });
            }

            if (pagoTarjeta.ComprobantePago == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message =
                        "El pago no tiene comprobante cargado."
                });
            }

            var comprobante = Convert.ToBase64String(
                pagoTarjeta.ComprobantePago
            );

            return Ok(new
            {
                ok = true,
                data = comprobante
            });
        }

        // PUT: endpoint/pago-tarjeta-historico/{id}/aprobar
        [HttpPut("{id}/aprobar")]
        public async Task<IActionResult> AprobarComprobante(
            int id)
        {
            try
            {
                var pagoTarjeta =
                    await _context.PagoTarjeta
                        .Include(p => p.Persona)
                        .FirstOrDefaultAsync(
                            p => p.Id == id
                        );

                if (pagoTarjeta == null)
                {
                    return NotFound(new
                    {
                        ok = false,
                        message =
                            "No se encontró el pago."
                    });
                }

                pagoTarjeta.EstadoPago =
                    EstadoPago.Aprobado;

                _context.PagoTarjeta.Update(
                    pagoTarjeta
                );

                await _context.SaveChangesAsync();

                var cliente = await _context.Clientes
                    .Include(c => c.Persona)
                    .FirstOrDefaultAsync(
                        x =>
                            x.Persona.Id ==
                            pagoTarjeta.Persona.Id
                    );

                if (cliente != null)
                {
                    var notificacion =
                        new NotificacionesPersonas
                        {
                            Cliente = cliente,
                            Titulo = "Pago Aprobado",
                            Descripcion =
                                "Se aprobo su comprobante de Pago",
                            FechaHora = DateTime.Now,
                            TomaConocimiento = null
                        };

                    _context.NotificacionesPersonas.Add(
                        notificacion
                    );

                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    ok = true,
                    message =
                        "El pago fue aprobado correctamente."
                });
            }
            catch
            {
                return BadRequest(new
                {
                    ok = false,
                    message =
                        "Hubo un error al aprobar el pago."
                });
            }
        }

        // PUT: endpoint/pago-tarjeta-historico/rechazar
        [HttpPut("rechazar")]
        public async Task<IActionResult> RechazarComprobante(
            [FromBody] RechazarComprobanteDTO dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "Datos inválidos."
                    });
                }

                var pagoTarjeta =
                    await _context.PagoTarjeta
                        .Include(p => p.Persona)
                        .FirstOrDefaultAsync(
                            p => p.Id == dto.Id
                        );

                if (pagoTarjeta == null)
                {
                    return NotFound(new
                    {
                        ok = false,
                        message =
                            "No se encontró el pago."
                    });
                }

                pagoTarjeta.EstadoPago =
                    EstadoPago.Rechazado;

                pagoTarjeta.Observacion =
                    dto.Observacion;

                _context.PagoTarjeta.Update(
                    pagoTarjeta
                );

                await _context.SaveChangesAsync();

                var cliente = await _context.Clientes
                    .Include(c => c.Persona)
                    .FirstOrDefaultAsync(
                        x =>
                            x.Persona.Id ==
                            pagoTarjeta.Persona.Id
                    );

                if (cliente != null)
                {
                    var notificacion =
                        new NotificacionesPersonas
                        {
                            Cliente = cliente,
                            Titulo = "Pago Rechazado",
                            Descripcion =
                                "Se rechazo su comprobante de Pago",
                            FechaHora = DateTime.Now,
                            TomaConocimiento = null
                        };

                    _context.NotificacionesPersonas.Add(
                        notificacion
                    );

                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    ok = true,
                    message =
                        "El pago fue rechazado correctamente."
                });
            }
            catch
            {
                return BadRequest(new
                {
                    ok = false,
                    message =
                        "Hubo un error al rechazar el pago."
                });
            }
        }

        // POST: endpoint/pago-tarjeta-historico/aprobar-masivo
        [HttpPost("aprobar-masivo")]
        public async Task<IActionResult> AprobarMasivo(
            [FromBody] List<int> ids)
        {
            try
            {
                if (ids == null || !ids.Any())
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message =
                            "Debe enviar al menos un pago para aprobar."
                    });
                }

                var pagosAprobar =
                    await _context.PagoTarjeta
                        .Include(p => p.Persona)
                        .Where(s => ids.Contains(s.Id))
                        .ToListAsync();

                foreach (
                    var pagoTarjeta in pagosAprobar)
                {
                    pagoTarjeta.EstadoPago =
                        EstadoPago.Aprobado;

                    _context.PagoTarjeta.Update(
                        pagoTarjeta
                    );
                }

                await _context.SaveChangesAsync();

                foreach (
                    var pagoTarjeta in pagosAprobar)
                {
                    if (pagoTarjeta.Persona == null)
                        continue;

                    var cliente =
                        await _context.Clientes
                            .Include(c => c.Persona)
                            .FirstOrDefaultAsync(
                                x =>
                                    x.Persona.Id ==
                                    pagoTarjeta.Persona.Id
                            );

                    if (cliente != null)
                    {
                        var notificacion =
                            new NotificacionesPersonas
                            {
                                Cliente = cliente,
                                Titulo =
                                    "Pago Aprobado",
                                Descripcion =
                                    "Se aprobó su comprobante de Pago",
                                FechaHora =
                                    DateTime.Now,
                                TomaConocimiento =
                                    null
                            };

                        _context
                            .NotificacionesPersonas
                            .Add(notificacion);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    ok = true,
                    message =
                        "Los pagos fueron aprobados."
                });
            }
            catch
            {
                return BadRequest(new
                {
                    ok = false,
                    message =
                        "Error al aprobar los pagos."
                });
            }
        }

        // POST: endpoint/pago-tarjeta-historico/rechazar-masivo
        [HttpPost("rechazar-masivo")]
        public async Task<IActionResult> RechazarMasivo(
            [FromBody] RechazarComprobanteMasivoDTO dto)
        {
            try
            {
                if (dto == null ||
                    dto.Ids == null ||
                    !dto.Ids.Any())
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message =
                            "Debe enviar al menos un pago para rechazar."
                    });
                }

                var pagosRechazar =
                    await _context.PagoTarjeta
                        .Include(p => p.Persona)
                        .Where(
                            s => dto.Ids.Contains(s.Id)
                        )
                        .ToListAsync();

                foreach (
                    var pagoTarjeta in pagosRechazar)
                {
                    pagoTarjeta.EstadoPago =
                        EstadoPago.Rechazado;

                    pagoTarjeta.Observacion =
                        dto.Observacion;

                    _context.PagoTarjeta.Update(
                        pagoTarjeta
                    );
                }

                await _context.SaveChangesAsync();

                foreach (
                    var pagoTarjeta in pagosRechazar)
                {
                    if (pagoTarjeta.Persona == null)
                        continue;

                    var cliente =
                        await _context.Clientes
                            .Include(c => c.Persona)
                            .FirstOrDefaultAsync(
                                x =>
                                    x.Persona.Id ==
                                    pagoTarjeta.Persona.Id
                            );

                    if (cliente != null)
                    {
                        var notificacion =
                            new NotificacionesPersonas
                            {
                                Cliente = cliente,
                                Titulo =
                                    "Pago Rechazado",
                                Descripcion =
                                    "Se rechazó su comprobante de Pago. Motivo: " +
                                    dto.Observacion,
                                FechaHora =
                                    DateTime.Now,
                                TomaConocimiento =
                                    null
                            };

                        _context
                            .NotificacionesPersonas
                            .Add(notificacion);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    ok = true,
                    message =
                        "Los pagos han sido rechazados."
                });
            }
            catch
            {
                return BadRequest(new
                {
                    ok = false,
                    message =
                        "Hubo un error al rechazar los pagos."
                });
            }
        }

        // POST: endpoint/pago-tarjeta-historico/exportar-excel
        [HttpPost("exportar-excel")]
        public async Task<IActionResult> ExportarExcel(
            [FromBody] List<int> ids,
            [FromQuery] bool exportarTodos = false,
            [FromQuery] string buscar = null,
            [FromQuery] int? estado = null,
            [FromQuery] DateTime? fecha = null,
            CancellationToken cancellationToken = default(
                CancellationToken))
        {
            try
            {
                var listaIds = ids == null
                    ? new List<int>()
                    : ids
                        .Where(x => x > 0)
                        .Distinct()
                        .ToList();

                if (!exportarTodos &&
                    !listaIds.Any())
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message =
                            "Debe enviar al menos un ID o indicar exportarTodos=true."
                    });
                }

                var datos =
                    new List<
                        PagoTarjetaHistoricoExcelFila
                    >();

                if (exportarTodos)
                {
                    /*
                     * Se aplica la misma condición
                     * que utiliza el listado histórico.
                     */
                    var fechaLimiteHistorico =
                        DateTime.Today.AddDays(-7);

                    IQueryable<PagoTarjeta> query =
                        _context.PagoTarjeta
                            .AsNoTracking()
                            .Where(
                                p =>
                                    p.FechaComprobante <
                                    fechaLimiteHistorico
                            );

                    /*
                     * Mismos filtros que utiliza
                     * el listado histórico.
                     */
                    if (!string.IsNullOrWhiteSpace(
                            buscar))
                    {
                        var texto = buscar
                            .Trim()
                            .ToLower();

                        query = query.Where(p =>
                            p.Persona != null &&
                            (
                                (
                                    (p.Persona.Apellido ??
                                     "") +
                                    " " +
                                    (p.Persona.Nombres ??
                                     "")
                                )
                                .ToLower()
                                .Contains(texto)

                                ||

                                (
                                    (p.Persona.Nombres ??
                                     "") +
                                    " " +
                                    (p.Persona.Apellido ??
                                     "")
                                )
                                .ToLower()
                                .Contains(texto)

                                ||

                                (p.Persona
                                     .NroDocumento ??
                                 "")
                                .ToLower()
                                .Contains(texto)
                            )
                        );
                    }

                    if (estado.HasValue)
                    {
                        query = query.Where(p =>
                            (int)p.EstadoPago ==
                            estado.Value
                        );
                    }

                    if (fecha.HasValue)
                    {
                        var desde =
                            fecha.Value.Date;

                        var hasta =
                            desde.AddDays(1);

                        query = query.Where(p =>
                            p.FechaComprobante
                                .HasValue &&
                            p.FechaComprobante.Value >=
                            desde &&
                            p.FechaComprobante.Value <
                            hasta
                        );
                    }

                    /*
                     * Solamente se consultan las
                     * columnas necesarias.
                     *
                     * No se trae ComprobantePago.
                     */
                    datos =
                        await ProyectarDatosExcel(
                                query
                            )
                            .OrderBy(
                                x =>
                                    x.FechaComprobante
                            )
                            .ToListAsync(
                                cancellationToken
                            );
                }
                else
                {
                    /*
                     * Los IDs se procesan en lotes
                     * para evitar una consulta SQL
                     * con miles de parámetros.
                     */
                    const int tamanoLote = 1000;

                    var queryBase =
                        _context.PagoTarjeta
                            .AsNoTracking();

                    for (
                        var posicion = 0;
                        posicion < listaIds.Count;
                        posicion += tamanoLote)
                    {
                        cancellationToken
                            .ThrowIfCancellationRequested();

                        var loteIds = listaIds
                            .Skip(posicion)
                            .Take(tamanoLote)
                            .ToList();

                        var datosLote =
                            await ProyectarDatosExcel(
                                    queryBase.Where(
                                        p =>
                                            loteIds.Contains(
                                                p.Id
                                            )
                                    )
                                )
                                .ToListAsync(
                                    cancellationToken
                                );

                        datos.AddRange(datosLote);
                    }

                    datos = datos
                        .OrderBy(
                            x => x.FechaComprobante
                        )
                        .ToList();
                }

                if (!datos.Any())
                {
                    return NotFound(new
                    {
                        ok = false,
                        message =
                            "No se encontraron pagos para exportar."
                    });
                }

                var excelStream =
                    GenerarExcelStream(datos);

                var excelName =
                    $"Comprobantes_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                Response.Headers[
                    "X-Total-Registros"
                ] = datos.Count.ToString();

                return File(
                    excelStream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    excelName
                );
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new
                {
                    ok = false,
                    message =
                        "La generación del archivo fue cancelada."
                });
            }
            catch (Exception e)
            {
                return BadRequest(new
                {
                    ok = false,
                    message =
                        "Hubo un error al generar el archivo Excel: " +
                        e.Message
                });
            }
        }

        private IQueryable<
            PagoTarjetaHistoricoExcelFila
        > ProyectarDatosExcel(
            IQueryable<PagoTarjeta> query)
        {
            /*
             * No se utiliza Include.
             *
             * Entity Framework genera el JOIN
             * necesario con Persona, pero solo
             * selecciona estas columnas.
             *
             * ComprobantePago no se descarga.
             */
            return query.Select(p =>
                new PagoTarjetaHistoricoExcelFila
                {
                    Cliente = p.Persona != null
                        ? (p.Persona.Apellido ?? "") +
                          ", " +
                          (p.Persona.Nombres ?? "")
                        : "",

                    NroDocumento =
                        p.Persona != null
                            ? p.Persona.NroDocumento ??
                              ""
                            : "",

                    FechaInformada =
                        p.FechaDePago,

                    FechaComprobante =
                        p.FechaComprobante,

                    MontoInformado =
                        p.MontoInformado,

                    EstadoPagoId =
                        (int)p.EstadoPago
                });
        }

        private MemoryStream GenerarExcelStream(
            List<PagoTarjetaHistoricoExcelFila>
                datos)
        {
            var stream = new MemoryStream();

            using (var package =
                   new ExcelPackage())
            {
                var worksheet =
                    package.Workbook.Worksheets.Add(
                        "Pagos"
                    );

                // Encabezados
                worksheet.Cells[1, 1].Value =
                    "Cliente";

                worksheet.Cells[1, 2].Value =
                    "NroDocumento";

                worksheet.Cells[1, 3].Value =
                    "Fecha Informada";

                worksheet.Cells[1, 4].Value =
                    "Fecha de Carga";

                worksheet.Cells[1, 5].Value =
                    "Monto Informado";

                worksheet.Cells[1, 6].Value =
                    "Estado";

                /*
                 * Se asigna una matriz completa
                 * al rango.
                 *
                 * Es mucho más rápido que escribir
                 * las celdas una por una.
                 */
                var valores =
                    new object[datos.Count, 6];

                for (var i = 0;
                     i < datos.Count;
                     i++)
                {
                    var fila = datos[i];

                    valores[i, 0] =
                        fila.Cliente;

                    valores[i, 1] =
                        fila.NroDocumento;

                    valores[i, 2] =
                        fila.FechaInformada.HasValue
                            ? (object)fila
                                .FechaInformada.Value
                            : null;

                    valores[i, 3] =
                        fila.FechaComprobante
                            .HasValue
                            ? (object)fila
                                .FechaComprobante.Value
                            : null;

                    valores[i, 4] =
                        fila.MontoInformado;

                    valores[i, 5] =
                        ((EstadoPago)
                            fila.EstadoPagoId)
                        .ToString();
                }

                worksheet.Cells[
                    2,
                    1,
                    datos.Count + 1,
                    6
                ].Value = valores;

                // Encabezados en negrita
                worksheet.Cells[
                    1,
                    1,
                    1,
                    6
                ].Style.Font.Bold = true;

                // Documento como texto
                worksheet.Cells[
                    2,
                    2,
                    datos.Count + 1,
                    2
                ].Style.Numberformat.Format = "@";

                // Fecha informada
                worksheet.Cells[
                    2,
                    3,
                    datos.Count + 1,
                    3
                ].Style.Numberformat.Format =
                    "dd/MM/yyyy";

                // Fecha de carga
                worksheet.Cells[
                    2,
                    4,
                    datos.Count + 1,
                    4
                ].Style.Numberformat.Format =
                    "dd/MM/yyyy HH:mm";

                // Monto informado
                worksheet.Cells[
                    2,
                    5,
                    datos.Count + 1,
                    5
                ].Style.Numberformat.Format =
                    "$ #,##0.00";

                /*
                 * Anchos fijos.
                 *
                 * No se usa AutoFitColumns porque
                 * recorre todas las filas y demora
                 * mucho en exportaciones grandes.
                 */
                worksheet.Column(1).Width = 35;
                worksheet.Column(2).Width = 18;
                worksheet.Column(3).Width = 18;
                worksheet.Column(4).Width = 21;
                worksheet.Column(5).Width = 20;
                worksheet.Column(6).Width = 18;

                // Congelar encabezados
                worksheet.View.FreezePanes(2, 1);

                // Agregar filtros
                worksheet.Cells[
                    1,
                    1,
                    datos.Count + 1,
                    6
                ].AutoFilter = true;

                /*
                 * Se guarda directamente en el
                 * MemoryStream.
                 *
                 * No se usa GetAsByteArray para
                 * evitar otra copia del archivo.
                 */
                package.SaveAs(stream);
            }

            stream.Position = 0;

            return stream;
        }

        /*
         * Clase utilizada exclusivamente
         * para la exportación a Excel.
         */
        private class PagoTarjetaHistoricoExcelFila
        {
            public string Cliente { get; set; }

            public string NroDocumento { get; set; }

            public DateTime? FechaInformada
            {
                get;
                set;
            }

            public DateTime? FechaComprobante
            {
                get;
                set;
            }

            public decimal MontoInformado
            {
                get;
                set;
            }

            public int EstadoPagoId
            {
                get;
                set;
            }
        }
    }
}