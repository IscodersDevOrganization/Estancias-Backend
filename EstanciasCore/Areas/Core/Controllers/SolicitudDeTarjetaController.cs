using System;
using System.Linq;
using System.Threading.Tasks;
using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static EstanciasCore.Services.common;

namespace EstanciasCore.Areas.Core.Controllers
{
    [Area("Core")]
    public class SolicitudDeTarjetaController : Controller
    {
        private readonly EstanciasContext _context;
        private readonly IMailService _mailService;

        public SolicitudDeTarjetaController(EstanciasContext context, IMailService mailService)
        {
            _context = context;
            _mailService = mailService;
        }

        private void CargarViewBags(int? provinciaId = null)
        {
            var provincias = _context.Provincia.OrderBy(p => string.IsNullOrEmpty(p.DescripcionCompleta) ? p.Descripcion : p.DescripcionCompleta).ToList();
            ViewBag.Provincias = provincias.Select(g => new SelectListItem 
            { 
                Text = string.IsNullOrEmpty(g.DescripcionCompleta) ? g.Descripcion : g.DescripcionCompleta, 
                Value = g.Id.ToString() 
            });

            if (provinciaId.HasValue && provinciaId.Value > 0)
            {
                ViewBag.Localidades = _context.Localidad.Where(x => x.IdProvincia == provinciaId.Value).OrderBy(l => l.Descripcion).Select(g => new SelectListItem { Text = g.Descripcion, Value = g.Id.ToString() });
            }
            else if (provincias.Any())
            {
                var primeraProvinciaId = provincias.First().Id;
                ViewBag.Localidades = _context.Localidad.Where(x => x.IdProvincia == primeraProvinciaId).OrderBy(l => l.Descripcion).Select(g => new SelectListItem { Text = g.Descripcion, Value = g.Id.ToString() });
            }
            else
            {
                ViewBag.Localidades = _context.Localidad.OrderBy(l => l.Descripcion).Select(g => new SelectListItem { Text = g.Descripcion, Value = g.Id.ToString() });
            }
        }

        // GET: Core/SolicitudDeTarjeta
        public async Task<IActionResult> Index()
        {
            return View(await _context.SolicitudDeTarjeta
                .Include(s => s.Localidad)
                .Include(s => s.Provincia)
                .Include(s => s.Estado)
                .OrderByDescending(s => s.FechaSolicitud).ToListAsync());
        }

        // GET: Core/SolicitudDeTarjeta/_Create
        public IActionResult _Create()
        {
            CargarViewBags();
            var model = new SolicitudDeTarjetaDTO
            {
                FechaNacimiento = DateTime.Today.AddYears(-18)
            };
            return PartialView(model);
        }

        // POST: Core/SolicitudDeTarjeta/_Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Create(SolicitudDeTarjetaDTO model)
        {
            ModelState.Remove("Id");
            if (ModelState.IsValid)
            {
                var entity = new SolicitudDeTarjeta
                {
                    Nombre = model.Nombre,
                    Apellido = model.Apellido,
                    DNI = model.DNI,
                    Email = model.Email,
                    FechaNacimiento = model.FechaNacimiento,
                    Calle = model.Calle,
                    Altura = model.Altura,
                    PisoDepto = model.PisoDepto,
                    CodigoPostal = model.CodigoPostal,
                    Localidad = model.Localidad != null && model.Localidad.Id > 0 ? _context.Localidad.Find(model.Localidad.Id) : null,
                    Provincia = model.Provincia != null && model.Provincia.Id > 0 ? _context.Provincia.Find(model.Provincia.Id) : null,
                    FechaSolicitud = DateTime.Now,
                    Estado = _context.EstadoSolicitudDeTarjeta.Where(e => e.Id == 1).FirstOrDefault()
                };

                _context.Add(entity);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Solicitud de tarjeta guardada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            CargarViewBags(model.Provincia?.Id);
            return PartialView(model);
        }

        // GET: Core/SolicitudDeTarjeta/_Edit/5
        public async Task<IActionResult> _Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entity = await _context.SolicitudDeTarjeta
                .Include(s => s.Localidad)
                .Include(s => s.Provincia)
                .Include(s => s.Estado)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (entity == null)
            {
                return NotFound();
            }

            CargarViewBags(entity.Provincia?.Id);

            var model = new SolicitudDeTarjetaDTO
            {
                Id = entity.Id,
                Nombre = entity.Nombre,
                Apellido = entity.Apellido,
                DNI = entity.DNI,
                Email = entity.Email,
                FechaNacimiento = entity.FechaNacimiento,
                Calle = entity.Calle,
                Altura = entity.Altura,
                PisoDepto = entity.PisoDepto,
                CodigoPostal = entity.CodigoPostal,
                Localidad = entity.Localidad,
                Provincia = entity.Provincia,
                FechaSolicitud = entity.FechaSolicitud,
                Estado = entity.Estado?.Nombre,
                NumeroTarjeta = entity.NumeroTarjeta,
            };

            return PartialView(model);
        }

