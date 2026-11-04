using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.API.Filters;
using EstanciasCore.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

namespace EstanciasCore.Areas.Core.Controllers
{
    // Agregar junto al controlador MVC y aplicar los mismos permisos del módulo.
    // Si se autentica con cookies, mantener la protección antiforgery del proyecto.
    [Route("endpoint/solicitudes-tarjeta")]
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    public class SolicitudDeTarjetaEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;
        private readonly IMailService _mailService;

        public SolicitudDeTarjetaEndpointController(EstanciasContext context, IMailService mailService)
        {
            _context = context;
            _mailService = mailService;
        }

        // GET /endpoint/solicitudes-tarjeta?estadoId=1
        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] int? estadoId = null)
        {
            var query = Consulta().AsNoTracking();
            if (estadoId.HasValue)
                query = query.Where(s => s.Estado != null && s.Estado.Id == estadoId.Value);

            var solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();
            return Ok(solicitudes.Select(Respuesta).ToList());
        }

        // GET /endpoint/solicitudes-tarjeta/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obtener(int id)
        {
            var entity = await Consulta().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (entity == null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            return Ok(Respuesta(entity));
        }

        // POST /endpoint/solicitudes-tarjeta
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] SolicitudDeTarjetaDTO model)
        {
            if (model == null)
                return BadRequest(new { mensaje = "Enviá los datos de la solicitud." });

            ModelState.Remove("Id");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var estado = await _context.EstadoSolicitudDeTarjeta.FindAsync(1);
            if (estado == null)
                return Conflict(new { mensaje = "No está configurado el estado inicial (Id 1)." });

            if (!await UbicacionValida(model)) return BadRequest(ModelState);

            var entity = new SolicitudDeTarjeta();
            await CopiarDatos(entity, model);
            entity.FechaSolicitud = DateTime.Now;
            entity.Estado = estado;

            _context.SolicitudDeTarjeta.Add(entity);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Obtener), new { id = entity.Id }, Respuesta(entity));
        }

        // PUT /endpoint/solicitudes-tarjeta/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Editar(int id, [FromBody] SolicitudDeTarjetaDTO model)
        {
            if (model == null)
                return BadRequest(new { mensaje = "Enviá los datos de la solicitud." });

            model.Id = id;
            ModelState.Remove("Id");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await Consulta().FirstOrDefaultAsync(s => s.Id == id);
            if (entity == null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            if (!await UbicacionValida(model)) return BadRequest(ModelState);
            await CopiarDatos(entity, model);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.SolicitudDeTarjeta.AnyAsync(s => s.Id == id))
                    return NotFound(new { mensaje = "La solicitud ya no existe." });
                throw;
            }

            return Ok(Respuesta(entity));
        }

        // POST /endpoint/solicitudes-tarjeta/5/aprobar
        [HttpPost("{id:int}/aprobar")]
        public async Task<IActionResult> Aprobar(int id, [FromBody] AprobarSolicitudDTO model)
        {
            if (model == null)
                return BadRequest(new { mensaje = "Enviá el número de tarjeta." });

            model.Id = id;
            ModelState.Remove("Id");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await _context.SolicitudDeTarjeta.FindAsync(id);
            if (entity == null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            entity.NumeroTarjeta = model.NumeroTarjeta;
            return await CambiarEstado(entity, 2);
        }

        // POST /endpoint/solicitudes-tarjeta/5/rechazar
        [HttpPost("{id:int}/rechazar")]
        public async Task<IActionResult> Rechazar(int id)
        {
            var entity = await _context.SolicitudDeTarjeta.FindAsync(id);
            if (entity == null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            return await CambiarEstado(entity, 3);
        }

        // POST /endpoint/solicitudes-tarjeta/5/nueva-revision
        [HttpPost("{id:int}/nueva-revision")]
        public async Task<IActionResult> NuevaRevision(int id)
        {
            var entity = await _context.SolicitudDeTarjeta.FindAsync(id);
            if (entity == null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            return await CambiarEstado(entity, 4);
        }

        // DELETE /endpoint/solicitudes-tarjeta/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var entity = await _context.SolicitudDeTarjeta.FindAsync(id);
            if (entity == null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            _context.SolicitudDeTarjeta.Remove(entity);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET /endpoint/solicitudes-tarjeta/5/adjuntos
        [HttpGet("{id:int}/adjuntos")]
        public async Task<IActionResult> Adjuntos(int id)
        {
            var entity = await Consulta().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (entity == null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            var direccion = $"{entity.Calle} {entity.Altura} {entity.PisoDepto}".Trim();
            var localidad = entity.Localidad?.Descripcion;
            var provincia = entity.Provincia == null ? null :
                (string.IsNullOrEmpty(entity.Provincia.DescripcionCompleta)
                    ? entity.Provincia.Descripcion : entity.Provincia.DescripcionCompleta);
            var partes = new[] { direccion, localidad, provincia }
                .Where(p => !string.IsNullOrWhiteSpace(p));

            return Ok(new
            {
                entity.Id,
                NombreCompleto = $"{entity.Nombre} {entity.Apellido}",
                entity.DNI,
                entity.FechaNacimiento,
                Domicilio = string.Join(", ", partes),
                entity.CodigoPostal,
                FrenteDNIBase64 = Base64(entity.FrenteDNI),
                DorsoDNIBase64 = Base64(entity.DorsoDNI),
                SelfieBase64 = Base64(entity.Selfie)
            });
        }

        // GET /endpoint/solicitudes-tarjeta/pendientes/cantidad
        [HttpGet("pendientes/cantidad")]
        public async Task<IActionResult> CantidadPendientes()
        {
            var query = _context.SolicitudDeTarjeta.AsNoTracking()
                .Where(s => s.Estado == null || s.Estado.Id == 1 || s.Estado.Id == 4);

            var cantidad = await query.CountAsync();
            var solicitudes = await query.OrderByDescending(s => s.FechaSolicitud)
                .Take(5)
                .Select(s => new { s.Id, s.Nombre, s.Apellido, s.FechaSolicitud })
                .ToListAsync();

            var ultimas = solicitudes.Select(s => new
            {
                s.Id,
                NombreCompleto = $"{s.Nombre} {s.Apellido}",
                Fecha = s.FechaSolicitud.ToString("dd/MM/yyyy HH:mm")
            }).ToList();

            return Ok(new { pendientes = cantidad, ultimas });
        }

        private IQueryable<SolicitudDeTarjeta> Consulta()
        {
            return _context.SolicitudDeTarjeta
                .Include(s => s.Localidad)
                .Include(s => s.Provincia)
                .Include(s => s.Estado);
        }

        // Respuesta explícita para evitar ciclos de navegación y adjuntos en el listado.
        private static object Respuesta(SolicitudDeTarjeta s)
        {
            return new
            {
                s.Id,
                s.Nombre,
                s.Apellido,
                s.DNI,
                s.Telefono,
                s.Email,
                s.FechaNacimiento,
                s.Calle,
                s.Altura,
                s.PisoDepto,
                s.CodigoPostal,
                Localidad = s.Localidad == null ? null : new
                {
                    s.Localidad.Id,
                    s.Localidad.Descripcion
                },
                Provincia = s.Provincia == null ? null : new
                {
                    s.Provincia.Id,
                    s.Provincia.Descripcion,
                    s.Provincia.DescripcionCompleta
                },
                Estado = s.Estado == null ? null : new { s.Estado.Id, s.Estado.Nombre },
                s.FechaSolicitud,
                s.FechaDeRechazoAprobacion,
                s.NumeroTarjeta
            };
        }

        private async Task<bool> UbicacionValida(SolicitudDeTarjetaDTO model)
        {
            if (model.Localidad != null && model.Localidad.Id > 0 &&
                !await _context.Localidad.AnyAsync(l => l.Id == model.Localidad.Id))
                ModelState.AddModelError("Localidad", "La localidad indicada no existe.");

            if (model.Provincia != null && model.Provincia.Id > 0 &&
                !await _context.Provincia.AnyAsync(p => p.Id == model.Provincia.Id))
                ModelState.AddModelError("Provincia", "La provincia indicada no existe.");

            return ModelState.IsValid;
        }

        private async Task CopiarDatos(SolicitudDeTarjeta entity, SolicitudDeTarjetaDTO model)
        {
            entity.Nombre = model.Nombre;
            entity.Apellido = model.Apellido;
            entity.DNI = model.DNI;
            entity.Telefono = model.Telefono;
            entity.Email = model.Email;
            entity.FechaNacimiento = model.FechaNacimiento;
            entity.Calle = model.Calle;
            entity.Altura = model.Altura;
            entity.PisoDepto = model.PisoDepto;
            entity.CodigoPostal = model.CodigoPostal;
            entity.Localidad = model.Localidad != null && model.Localidad.Id > 0
                ? await _context.Localidad.FindAsync(model.Localidad.Id) : null;
            entity.Provincia = model.Provincia != null && model.Provincia.Id > 0
                ? await _context.Provincia.FindAsync(model.Provincia.Id) : null;
        }

        private static string Base64(byte[] archivo)
        {
            return archivo != null && archivo.Length > 0 ? Convert.ToBase64String(archivo) : null;
        }

        private async Task<IActionResult> CambiarEstado(SolicitudDeTarjeta entity, int estadoId)
        {
            var estado = await _context.EstadoSolicitudDeTarjeta.FindAsync(estadoId);
            if (estado == null && estadoId == 4)
                estado = await _context.EstadoSolicitudDeTarjeta
                    .FirstOrDefaultAsync(e => e.Nombre.Contains("Revisión"));

            // Los estados son datos del catálogo: no insertar un Id explícito
            // que podría estar generado automáticamente por la base de datos.
            if (estado == null)
                return Conflict(new { mensaje = "No está configurado el estado solicitado.", estadoId });

            entity.Estado = estado;
            entity.FechaDeRechazoAprobacion = DateTime.Now;
            await _context.SaveChangesAsync();

            // Igual que el MVC: el cambio se guarda aunque falle el correo.
            try
            {
                await EnviarNotificacion(entity, estadoId);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al notificar la solicitud de tarjeta {SolicitudId}", entity.Id);
                return Ok(new
                {
                    success = true,
                    id = entity.Id,
                    estadoId = estado.Id,
                    numeroTarjeta = entity.NumeroTarjeta,
                    correoEnviado = false,
                    warning = "El estado se guardó, pero no se pudo enviar el correo."
                });
            }

            return Ok(new
            {
                success = true,
                id = entity.Id,
                estadoId = estado.Id,
                numeroTarjeta = entity.NumeroTarjeta,
                correoEnviado = true,
                mensaje = "Estado actualizado y notificación enviada."
            });
        }

        private async Task EnviarNotificacion(SolicitudDeTarjeta entity, int estadoId)
        {
            if (string.IsNullOrWhiteSpace(entity.Email))
                throw new InvalidOperationException("La solicitud no tiene email.");

            string htmlBody;
            string titulo;
            if (estadoId == 2)
            {
                titulo = "Solicitud de Tarjeta Aprobada - Estancias";
                htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px;'>
                            <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='width: 100%; margin-bottom: 25px;'>
                                <tr>
                                    <td align='center' style='padding: 5px;'>
                                        <img src='https://portalestancias.com.ar/images/logo-email.png' alt='Textil' style='width: 100%;height: auto; display: block;' />
                                    </td>                     
                                </tr>
                            </table>

                            <h2>¡Tu solicitud de tarjeta ha sido aprobada!</h2>
                            <p>Estimado/a <strong>{System.Net.WebUtility.HtmlEncode(Convert.ToString(entity.Nombre))} {System.Net.WebUtility.HtmlEncode(Convert.ToString(entity.Apellido))}</strong>,</p>
                            <p>Queríamos avisarte que tu solicitud de la tarjeta Textil del Campo fue <strong>APROBADA</strong> exitosamente</p>
                            <p style='font-size: 16px; background-color: #f4f4f4; padding: 15px; border-left: 4px solid #28a745;'>
                                <strong>Número de tarjeta asignado:</strong> {System.Net.WebUtility.HtmlEncode(Convert.ToString(entity.NumeroTarjeta))}
                            </p>
                            <p>¡Gracias por formar parte de esta comunidad! Te invitamos a registrarte en la app y disfrutar de todos los beneficios exclusivos!</p>
                            <p>Saludos,<br>
                            <strong>Equipo Textil del Campo</strong></p>
                            <hr>
                            <small>Este es un correo automático, por favor no responder a este mensaje.</small>
                        </div>";
            }
            else if (estadoId == 3)
            {
                titulo = "Información sobre tu Solicitud de Tarjeta - Estancias";
                htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px;'>
                            <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='width: 100%; margin-bottom: 25px;'>
                                <tr>
                                    <td align='center' style='padding: 5px;'>
                                        <img src='https://portalestancias.com.ar/images/logo-email.png' alt='Textil' style='width: 100%;height: auto; display: block;' />
                                    </td>    
                                </tr>
                            </table>

                            <h2>Información sobre tu Solicitud de Tarjeta</h2>
                            <p>Estimado/a <strong>{System.Net.WebUtility.HtmlEncode(Convert.ToString(entity.Nombre))} {System.Net.WebUtility.HtmlEncode(Convert.ToString(entity.Apellido))}</strong>,</p>
                            <p>Lamentablemente no cumplís con los requisitos mínimos para dar el alta.</p>
                            <p>Te recomendamos intentarlo en otro momento.</p>
                            <p>Cualquier duda, podés consultar con tu sucursal más cercana.</p>
                            <p>Saludos,<br>
                            <strong>Equipo Textil del Campo</strong></p>
                            <hr>
                            <small>Este es un correo automático, por favor no responder a este mensaje.</small>
                        </div>";
            }
            else
            {
                titulo = "Información sobre tu Solicitud de Tarjeta - Estancias";
                htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px;'>
                            <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='width: 100%; margin-bottom: 25px;'>
                                <tr>
                                    <td align='center' style='padding: 5px;'>
                                        <img src='https://portalestancias.com.ar/images/logo-email.png' alt='Textil' style='width: 100%;height: auto; display: block;' />
                                    </td>    
                                </tr>
                            </table>

                            <h2>Información sobre tu Solicitud de Tarjeta</h2>
                            <p>Estimado/a <strong>{System.Net.WebUtility.HtmlEncode(Convert.ToString(entity.Nombre))} {System.Net.WebUtility.HtmlEncode(Convert.ToString(entity.Apellido))}</strong>,</p>
                            <p>No pudimos validar tu solicitud de manera online. Te pedimos por favor nos vuelvas a enviar la siguiente documentación a tarjetas@cpecreditos.com.ar:</p>
                            <ul style='line-height: 1.8; margin-bottom: 15px;'>
                                <li>Nombre y Apellido:</li>
                                <li>Tipo y Número de Documento:</li>
                                <li>Domicilio completo (Calle, Numeración, Localidad, Provincia, País y Código Postal):</li>
                                <li>Email:</li>
                                <li>Número de teléfono:</li>
                                <li>Fecha de Nacimiento:</li>
                                <li>Foto DNI Frente y Dorso y Selfie sosteniendo el documento:</li>
                                <li>Algún servicio a tu nombre:</li>
                            </ul>
                            <p>Cualquier duda, podés consultar con tu sucursal más cercana.</p>
                            <p>Saludos,<br>
                            <strong>Equipo Textil del Campo</strong></p>
                            <hr>
                            <small>Este es un correo automático, por favor no responder a este mensaje.</small>
                        </div>";
            }

            await _mailService.EnviarAsync(new MailAPI
            {
                Mail = entity.Email.Trim(),
                Titulo = titulo,
                Html = htmlBody
            });
        }
    }
}
