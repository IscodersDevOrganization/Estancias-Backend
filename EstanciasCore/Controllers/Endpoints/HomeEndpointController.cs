using Commons.Extensions;
using Commons.Identity.Extensions;
using DAL.Data;
using DAL.Models;
using DAL.Models.Core;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EstanciasCore.Controllers
{
    // Agregar el using real del filtro si está declarado en otro namespace.
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/dashboard")]
    public class DashboardEndpointController : EstanciasCoreController
    {
        public DashboardEndpointController(EstanciasContext context) : base(context)
        {
        }

        private bool TienePermisoModulo(bool isAdmin, params string[] rutasAcceso)
        {
            if (isAdmin) return true;
            if (rutasAcceso == null || rutasAcceso.Length == 0) return false;

            foreach (var ruta in rutasAcceso)
            {
                if (string.IsNullOrEmpty(ruta)) continue;
                if (HttpContext.UserHasRoute(ruta)) return true;

                if (!ruta.StartsWith("/Core/", StringComparison.OrdinalIgnoreCase) &&
                    !ruta.StartsWith("/Administracion/", StringComparison.OrdinalIgnoreCase) &&
                    !ruta.StartsWith("/Reportes/", StringComparison.OrdinalIgnoreCase))
                {
                    if (HttpContext.UserHasRoute("/Core" + (ruta.StartsWith("/") ? ruta : "/" + ruta))) return true;
                    if (HttpContext.UserHasRoute("/Administracion" + (ruta.StartsWith("/") ? ruta : "/" + ruta))) return true;
                    if (HttpContext.UserHasRoute("/Reportes" + (ruta.StartsWith("/") ? ruta : "/" + ruta))) return true;
                }
                else
                {
                    var slashIdx = ruta.IndexOf('/', 1);
                    if (slashIdx > 0)
                    {
                        var withoutArea = ruta.Substring(slashIdx);
                        if (HttpContext.UserHasRoute(withoutArea)) return true;
                    }
                }
            }
            return false;
        }

        [HttpGet("metricas")]
        public async Task<IActionResult> ObtenerMetricasDashboard()
        {
            // El filtro UAT no reemplaza los permisos por usuario del dashboard.
            if (User?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(User.Identity.Name))
                return Unauthorized(new { mensaje = "Se necesita un usuario autenticado para consultar sus permisos." });

            var rolesusuario = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserName == User.Identity.Name);
            bool isAdmin = ((ClaimsPrincipal)User).IsAdmin() || (rolesusuario != null && rolesusuario.Administradores == true);

            bool puedeVerClientes = TienePermisoModulo(isAdmin, "/Core/Clientes/Index", "/Clientes/Index");
            bool puedeVerPagos = TienePermisoModulo(isAdmin, "/Core/PagoTarjeta/Index", "/PagoTarjeta/Index");
            bool puedeVerSolicitudes = TienePermisoModulo(isAdmin, "/Core/SolicitudDeTarjeta/Index", "/SolicitudDeTarjeta/Index");
            bool puedeVerMorosos = TienePermisoModulo(isAdmin, "/Core/Morosos/Index", "/Morosos/Index");

            var hoy = DateTime.Today;

            // Clientes
            int sociosConApp = 0;
            int sociosNuevosMes = 0;
            if (puedeVerClientes)
            {
                sociosConApp = await _context.Clientes.AsNoTracking().CountAsync();
                sociosNuevosMes = await _context.Clientes.AsNoTracking().Where(x => x.FechaIngreso.Date >= hoy.AddDays(-30).Date).CountAsync();
            }

            // Pagos
            int pagosHoyCantidad = 0;
            decimal pagosHoyMonto = 0;
            var ultimosPagosList = new List<object>();

            if (puedeVerPagos)
            {
                var pagosHoyQuery = await _context.PagoTarjeta.AsNoTracking()
                    .Include(x => x.Persona)
                    .Where(x => x.EstadoPago == EstadoPago.Pagado &&
                                ((x.FechaDePago.HasValue && x.FechaDePago.Value.Date == hoy) ||
                                 (x.FechaComprobante.HasValue && x.FechaComprobante.Value.Date == hoy)))
                    .OrderByDescending(x => x.FechaDePago ?? x.FechaComprobante)
                    .ToListAsync();

                pagosHoyCantidad = pagosHoyQuery.Count;
                pagosHoyMonto = pagosHoyQuery.Sum(x => x.MontoInformado);

                ultimosPagosList = pagosHoyQuery.Take(5).Select(p => new
                {
                    PersonaNombre = p.Persona != null ? p.Persona.GetNombreCompleto() : "-",
                    NroTarjeta = p.NroTarjeta ?? "-",
                    MontoInformado = p.MontoInformado,
                    Fecha = p.FechaDePago.HasValue ? p.FechaDePago.Value.ToString("dd/MM/yyyy HH:mm") : "-"
                }).Cast<object>().ToList();
            }

            // Solicitudes
            int solicitudesPendientes = 0;
            int solicitudesAprobadas = 0;
            int solicitudesRechazadas = 0;
            var ultimasSolicitudesList = new List<object>();

            if (puedeVerSolicitudes)
            {
                solicitudesPendientes = await _context.SolicitudDeTarjeta.AsNoTracking()
                    .Where(x => x.Estado == null || x.Estado.Id == 1)
                    .CountAsync();

                solicitudesAprobadas = await _context.SolicitudDeTarjeta.AsNoTracking()
                    .Where(x => x.Estado != null && x.Estado.Id == 2)
                    .CountAsync();

                solicitudesRechazadas = await _context.SolicitudDeTarjeta.AsNoTracking()
                    .Where(x => x.Estado != null && x.Estado.Id == 3)
                    .CountAsync();

                var solList = await _context.SolicitudDeTarjeta.AsNoTracking()
                    .Include(x => x.Estado)
                    .OrderByDescending(x => x.FechaSolicitud)
                    .Take(5)
                    .ToListAsync();

                ultimasSolicitudesList = solList.Select(s => new
                {
                    NombreCompleto = $"{s.Nombre} {s.Apellido}",
                    DNI = s.DNI ?? "-",
                    Fecha = s.FechaSolicitud.ToString("dd/MM/yyyy HH:mm"),
                    EstadoId = s.Estado != null ? s.Estado.Id : 1
                }).Cast<object>().ToList();
            }

            // Morosos
            int morososCount = 0;
            if (puedeVerMorosos)
            {
                morososCount = await _context.Morosos.AsNoTracking().CountAsync();
            }

            return Ok(new
            {
                puedeVerClientes,
                puedeVerPagos,
                puedeVerSolicitudes,
                puedeVerMorosos,
                sociosConApp,
                sociosNuevosMes,
                pagosHoyCantidad,
                pagosHoyMonto,
                ultimosPagos = ultimosPagosList,
                solicitudesPendientes,
                solicitudesAprobadas,
                solicitudesRechazadas,
                ultimasSolicitudes = ultimasSolicitudesList,
                morososCount
            });
        }
    }
}