        // POST: Core/SolicitudDeTarjeta/_Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Edit(int id, SolicitudDeTarjetaDTO model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var entity = await _context.SolicitudDeTarjeta
                    .Include(s => s.Localidad)
                    .Include(s => s.Provincia)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (entity == null)
                {
                    return NotFound();
                }

                entity.Nombre = model.Nombre;
                entity.Apellido = model.Apellido;
                entity.DNI = model.DNI;
                entity.Email = model.Email;
                entity.FechaNacimiento = model.FechaNacimiento;
                entity.Calle = model.Calle;
                entity.Altura = model.Altura;
                entity.PisoDepto = model.PisoDepto;
                entity.CodigoPostal = model.CodigoPostal;
                entity.Localidad = model.Localidad != null && model.Localidad.Id > 0 ? _context.Localidad.Find(model.Localidad.Id) : null;
                entity.Provincia = model.Provincia != null && model.Provincia.Id > 0 ? _context.Provincia.Find(model.Provincia.Id) : null;

                try
                {
                    _context.Update(entity);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SolicitudDeTarjetaExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["Success"] = "Solicitud de tarjeta actualizada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            CargarViewBags(model.Provincia?.Id);
            return PartialView(model);
        }

        [HttpPost]
        public string SelectLocalidades(int id)
        {
            string array = "<option value=''>Seleccione una Localidad</option>";
            var localidad = _context.Localidad.Where(x => x.IdProvincia == id).OrderBy(x => x.Descripcion).ToList();
            foreach (var loc in localidad)
            {
                array += "<option value='" + loc.Id + "'>" + loc.Descripcion + "</option>";
            }
            return array;
        }

        // GET: Core/SolicitudDeTarjeta/_Aprobar/5
        public async Task<IActionResult> _Aprobar(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entity = await _context.SolicitudDeTarjeta.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            var model = new AprobarSolicitudDTO
            {
                Id = entity.Id,
                NumeroTarjeta = entity.NumeroTarjeta
            };

            return PartialView(model);
        }

