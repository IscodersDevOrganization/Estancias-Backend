using Commons.Extensions;
using Commons.Identity.Extensions;
using Commons.Identity.Services;
using DAL.Data;
using DAL.DTOs.Reportes;
using DAL.DTOs.Servicios;
using DAL.Models;
using DAL.Models.Core;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using iText.Html2pdf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml.FormulaParsing.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

namespace EstanciasCore.Controllers
{
    public class HomeController : EstanciasCoreController
    {
        private readonly SignInManager<Usuario> _signInManager;
        private readonly UserService<Usuario> _userManager;
        private readonly IResumenTarjetaService _resumen;
        private readonly IDatosTarjetaService _datosTarjeta;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMailService _mailService;
        public HomeController(EstanciasContext context, UserService<Usuario> userManager, SignInManager<Usuario> signInManager, IResumenTarjetaService resumen, IDatosTarjetaService datosTarjeta, ICompositeViewEngine viewEngine, IServiceProvider serviceProvider, IMailService mailService) : base(context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _resumen = resumen;
            _datosTarjeta=datosTarjeta;
            _viewEngine=viewEngine;
            _serviceProvider = serviceProvider;
            _mailService = mailService;
        }
        public IActionResult Index()
        {
            AddPageAlerts(PageAlertType.Success, $"Bienvenido {User.Identity.Name}!");
            return View();
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

        [HttpGet]
        public async Task<IActionResult> ObtenerMetricasDashboard()
        {
            var rolesusuario = _context.Users.FirstOrDefault(x => x.UserName == User.Identity.Name);
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
                sociosConApp = await _context.Clientes.CountAsync();
                sociosNuevosMes = await _context.Clientes.Where(x => x.FechaIngreso.Date >= hoy.AddDays(-30).Date).CountAsync();
            }

            // Pagos
            int pagosHoyCantidad = 0;
            decimal pagosHoyMonto = 0;
            var ultimosPagosList = new List<object>();

            if (puedeVerPagos)
            {
                var pagosHoyQuery = await _context.PagoTarjeta
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
                solicitudesPendientes = await _context.SolicitudDeTarjeta
                    .Where(x => x.Estado == null || x.Estado.Id == 1)
                    .CountAsync();

                solicitudesAprobadas = await _context.SolicitudDeTarjeta
                    .Where(x => x.Estado != null && x.Estado.Id == 2)
                    .CountAsync();

                solicitudesRechazadas = await _context.SolicitudDeTarjeta
                    .Where(x => x.Estado != null && x.Estado.Id == 3)
                    .CountAsync();

                var solList = await _context.SolicitudDeTarjeta
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
                morososCount = await _context.Morosos.CountAsync();
            }

            return Json(new
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




        public async Task<IActionResult> DescargarResumen(string dni)
        {
            Usuario usuarioLocal = _context.Usuarios.Where(x => x.Personas.NroDocumento == dni).FirstOrDefault();
            DateTime fecha = DateTime.Now;

            var resumen = _context.ResumenTarjeta.Where(x => x.Usuario.Personas.NroDocumento == dni).OrderByDescending(x => x.Periodo.FechaHasta).FirstOrDefault();        

            return File(resumen.Adjunto, "application/pdf", "ResumenBancario.pdf");
        }

        public async Task<IActionResult> DescargarResumenHtml(string dni)
        {
            Usuario usuarioLocal = _context.Usuarios.Where(x => x.Personas.NroDocumento == dni).FirstOrDefault();
            DateTime fechaMesActualCuotas = DateTime.Now;


            //DateTime fechaMesActualCuotas = new DateTime(2025, 11, 01);
            int diasEnMes = DateTime.DaysInMonth(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month);

            //Fecha para Punitorios
            if (fechaMesActualCuotas.Day > 15)
            {
                DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
            }
            else
            {
                DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, 15);
            }

            DateTime fechaActualCuotas = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
            DateTime fechaActualCuotasProximo = fechaActualCuotas.AddMonths(1);


            var movimientos = _datosTarjeta.ConsultarMovimientos("APPESTANCIAS", "appcpe01", dni, Convert.ToInt64(usuarioLocal.Personas.NroTarjeta), 100, 1).Result;

            var datosResumen = _datosTarjeta.CuotasDetallesResumen(movimientos, fechaActualCuotas).Result;

            var datosResumenConPunitorios = _datosTarjeta.CalcularPunitoriosResumen(datosResumen).Result;

            Periodo periodo = _context.Periodo.Where(x => x.FechaVencimiento.Date==new DateTime(2025, 11, 15).Date).FirstOrDefault();

            UsuarioParaProcesarDTO usuarioDTO = new UsuarioParaProcesarDTO()
            {
                NroDocumento = usuarioLocal.Personas.NroDocumento,
                NombreCompleto = usuarioLocal.Personas.GetNombreCompleto(),
                Id = usuarioLocal.Id,
                UserName = usuarioLocal.UserName,
                NroTarjeta = usuarioLocal.Personas.NroTarjeta
            };

            var datosParaResumenDTO = _datosTarjeta.PrepararDatosResumen(movimientos, datosResumenConPunitorios, periodo, usuarioDTO).Result;

            var html = await _datosTarjeta.RenderViewToStringAsync("ResumenBancarioTemplate", datosParaResumenDTO);

            return View("ResumenBancarioTemplate", datosParaResumenDTO);
        }

        public async Task<string> EnvioDeMail(int PeriodoId)
        {
            //var dnisConfig = _context.Usuarios.Where(x=>x.Personas!=null).Select(x=>x.Personas.NroDocumento).ToList();

            //_resumen.GenerarResumenTarjetas();

            //var dnisConfig = new List<string>() { "37217944", "29129264", "30463400", "28437058", "17984862", "38157735", "38321219", "36141667" };
            //var dnisConfig = new List<string>() { "30463400" };


            try
            {
                int cont = 0;
                var periodo = _context.Periodo.Where(x => x.Id==PeriodoId).FirstOrDefault();
                if (periodo==null)
                {
                    return "El periodo no existe";
                }

                var dnisConfig = _context.ResumenTarjeta.Where(x => x.Periodo.Id==PeriodoId).ToList();
                if (dnisConfig.Count()==0)
                {
                    return "No hay Resumen del Periodode "+periodo.Descripcion;
                }

                foreach (var item in dnisConfig)
                {
                    cont++;
                    //var resumenesUsuario = _context.ResumenTarjeta.Where(x => x.Usuario.Personas.NroDocumento == item && x.Periodo.Id==95).FirstOrDefault();

                    CultureInfo culturaAR = new CultureInfo("es-AR");
                    string mesNombre = culturaAR.DateTimeFormat.GetMonthName(periodo.FechaHasta.Month);
                    string asunto = $" Tu resumen de Tarjeta Estancias ya está disponible";

                    // **1. Genera el PDF en bytes (utilizando el Adjunto pre-generado)**
                    byte[] pdfBytes = item.Adjunto;
                    DateTime fechaVencimiento = new DateTime(periodo.FechaVencimiento.Year, periodo.FechaVencimiento.Month, 10);

                    var detallesCuotasResumenDTO = new DetallesCuotasResumenDTO()
                    {
                        Fecha = fechaVencimiento.ToString("dd/MM"),
                        // Nota: Usando decimales correctos para la suma.
                        Monto = item.Monto + item.MontoAdeudado,
                    };

                    // **2. Renderiza la vista del correo electrónico**
                    var viewHtml = RenderViewToString(_viewEngine, _serviceProvider, "Home/MailResumen", detallesCuotasResumenDTO, mesNombre).Result;

                    common.EnviarMailSendinBlueAdjunto(new MailAPI { Mail = item.Usuario.UserName, Titulo = asunto, Html = viewHtml }, pdfBytes);
                    //common.EnviarMailSendinBlueAdjunto(new MailAPI { Mail = "javiermercenario@hotmail.com", Titulo = asunto, Html = viewHtml }, pdfBytes);
                }

                return "Finalizado con Exito - se enviaron "+cont+" resumenes";
            }
            catch (Exception e)
            {
                return "Error"+ e.Message;
            }                     

        }

        public async Task<IActionResult> DescargarResumenpdf(string dni)
        {
            Usuario usuarioLocal = _context.Usuarios.Where(x => x.Personas.NroDocumento == dni).FirstOrDefault();
            DateTime fechaMesActualCuotas = DateTime.Now;


            //DateTime fechaMesActualCuotas = new DateTime(2025, 11, 01);
            int diasEnMes = DateTime.DaysInMonth(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month);

            //Fecha para Punitorios
            if (fechaMesActualCuotas.Day > 15)
            {
                DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
            }
            else
            {
                DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, 15);
            }

            DateTime fechaActualCuotas = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
            DateTime fechaActualCuotasProximo = fechaActualCuotas.AddMonths(1);


            var movimientos = _datosTarjeta.ConsultarMovimientos("APPESTANCIAS", "appcpe01", dni, Convert.ToInt64(usuarioLocal.Personas.NroTarjeta), 100, 1).Result;

            var datosResumen = _datosTarjeta.CuotasDetallesResumen(movimientos, fechaActualCuotas).Result;

            var datosResumenConPunitorios = _datosTarjeta.CalcularPunitoriosResumen(datosResumen).Result;

            Periodo periodo = _context.Periodo.Where(x => x.FechaVencimiento.Date==new DateTime(2025, 11, 15).Date).FirstOrDefault();

            UsuarioParaProcesarDTO usuarioDTO = new UsuarioParaProcesarDTO()
            {
                NroDocumento = usuarioLocal.Personas.NroDocumento,
                NombreCompleto = usuarioLocal.Personas.GetNombreCompleto(),
                Id = usuarioLocal.Id,
                UserName = usuarioLocal.UserName,
                NroTarjeta = usuarioLocal.Personas.NroTarjeta
            };

            var datosParaResumenDTO = _datosTarjeta.PrepararDatosResumen(movimientos, datosResumenConPunitorios, periodo, usuarioDTO).Result;

            var html = await _datosTarjeta.RenderViewToStringAsync("ResumenBancarioTemplate", datosParaResumenDTO); 
            byte[] pdfBytesPDF;
            using (var memoryStream = new MemoryStream())
            {
                // El método HtmlConverter.ConvertToPdf realiza la magia
                HtmlConverter.ConvertToPdf(html, memoryStream);
                pdfBytesPDF = memoryStream.ToArray();
            }
            string nombreArchivo = $"Resumen-{usuarioLocal.Personas.NroDocumento}-{DateTime.Now:yyyyMMdd}.pdf";

            /*-----------------*/
            DateTime fechaVencimiento = new DateTime(2025, 11, 10);

            string Mes = fechaVencimiento.ToString("MMMM", new System.Globalization.CultureInfo("es-ES"));
            var detallesCuotasResumenDTO = new DetallesCuotasResumenDTO()
            {
                Fecha = fechaVencimiento.ToString("dd/MM"),
                // Nota: Usando decimales correctos para la suma.
                Monto = datosParaResumenDTO.SaldoActual + datosParaResumenDTO.SaldoAnterior,
            };

            // **2. Renderiza la vista del correo electrónico**
            var viewHtml = RenderViewToString(_viewEngine, _serviceProvider, "Home/MailResumen", detallesCuotasResumenDTO, Mes).Result;


            // Creamos el objeto que espera nuestra interfaz
            var mail = new MailAPI
            {
                //Mail = prestamo.Cliente.Empresa.Mail,
                Mail = "jorge.cutulli@iscoders.com.ar",
                Titulo = "Tu resumen de Tarjeta Estancias ya está disponible",
                Html = viewHtml
            };

            var configActiva = _context.MailConfig.Where(c => c.Activo).FirstOrDefault();

            if (configActiva != null)
                await _mailService.EnviarAsync(mail, pdfBytesPDF);

            //common.EnviarMailSendinBlueAdjunto(new MailAPI { Mail = "jorgecutuli@gmail.com", Titulo = "Tu resumen de Tarjeta Estancias ya está disponible", Html = viewHtml }, pdfBytesPDF);


            /*-----------------*/

            return File(pdfBytesPDF, "application/pdf", nombreArchivo);
        }

        public IActionResult MailRegistro()
        {
            return View("MailRegistro");
        }
		public IActionResult MailRecuperaPassword()
		{
			return View("MailRecuperaPassword");
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new DAL.Models.ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        private async Task<string> RenderViewToString(ICompositeViewEngine viewEngine, IServiceProvider serviceProvider, string viewName, DetallesCuotasResumenDTO model, string mesNombre)
        {
            var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            using (var sw = new StringWriter())
            {
                var viewResult = viewEngine.FindView(actionContext, viewName, false);

                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"No se pudo encontrar la vista '{viewName}'");
                }

                var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                {
                    Model = model
                };

                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(actionContext.HttpContext, serviceProvider.GetRequiredService<ITempDataProvider>()),
                    sw,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                string html = sw.ToString();
                var culturaAR = new CultureInfo("es-AR");

                string textoModificado = html.Replace("TextoFechaReemplazar", model.Fecha);
                textoModificado = textoModificado.Replace("TextoMontoReemplazar", model.Monto.ToString("N2", culturaAR));
                textoModificado = textoModificado.Replace("TextoMesEscritoReemplazar", mesNombre+"!");

                return textoModificado;
            }
        }

    }
}