        // POST: Core/SolicitudDeTarjeta/_Aprobar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Aprobar(AprobarSolicitudDTO model)
        {
            if (ModelState.IsValid)
            {
                var entity = await _context.SolicitudDeTarjeta.FindAsync(model.Id);
                if (entity == null)
                {
                    return NotFound();
                }

                entity.Estado  = _context.EstadoSolicitudDeTarjeta.Where(e => e.Id == 2).FirstOrDefault();
                entity.NumeroTarjeta = model.NumeroTarjeta;
                entity.FechaDeRechazoAprobacion = DateTime.Now;

                _context.Update(entity);
                await _context.SaveChangesAsync();

                // Enviar mail de aprobación
                try
                {
                    string htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px;'>
                            <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='width: 100%; margin-bottom: 25px;'>
                                <tr>
                                    <td align='center' style='padding: 5px;'>
                                        <img src='https://portalestancias.com.ar/images/logo-email.png' alt='Textil' style='width: 100%;height: auto; display: block;' />
                                    </td>                     
                                </tr>
                            </table>

                            <h2>¡Tu solicitud de tarjeta ha sido aprobada!</h2>
                            <p>Estimado/a <strong>{entity.Nombre} {entity.Apellido}</strong>,</p>
                            <p>Queríamos avisarte que tu solicitud de la tarjeta Textil del Campo fue <strong>APROBADA</strong> exitosamente</p>
                            <p style='font-size: 16px; background-color: #f4f4f4; padding: 15px; border-left: 4px solid #28a745;'>
                                <strong>Número de tarjeta asignado:</strong> {entity.NumeroTarjeta}
                            </p>
                            <p>¡Gracias por formar parte de esta comunidad! Te invitamos a registrarte en la app y disfrutar de todos los beneficios exclusivos!</p>
                            <p>Saludos,<br>
                            <strong>Equipo Textil del Campo</strong></p>
                            <hr>
                            <small>Este es un correo automático, por favor no responder a este mensaje.</small>
                        </div>";

                    var mail = new MailAPI
                    {
                        Mail = entity.Email.Trim(),
                        Titulo = "Solicitud de Tarjeta Aprobada - Estancias",
                        Html = htmlBody
                    };

                    await _mailService.EnviarAsync(mail);
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"La solicitud fue aprobada, pero ocurrió un error al enviar el email: {ex.Message}";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = "La solicitud fue aprobada correctamente y se envió la notificación por correo electrónico.";
                return RedirectToAction(nameof(Index));
            }
            return PartialView(model);
        }

        // GET: Core/SolicitudDeTarjeta/Rechazar/5
        public async Task<IActionResult> Rechazar(int id)
        {
            var entity = await _context.SolicitudDeTarjeta.FindAsync(id);
            if (entity != null)
            {
                entity.Estado  = _context.EstadoSolicitudDeTarjeta.Where(e => e.Id == 3).FirstOrDefault();
                entity.FechaDeRechazoAprobacion = DateTime.Now;

                _context.Update(entity);
                await _context.SaveChangesAsync();

                // Enviar mail de rechazo
                try
                {
                    string htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px;'>
                            <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='width: 100%; margin-bottom: 25px;'>
                                <tr>
                                    <td align='center' style='padding: 5px;'>
                                        <img src='https://portalestancias.com.ar/images/logo-email.png' alt='Textil' style='width: 100%;height: auto; display: block;' />
                                    </td>    
                                </tr>
                            </table>

                            <h2>Información sobre tu Solicitud de Tarjeta</h2>
                            <p>Estimado/a <strong>{entity.Nombre} {entity.Apellido}</strong>,</p>
                            <p>Queríamos avisarte que no pudimos validar tu gestión de manera online. Te recomendamos acercarte a nuestra sucursal más cercana para que podamos verificar tu caso nuevamente.</p>
                            <p>Nuestro personal estará a disposición para resolver tu solicitud</p>
                            <p>Muchas gracias,</p>
                            <p>Saludos,<br>
                            <strong>Equipo Textil del Campo</strong></p>
                            <hr>
                            <small>Este es un correo automático, por favor no responder a este mensaje.</small>
                        </div>";

                    var mail = new MailAPI
                    {
                        Mail = entity.Email.Trim(),
                        Titulo = "Novedades sobre tu Solicitud de Tarjeta - Estancias",
                        Html = htmlBody
                    };

                    await _mailService.EnviarAsync(mail);
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"La solicitud fue marcada como rechazada, pero ocurrió un error al enviar el email: {ex.Message}";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = "La solicitud fue rechazada y se envió la notificación por correo electrónico.";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Core/SolicitudDeTarjeta/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.SolicitudDeTarjeta.FindAsync(id);
            if (entity != null)
            {
                _context.SolicitudDeTarjeta.Remove(entity);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Solicitud de tarjeta eliminada correctamente.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerCantidadPendientes()
        {
            var cantidad = await _context.SolicitudDeTarjeta
                .Where(s => s.Estado == null || s.Estado.Id == 1)
                .CountAsync();

            var ultimas = await _context.SolicitudDeTarjeta
                .Where(s => s.Estado == null || s.Estado.Id == 1)
                .OrderByDescending(s => s.FechaSolicitud)
                .Take(5)
                .Select(s => new
                {
                    s.Id,
                    NombreCompleto = $"{s.Nombre} {s.Apellido}",
                    Fecha = s.FechaSolicitud.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(new { pendientes = cantidad, ultimas });
        }

        // GET: Core/SolicitudDeTarjeta/_VerAdjuntos/5
        public async Task<IActionResult> _VerAdjuntos(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entity = await _context.SolicitudDeTarjeta.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            var model = new AdjuntosSolicitudDTO
            {
                Id = entity.Id,
                NombreCompleto = $"{entity.Nombre} {entity.Apellido}",
                DNI = entity.DNI,
                FrenteDNIBase64 = entity.FrenteDNI != null && entity.FrenteDNI.Length > 0 ? Convert.ToBase64String(entity.FrenteDNI) : null,
                DorsoDNIBase64 = entity.DorsoDNI != null && entity.DorsoDNI.Length > 0 ? Convert.ToBase64String(entity.DorsoDNI) : null,
                SelfieBase64 = entity.Selfie != null && entity.Selfie.Length > 0 ? Convert.ToBase64String(entity.Selfie) : null
            };

            return PartialView("_VerAdjuntos", model);
        }

        private bool SolicitudDeTarjetaExists(int id)
        {
            return _context.SolicitudDeTarjeta.Any(e => e.Id == id);
        }
    }
}

