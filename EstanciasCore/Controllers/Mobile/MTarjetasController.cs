using DAL.Data;
using DAL.DTOs;
using DAL.DTOs.API;
using DAL.DTOs.ApiCpeCreditos;
using DAL.DTOs.Reportes;
using DAL.DTOs.Servicios;
using DAL.Mobile;
using DAL.Models;
using DAL.Models.Core;
using EstanciasCore.API.Filters;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using iText.Html2pdf;
using Microsoft.AspNetCore.Hosting;
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
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;
using static EstanciasCore.Services.MercadoPagoServices;
using PagoTarjetaDTO = DAL.Mobile.PagoTarjetaDTO;

namespace EstanciasCore.API.Controllers.Billetera
{
    [TypeFilter(typeof(ChequeaUatApiAttribute))]
    [ApiController]
    [Route("api/[controller]")]
    public class MTarjetasController : BaseApiController
    {
        private readonly MercadoPagoServices _mp;
        private readonly IDatosTarjetaService _datosServices;
        private readonly IHostingEnvironment _webHostEnvironment; 
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MTarjetasController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMailService _mailService;

        public MTarjetasController(EstanciasContext context, MercadoPagoServices mp, IDatosTarjetaService datosServices, IHostingEnvironment webHostEnvironment, IServiceScopeFactory scopeFactory, ILogger<MTarjetasController> logger, IConfiguration configuration, ICompositeViewEngine viewEngine, IServiceProvider serviceProvider, IMailService mailService) : base(context)
        {
            _datosServices = datosServices;
            _mp = mp;
            _webHostEnvironment = webHostEnvironment;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
            _viewEngine=viewEngine;
            _serviceProvider=serviceProvider;
            _mailService = mailService;
        }

        [HttpPost("Alta")]
        public async Task<IActionResult> Alta([FromBody] DAL.DTOs.API.AltaTarjetaDTO altaTarjetaDTO)
        {
            try
            {
                var usuario = TraeUsuarioUAT(altaTarjetaDTO.UAT);
                var billetera = _context.Billeteras.Where(b => b.Cliente.Usuario.Id == usuario.Id).FirstOrDefault();
                var tarjeta = new Tarjeta { Titular = altaTarjetaDTO.Titular, Numero = altaTarjetaDTO.Numero, Vencimiento = altaTarjetaDTO.Vencimiento };
                billetera.Tarjetas.Add(tarjeta);
                _context.Update(billetera);
                await _context.SaveChangesAsync();

                return new JsonResult(new RespuestaAPI { Status = 200, UAT = altaTarjetaDTO.UAT, Mensaje = "Tarjeta creada con exito" });
            }
            catch (Exception e)
            {
                Log.Error($"Error en creacion de tarjeta - {e.Message}");
                return new JsonResult(new RespuestaAPI { Status = 500, UAT = altaTarjetaDTO.UAT, Mensaje = $"Error en creacion de terjeta" });
            }

        }

        [HttpPost("MovimientoTarjeta")]
        public async Task<IActionResult> MovimientoTarjeta([FromBody] ListaMovimientoTarjetaDTO movimientostarjetaDTOS)
        {
            try
            {
                decimal MontoCuota = 0;
                decimal MontoProximaCuota = 0;
                decimal MontoPunitorios = 0;
                decimal DeudaTotal = 0;
                decimal TotalRedondeo = 0;
                decimal MontoDisponible = 0;
                string totalDeuda = "0";
                int Status = 200;
                string Mensaje = "Movimiento obtenidos";
                string Resultado = "Exito";

                List <MovimientoTarjetaDTO> comprasAgrupadas = new List<MovimientoTarjetaDTO>();
                ResponseObtenerDatosPersonaDTO datosPersona = new ResponseObtenerDatosPersonaDTO();
                ResponseObtenerConsultaDTO montosConPunitorios = new ResponseObtenerConsultaDTO();
                var fechaMesActualCuotas = DateTime.Now;
                //var fechaMesActualCuotas = new DateTime(2025,10,01);

                int diasEnMes = DateTime.DaysInMonth(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month);

                //Fecha para Punitorios
                if (fechaMesActualCuotas.Day>15)
                {
                    DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
                }
                else
                {
                    DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, 15);
                }

                DateTime fechaActualCuotas = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
                DateTime fechaActualCuotasProximo = fechaActualCuotas.AddMonths(1);

                var usuario = TraeUsuarioUAT(movimientostarjetaDTOS.UAT);
                if (usuario == null)
                    return new JsonResult(new RespuestaAPI { Status = 500, UAT = movimientostarjetaDTOS.UAT, Mensaje = $"no existe UAT de Usuario" });

                DatosEstructura empresa = _context.DatosEstructura.FirstOrDefault();
                
                var datosMovimientos = _datosServices.ConsultarMovimientos(empresa.UsernameWS.ToLower(), empresa.PasswordWS, usuario.Personas.NroDocumento, movimientostarjetaDTOS.NroTarjeta, 100, 0).Result;
                if (datosMovimientos.Detalle.Resultado=="EXITO")
                {
                    CultureInfo.CurrentCulture = new CultureInfo("es-AR");

                    //Monto Disponible
                    MontoDisponible = Math.Round(Convert.ToDecimal(datosMovimientos.Detalle.MontoDisponible.Replace(".", ",")), 2);

                    //Calcula Cuota del Mes
                    MontoCuota = await _datosServices.CalcularMontoCuota(datosMovimientos, fechaActualCuotas);

                    //Calcula Cuota del Proximo Mes
                    MontoProximaCuota = await _datosServices.CalcularMontoProximaCuota(datosMovimientos, fechaActualCuotasProximo);

                    //Calculo de Punitorios
                    MontoPunitorios = await _datosServices.CalcularPunitorios(datosMovimientos.DetallesSolicitud);                    

                    //Movimientos Tarjeta
                    comprasAgrupadas = await _datosServices.ObtieneUltimosMovimientos(datosMovimientos, 20);
                }

                //Calcula Deuda total suma la cuota mas los punitorios.
                DeudaTotal = MontoCuota + MontoPunitorios;
                TotalRedondeo = Math.Round(DeudaTotal, 2);

                var fechaVencimiento = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, 10);

                var datosEmpresa = _context.DatosEstructura.FirstOrDefault();

                //Obtiene los datos de la persona
                datosPersona = await _datosServices.ObtenerPersona(usuario.Personas.NroDocumento);

                if (datosPersona != null)
                {
                    string letraSexo = "";
                    if (datosPersona.Persona.Sexo.Id == 3)
                    {
                        letraSexo = "F";
                    }
                    else
                    {
                        if (datosPersona.Persona.Sexo.Id == 2)
                        {
                            letraSexo = "M";
                        }
                        else if (datosPersona.Persona.Sexo.Id == 1)
                        {
                            letraSexo = "F";
                        }
                    }

                    //Obtiene los datos de la persona
                    montosConPunitorios = await _datosServices.ObtenerConsulta(usuario.Personas.NroDocumento, letraSexo);
                    if (montosConPunitorios != null)
                    {
                        DateTime hoy = DateTime.Today;
                        int diasEnElMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
                        DateTime fechaActual = new DateTime(hoy.Year, hoy.Month, diasEnElMes);
                        if (montosConPunitorios.cobranzas != null)
                        {
                            totalDeuda = montosConPunitorios.cobranzas.Where(x => x.fechaVencimiento.Date <= fechaActual).Sum(x => x.importe).ToString();
                        }
                    }
                    else
                    {

                        Status = 400;
                        Mensaje = "Error al obtener montos y punitorios de LOAN";
                        Resultado = "Error";
                    }
                }
                else
                {
                    Status = 400;
                    Mensaje = "Error al obtener datos de la persona de LOAN";
                    Resultado = "Error";
                }

                return new JsonResult(
                    new ListaMovimientoTarjetaDTO
                    {
                        Status = Status,
                        UAT = "null",
                        Mensaje = Mensaje,
                        Resultado = Resultado,
                        NroTarjeta = movimientostarjetaDTOS.NroTarjeta,
                        Nombre = usuario.Personas.GetNombreCompleto(),
                        NroDocumento = Convert.ToInt32(usuario.Personas.NroDocumento),
                        Direccion = datosMovimientos.Detalle.Direccion,
                        MontoAdeudado = totalDeuda.Replace(".", ","),
                        //MontoAdeudado = TotalRedondeo.ToString().Replace(".", ","),
                        ProximaFechaPago = fechaVencimiento.ToString("dd/MM/yyyy"),
                        CuotaVencida = true,
                        TotalProximaCuota = MontoProximaCuota.ToString().Replace(".", ","),
                        FechaPagoProximaCuota = fechaVencimiento.AddMonths(1).ToString("dd/MM/yyyy"),
                        MontoDisponible = MontoDisponible.ToString().Replace(".", ","),
                        ContieneLeyenda = false,
                        Leyenda = "",
                        Telefono = empresa.Telefono,
                        CantMovimientos = comprasAgrupadas.Count(),
                        CategoriaUsuario = usuario.UsuariosCategorias.Nombre,
                        CategoriaColor = usuario.UsuariosCategorias.CodigoColor,
                        ImagenTarjeta = (usuario.UsuariosCategorias.ImagenTarjeta==null ? "" : Convert.ToBase64String(usuario.UsuariosCategorias.ImagenTarjeta)),
                        AliasEmpresa = datosEmpresa.Alias,
                        WhatsappEmpresa = datosEmpresa.Telefono,
                        CBUEmpresa = datosEmpresa.CBU,
                        MovimientosTarjeta = comprasAgrupadas,
                    });

                
            }
            catch (Exception e)
            {
                Log.Error($"Error en MovimientoTarjeta - {e.Message}");
                return new JsonResult(new RespuestaAPI { Status = 500, UAT = movimientostarjetaDTOS.UAT, Mensaje = $"Error en MovimientoTarjeta: {e.Message}" });
            }
        }
                

        [HttpPost("TraePeriodos")]
        public async Task<IActionResult> TraePeriodos(TraePeriodosDTO body)
        {
            TraePeriodosDTO traePeriodosDTO = new TraePeriodosDTO() { UAT = body.UAT };

            var usuario = TraeUsuarioUAT(body.UAT);
            if (usuario == null)
                return new JsonResult(new RespuestaAPI { Status = 500, UAT = traePeriodosDTO.UAT, Mensaje = $"no existe UAT de Usuario" });

            var periodos = await _context.Periodo.Where(x=>x.Activo).Select(x=> new PeriodoDTO() { FechaDesde = x.FechaDesde, FechaHasta = x.FechaHasta, Id = x.Id, Nombre = x.Descripcion }).ToListAsync();
            if (periodos == null || !periodos.Any())
            {
                return NotFound("No se encontraron periodos.");
            }
            traePeriodosDTO.Periodos = periodos.OrderByDescending(x => x.FechaDesde).ToList();
            traePeriodosDTO.Status = 200;
            traePeriodosDTO.Mensaje = "Periodos obtenidos con exito";
            return new JsonResult(traePeriodosDTO);
        }

               


        [HttpPost("DescargarResumen")]
        public async Task<IActionResult> DescargarResumen(MovimientosTarjetaDTO body)
        {
            try
            {
                //var usuario = TraeUsuarioUAT(body.UAT);
                //if (usuario == null)
                //{
                //    return BadRequest(new { Mensaje = "UAT de Usuario no válida o inexistente." });
                //}
                Periodo periodo = _context.Periodo.Where(p=>p.Id==body.PeriodoId).FirstOrDefault();
                if (periodo==null)
                {
                    return UnprocessableEntity(new { Mensaje = "El Período con el ID proporcionado no existe." });
                }

                Usuario user = _context.Usuarios.Where(x => x.Personas.NroDocumento == body.NroDocumento).FirstOrDefault();

                ResumenTarjeta resumenTarjeta = _context.ResumenTarjeta.Where(x => x.UsuarioId== user.Id && x.PeriodoId== periodo.Id).FirstOrDefault();
                if (resumenTarjeta==null)
                {
                    return NotFound(new { Mensaje = "No se encontró un resumen para el usuario y período especificados." });
                }
                string fechaString = DateTime.Now.ToString("ddMMyyyy");
                string nombreArchivo = $"Resumen_{user.Personas.NroDocumento}_{fechaString}";

                return File(resumenTarjeta.Adjunto, "application/pdf", nombreArchivo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ocurrió un error inesperado al procesar la solicitud." });
            }
        }
                

        [HttpPost("EnvioDeResumen")]
        public async Task<IActionResult> EnvioDeResumen(EnvioDeResumen body)
        {
            try
            {
                var usuario = TraeUsuarioUAT(body.UAT);
                if (usuario == null)
                {
                    return BadRequest(new { Mensaje = "UAT de Usuario no válida o inexistente." });
                }
                Periodo periodo = _context.Periodo.Where(x=>x.Id==body.PeriodoId).OrderByDescending(x => x.Id).FirstOrDefault();
                if (periodo==null)
                {
                    return UnprocessableEntity(new { Mensaje = "El Período con el ID proporcionado no existe." });
                }
                var resumen = _context.ResumenTarjeta.Where(x => x.Usuario.Personas.NroDocumento== body.NroDocumento && x.Periodo.Id==periodo.Id).FirstOrDefault();
                if (resumen == null)
                {
                    return BadRequest(new { Mensaje = "No Existe ningún resumen." });
                }

                byte[] pdfBytes = resumen.Adjunto;
                DateTime fechaVencimiento = new DateTime(periodo.FechaVencimiento.Year, periodo.FechaVencimiento.Month, 10);
                var detallesCuotasResumenDTO = new DetallesCuotasResumenDTO()
                {
                    Fecha = fechaVencimiento.ToString("dd/MM/yyyy"),
                    Monto = resumen.Monto+resumen.MontoAdeudado,
                };

                string mesNombre = ConvertirNumeroAMes(periodo.FechaHasta.Month);
                string asunto = $"Tu resumen del mes de {mesNombre} ya está disponible";
                string fechaString = DateTime.Now.ToString("ddMMyyyy");
                string nombreArchivo = $"Resumen_{resumen.Usuario.Personas.NroDocumento}_{fechaString}";

                var viewHtml = await RenderViewToString(_viewEngine, _serviceProvider, "Home/MailResumen", detallesCuotasResumenDTO, mesNombre);
                if (body.email!=null)
                {
                    //await common.EnviarMailSendinBlueAdjunto(new MailAPI { Mail = body.email, Titulo = asunto, Html = viewHtml }, pdfBytes);

                    var mail = new MailAPI { Mail = body.email, Titulo = asunto, Html = viewHtml};                    
                    await _mailService.EnviarAsync(mail, pdfBytes);
                }
                else
                {
                    //await common.EnviarMailSendinBlueAdjunto(new MailAPI { Mail = resumen.Usuario.UserName, Titulo = asunto, Html = viewHtml }, pdfBytes);
                    var mail = new MailAPI { Mail = usuario.UserName, Titulo = asunto, Html = viewHtml };
                    await _mailService.EnviarAsync(mail, pdfBytes);
                }
                    
                return Ok(new { Mensaje = "Resumen enviado correctamente.", NombreArchivo = nombreArchivo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ocurrió un error inesperado al procesar la solicitud." });
            }
        }


        [HttpPost("SolicitarPagoTarjeta")]
        public IActionResult SolicitarPagoTarjeta([FromBody] PagoTarjetaDTO pagotarjetaDTO)
        {
            try
            {
                ListaMovimientoTarjetaDTO movimientostarjetaDTOS = new ListaMovimientoTarjetaDTO();
                var usuario = TraeUsuarioUAT(pagotarjetaDTO.UAT);
                if (usuario == null)
                    return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"no existe UAT de Usuario" });

                DatosEstructura empresa = _context.DatosEstructura.FirstOrDefault();
                var movtarj = new MovPersona();

                var tipomov = _context.TipoMovimientoTarjeta.FirstOrDefault();
                var nuevosMovimientos = movtarj.ConsultarMovimientosTarjetas2(empresa.UsernameWS, empresa.PasswordWS, usuario.Personas.NroDocumento, Convert.ToInt64(pagotarjetaDTO.NroTarjeta), 100, 0);                
                var pers = _context.Personas.Where(x => x.NroTarjeta == usuario.Personas.NroTarjeta).FirstOrDefault();


                if (nuevosMovimientos.Detalle.Resultado == "EXITO")
                {
                    List<MovimientoTarjetaDTO> resultadoNuevos = new List<MovimientoTarjetaDTO>();
                    if (movimientostarjetaDTOS.tipomovimiento == 0)
                    {
                        resultadoNuevos = nuevosMovimientos.Movimientos
                        .GroupBy(m => new { m.Descripcion, m.Fecha })
                        .Select(g => new MovimientoTarjetaDTO
                        {
                            Monto =  (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ","))==null ? g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", "."))).ToString().Replace(".", ",") : (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ",")),
                            TipoMovimiento = g.Key.Descripcion,
                            Fecha = g.Key.Fecha.Date.ToString("dd/MM/yyyy")
                        })
                        .ToList();
                    }
                    else
                    {
                        resultadoNuevos = nuevosMovimientos.Movimientos.Where(x => x.Descripcion == tipomov.Nombre)
                        .GroupBy(m => new { m.Descripcion, m.Fecha })
                        .Select(g => new MovimientoTarjetaDTO
                        {
                            Monto =  (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ","))==null ? g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", "."))).ToString().Replace(".", ",") : (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ",")),
                            TipoMovimiento = g.Key.Descripcion,
                            Fecha = g.Key.Fecha.Date.ToString("dd/MM/yyyy")
                        })
                        .ToList();
                    }

                    List<ListDetalleCuotaDTO> listDetalle = new List<ListDetalleCuotaDTO>();

                    foreach (var item in nuevosMovimientos.DetallesSolicitud)
                    {
                        foreach (var itemMovimiento in item.DetallesCuota)
                        {
                            if (listDetalle.Any(x => x.Fecha == itemMovimiento.Fecha))
                            {
                                var detalle = listDetalle.Where(x => x.Fecha == itemMovimiento.Fecha).First();
                                detalle.Monto = (Convert.ToDecimal(detalle.Monto) + Convert.ToDecimal(itemMovimiento.Monto)).ToString();
                            }
                            else
                            {
                                listDetalle.Add(new ListDetalleCuotaDTO()
                                {
                                    Fecha = itemMovimiento.Fecha,
                                    Monto = itemMovimiento.Monto
                                });
                            }
                        }
                    }

                    string deudaTotal = "0.0";
                    string saldoVencidoAcumulado = "0.0";
                    string sumaProximoVencimientoAcumulado = "0.0";
                    string sumaTotalAcumulado = "0.0";
                    bool cuotaVencida = false;
                    string fechaSiguienteCuota = "";
                    DateTime? fechaProximoPago = null;
                    foreach (var item in listDetalle.OrderBy(x => x.Fecha))
                    {
                        if (VerificarVencimiento(item.Fecha))
                        {
                            SetearCultureInfoUS();
                            var aux1 = Convert.ToDecimal(item.Monto);
                            var aux2 = Convert.ToDecimal(saldoVencidoAcumulado);
                            saldoVencidoAcumulado = (aux1+aux2).ToString();
                            cuotaVencida = true;
                        }
                        else
                        {
                            
                            if (fechaSiguienteCuota=="")
                                fechaSiguienteCuota = item.Fecha;

                            if ((ConvertirFecha(item.Fecha).Month==(ConvertirFecha(fechaSiguienteCuota).Month)) && (ConvertirFecha(item.Fecha).Year==ConvertirFecha(fechaSiguienteCuota).Year))
                            {
                                if (fechaProximoPago==null)
                                    fechaProximoPago=ConvertirFecha(item.Fecha);

                                SetearCultureInfoUS();
                                var monto2 = Convert.ToDecimal(item.Monto);
                                var monto1 = Convert.ToDecimal(sumaProximoVencimientoAcumulado);
                                sumaProximoVencimientoAcumulado = (monto1+monto2).ToString();
                            }
                            SetearCultureInfoUS();
                            var monto3 = Convert.ToDecimal(item.Monto);
                            var monto4 = Convert.ToDecimal(deudaTotal);
                            deudaTotal = (monto3+monto4).ToString();
                        }

                    }
                    SetearCultureInfoUS();
                    sumaProximoVencimientoAcumulado = (Convert.ToDecimal(sumaProximoVencimientoAcumulado)+Convert.ToDecimal(saldoVencidoAcumulado)).ToString();
                    sumaTotalAcumulado = (Convert.ToDecimal(deudaTotal)+Convert.ToDecimal(saldoVencidoAcumulado)).ToString();

                    bool ContieneLeyenda = false;
                    string Leyenda = "";
                    var LeyendaTexto = _context.LeyendaTipoMovimiento.FirstOrDefault();

                    if (resultadoNuevos.Where(x => x.TipoMovimiento == LeyendaTexto.NombreMovimiento).Count()>0 && LeyendaTexto.Activo==true)
                    {
                        ContieneLeyenda = true;
                        Leyenda = LeyendaTexto.TextoLeyenda;
                    }

                    List<MovimientoTarjetaDTO> MovimientosTarjeta = nuevosMovimientos.Movimientos
                        .Select(mov => new MovimientoTarjetaDTO
                        {
                            TipoMovimiento = mov.Descripcion,
                            Monto = mov.Monto,
                            Fecha = mov.Fecha.ToString("dd/MM/yyyy")
                        })
                        .ToList();

                        int cantidadDeMovimientos = MovimientosTarjeta.Count();
                        if (cantidadDeMovimientos>movimientostarjetaDTOS.CantMovimientos)
                        {
                            cantidadDeMovimientos = Convert.ToInt32(movimientostarjetaDTOS.CantMovimientos);
                        }


                        var pagoTarjeta = new PagoTarjeta
                        {
                            NroTarjeta = pagotarjetaDTO.NroTarjeta,
                            //MontoAdeudado = Convert.ToDecimal(sumaTotalAcumulado.Replace(".", ",")),
                            MontoAdeudado = Convert.ToDecimal(sumaProximoVencimientoAcumulado),
                            FechaVencimiento = fechaProximoPago==null? DateTime.MinValue : (DateTime)fechaProximoPago,                         
                            Persona = pers, 
                            EstadoPago = EstadoPago.Pendiente,  
                            FechaComprobante = DateTime.Now, 
                            FechaPagoProximaCuota = fechaProximoPago==null ? DateTime.MinValue : (DateTime)fechaProximoPago,
                        };
                        //_context.PagoTarjeta.Add(pagoTarjeta);
                        //_context.SaveChanges();
                        pagotarjetaDTO.id = pagoTarjeta.Id;


                        return new JsonResult(new PagoTarjetaDTO { Status = 200, UAT = pagotarjetaDTO.UAT, Mensaje = "Solicitud de pago de tarjeta - SolicitarPagoTarjeta", NroTarjeta = pagotarjetaDTO.NroTarjeta.ToString(), Persona = pers.Id,  MontoAdeudado = pagoTarjeta.MontoAdeudado.ToString().Replace(".", ","), EstadoPago = EstadoPago.Pendiente,  FechaPagoProximaCuota = pagoTarjeta.FechaPagoProximaCuota?.ToString("dd/MM/yyyy"), FechaVencimiento=pagoTarjeta.FechaVencimiento?.ToString("dd/MM/yyyy"), FechaComprobante=pagoTarjeta.FechaComprobante?.ToString("dd/MM/yyyy"), id=pagotarjetaDTO.id, alias = empresa.Alias ,CBU=empresa.CBU });
                    }
                    else
                        return new JsonResult(new PagoTarjetaDTO { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = "Error a Solicitar pago de tarjeta - SolicitarPagoTarjeta" });

            }
            catch (Exception e)
            {
                Log.Error($"Error en creacion de tarjeta - {e.Message}");
                return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"Error en metodo SolicitarPagoTarjeta "+ e.Message });
                }

        }

        [HttpPost("SubirComprobantePagoTarjeta")]
        public async Task<IActionResult> SubirComprobantePagoTarjeta([FromBody] PagoTarjetaDTO pagotarjetaDTO)
        {
            try
            {
                if (pagotarjetaDTO.ComprobantePago!=null)
                {
                    decimal MontoCuota = 0;
                    decimal MontoProximaCuota = 0;
                    decimal MontoPunitorios = 0;
                    decimal DeudaTotal = 0;
                    decimal TotalRedondeo = 0;
                    decimal MontoDisponible = 0;

                    var usuario = TraeUsuarioUAT(pagotarjetaDTO.UAT);
                    if (usuario == null)
                        return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"no existe UAT de Usuario" });

                    ListaMovimientoTarjetaDTO movimientostarjetaDTOS = new ListaMovimientoTarjetaDTO();
                    DatosEstructura empresa = _context.DatosEstructura.FirstOrDefault();
                    var pers = _context.Personas.Where(x => x.NroTarjeta == usuario.Personas.NroTarjeta).FirstOrDefault();

                    //var fechaMesActualCuotas = DateTime.Now;
                    //int diasEnMes = DateTime.DaysInMonth(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month);

                    ////Fecha para Punitorios
                    //if (fechaMesActualCuotas.Day>15)
                    //{
                    //    DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
                    //}
                    //else
                    //{
                    //    DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, 15);
                    //}

                    //DateTime fechaActualCuotas = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);

                    ////Fecha para Punitorios
                    //if (fechaMesActualCuotas.Day>15)
                    //{
                    //    DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
                    //}
                    //else
                    //{
                    //    DateTime fechaPunitorios = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, 15);
                    //}                   

                    //var datosMovimientos = _datosServices.ConsultarMovimientos(empresa.UsernameWS.ToLower(), empresa.PasswordWS, usuario.Personas.NroDocumento, movimientostarjetaDTOS.NroTarjeta, 10, 0).Result;
                    //if (datosMovimientos.Detalle.Resultado=="EXITO")
                    //{
                    //    CultureInfo.CurrentCulture = new CultureInfo("es-AR");

                    //    //Monto Disponible
                    //    MontoDisponible = Math.Round(Convert.ToDecimal(datosMovimientos.Detalle.MontoDisponible.Replace(".", ",")), 2);

                    //    //Calcula Cuota del Mes
                    //    MontoCuota = await _datosServices.CalcularMontoCuota(datosMovimientos, fechaActualCuotas);

                    //    //Calculo de Punitorios
                    //    MontoPunitorios = await _datosServices.CalcularPunitorios(datosMovimientos.DetallesSolicitud);
                    //}

                    

                    //Calcula Deuda total suma la cuota mas los punitorios.
                    //DeudaTotal = MontoCuota + MontoPunitorios;
                    //TotalRedondeo = Math.Round(DeudaTotal, 2);
                    //var fechaVencimiento = new DateTime(fechaActualCuotas.Year, fechaActualCuotas.Month, 15);

                    var fechaPago = ConvertirFechaCompleta(pagotarjetaDTO.FechaComprobante);

                    var pagoTarjeta = new PagoTarjeta
                    {
                        NroTarjeta = pagotarjetaDTO.NroTarjeta,
                        MontoAdeudado = TotalRedondeo,         
                        Persona = pers,
                        EstadoPago = EstadoPago.Pagado,
                        FechaComprobante = DateTime.Now,
                        ComprobantePago = pagotarjetaDTO.ComprobantePago,
                        FechaDePago = fechaPago!=null? fechaPago : DateTime.Now,
                        MontoInformado = Convert.ToDecimal(pagotarjetaDTO.MontoInformado)

                    };
                        _context.PagoTarjeta.Add(pagoTarjeta);
                        _context.SaveChanges();
                        return new JsonResult(new PagoTarjetaDTO { Status = 200, UAT = pagotarjetaDTO.UAT, Mensaje = "Comprobante cargado con exito - SubirComprobantePagoTarjeta" });
                    }
                    else
                    {
                        return new JsonResult(new PagoTarjetaDTO { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = "Error al traer el Saldo Pendiente." });
                    }                 
            }
            catch (Exception e)
            {
                Log.Error($"Error en creacion de tarjeta - {e.Message}");
                return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"Error al subir comprobante - SubirComprobantePagoTarjeta "+ e.Message});
            }

        }


        [HttpPost("SubirComprobantePago")]
        public IActionResult SubirComprobantePago([FromBody] PagoTarjetaNewDTO pagotarjetaDTO)
        {
            try
            {
                if (pagotarjetaDTO.ComprobantePago!=null)
                //if (true)
                {
                    var usuario = TraeUsuarioUAT(pagotarjetaDTO.UAT);
                    if (usuario == null)
                        return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"no existe UAT de Usuario" });

                    ListaMovimientoTarjetaDTO movimientostarjetaDTOS = new ListaMovimientoTarjetaDTO();

                    DatosEstructura empresa = _context.DatosEstructura.FirstOrDefault();
                    var movtarj = new MovPersona();

                    var tipomov = _context.TipoMovimientoTarjeta.FirstOrDefault();
                    var nuevosMovimientos = movtarj.ConsultarMovimientosTarjetas2(empresa.UsernameWS, empresa.PasswordWS, usuario.Personas.NroDocumento, Convert.ToInt64(pagotarjetaDTO.NroTarjeta), 100, 0);
                    var pers = _context.Personas.Where(x => x.NroTarjeta == usuario.Personas.NroTarjeta).FirstOrDefault();


                    if (nuevosMovimientos.Detalle.Resultado == "EXITO")
                    {
                        List<MovimientoTarjetaDTO> resultadoNuevos = new List<MovimientoTarjetaDTO>();
                        if (movimientostarjetaDTOS.tipomovimiento == 0)
                        {
                            resultadoNuevos = nuevosMovimientos.Movimientos
                            .GroupBy(m => new { m.Descripcion, m.Fecha })
                            .Select(g => new MovimientoTarjetaDTO
                            {
                                Monto =  (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ","))==null ? g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", "."))).ToString().Replace(".", ",") : (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ",")),
                                TipoMovimiento = g.Key.Descripcion,
                                Fecha = g.Key.Fecha.Date.ToString("dd/MM/yyyy")
                            })
                            .ToList();
                        }
                        else
                        {
                            resultadoNuevos = nuevosMovimientos.Movimientos.Where(x => x.Descripcion == tipomov.Nombre)
                            .GroupBy(m => new { m.Descripcion, m.Fecha })
                            .Select(g => new MovimientoTarjetaDTO
                            {
                                Monto =  (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ","))==null ? g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", "."))).ToString().Replace(".", ",") : (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ",")),
                                TipoMovimiento = g.Key.Descripcion,
                                Fecha = g.Key.Fecha.Date.ToString("dd/MM/yyyy")
                            })
                            .ToList();
                        }

                        List<ListDetalleCuotaDTO> listDetalle = new List<ListDetalleCuotaDTO>();

                        foreach (var item in nuevosMovimientos.DetallesSolicitud)
                        {
                            foreach (var itemMovimiento in item.DetallesCuota)
                            {
                                if (listDetalle.Any(x => x.Fecha == itemMovimiento.Fecha))
                                {
                                    var detalle = listDetalle.Where(x => x.Fecha == itemMovimiento.Fecha).First();
                                    detalle.Monto = (Convert.ToDecimal(detalle.Monto) + Convert.ToDecimal(itemMovimiento.Monto)).ToString();
                                }
                                else
                                {
                                    listDetalle.Add(new ListDetalleCuotaDTO()
                                    {
                                        Fecha = itemMovimiento.Fecha,
                                        Monto = itemMovimiento.Monto
                                    });
                                }
                            }
                        }

                        string deudaTotal = "0.0";
                        string saldoVencidoAcumulado = "0.0";
                        string sumaProximoVencimientoAcumulado = "0.0";
                        string sumaTotalAcumulado = "0.0";
                        bool cuotaVencida = false;
                        string fechaSiguienteCuota = "";
                        DateTime? fechaProximoPago = null;
                        foreach (var item in listDetalle.OrderBy(x => x.Fecha))
                        {
                            if (VerificarVencimiento(item.Fecha))
                            {
                                SetearCultureInfoUS();
                                var aux1 = Convert.ToDecimal(item.Monto);
                                var aux2 = Convert.ToDecimal(saldoVencidoAcumulado);
                                saldoVencidoAcumulado = (aux1+aux2).ToString();
                                cuotaVencida = true;
                            }
                            else
                            {

                                if (fechaSiguienteCuota=="")
                                    fechaSiguienteCuota = item.Fecha;

                                if ((ConvertirFecha(item.Fecha).Month==(ConvertirFecha(fechaSiguienteCuota).Month)) && (ConvertirFecha(item.Fecha).Year==ConvertirFecha(fechaSiguienteCuota).Year))
                                {
                                    if (fechaProximoPago==null)
                                        fechaProximoPago=ConvertirFecha(item.Fecha);

                                    SetearCultureInfoUS();
                                    var monto2 = Convert.ToDecimal(item.Monto);
                                    var monto1 = Convert.ToDecimal(sumaProximoVencimientoAcumulado);
                                    sumaProximoVencimientoAcumulado = (monto1+monto2).ToString();
                                }
                                SetearCultureInfoUS();
                                var monto3 = Convert.ToDecimal(item.Monto);
                                var monto4 = Convert.ToDecimal(deudaTotal);
                                deudaTotal = (monto3+monto4).ToString();
                            }

                        }
                        SetearCultureInfoUS();
                        sumaProximoVencimientoAcumulado = (Convert.ToDecimal(sumaProximoVencimientoAcumulado)+Convert.ToDecimal(saldoVencidoAcumulado)).ToString();
                        sumaTotalAcumulado = (Convert.ToDecimal(deudaTotal)+Convert.ToDecimal(saldoVencidoAcumulado)).ToString();

                        bool ContieneLeyenda = false;
                        string Leyenda = "";
                        var LeyendaTexto = _context.LeyendaTipoMovimiento.FirstOrDefault();

                        if (resultadoNuevos.Where(x => x.TipoMovimiento == LeyendaTexto.NombreMovimiento).Count()>0 && LeyendaTexto.Activo==true)
                        {
                            ContieneLeyenda = true;
                            Leyenda = LeyendaTexto.TextoLeyenda;
                        }

                        List<MovimientoTarjetaDTO> MovimientosTarjeta = nuevosMovimientos.Movimientos
                            .Select(mov => new MovimientoTarjetaDTO
                            {
                                TipoMovimiento = mov.Descripcion,
                                Monto = mov.Monto,
                                Fecha = mov.Fecha.ToString("dd/MM/yyyy")
                            })
                            .ToList();

                        int cantidadDeMovimientos = MovimientosTarjeta.Count();
                        if (cantidadDeMovimientos>movimientostarjetaDTOS.CantMovimientos)
                        {
                            cantidadDeMovimientos = Convert.ToInt32(movimientostarjetaDTOS.CantMovimientos);
                        }
                        DateTime fechaDateTimePago = DateTime.ParseExact(pagotarjetaDTO.FechaComprobante, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        var pagoTarjeta = new PagoTarjeta
                        {
                            NroTarjeta = pagotarjetaDTO.NroTarjeta,
                            //MontoAdeudado = Convert.ToDecimal(sumaTotalAcumulado.Replace(".", ",")),
                            MontoAdeudado = Convert.ToDecimal(sumaProximoVencimientoAcumulado),
                            FechaVencimiento = fechaProximoPago==null ? DateTime.MinValue : (DateTime)fechaProximoPago,
                            Persona = pers,
                            EstadoPago = EstadoPago.Pagado,
                            FechaComprobante = DateTime.Now,
                            FechaPagoProximaCuota = fechaProximoPago==null ? DateTime.MinValue : (DateTime)fechaProximoPago,
                            ComprobantePago = pagotarjetaDTO.ComprobantePago,
                            FechaDePago = fechaDateTimePago,
                            MontoInformado = Convert.ToDecimal(pagotarjetaDTO.MontoInformado)

                        };
                        _context.PagoTarjeta.Add(pagoTarjeta);
                        _context.SaveChanges();
                        return new JsonResult(new PagoTarjetaDTO { Status = 200, UAT = pagotarjetaDTO.UAT, Mensaje = "Comprobante cargado con exito - SubirComprobantePago" });
                    }
                    else
                    {
                        return new JsonResult(new PagoTarjetaDTO { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = "Error al traer el Saldo Pendiente." });
                    }
                }
                else
                {
                    return new JsonResult(new PagoTarjetaDTO { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = "No hay comprobante cargado" });
                }

                //else
                //    return new JsonResult(new PagoTarjetaDTO { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = "No tiene pagos pendientes para subir archivo" });

            }
            catch (Exception e)
            {
                Log.Error($"Error en creacion de tarjeta - {e.Message}");
                return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"Error al subir comprobante SubirComprobantePago" + e.Message});
            }

        }


        [HttpPost("ObtenerComprobantes")]
		public IActionResult ObtenerComprobantes([FromBody] ComprobantesDTO pagotarjetaDTO)
		{
			try
			{
				var usuario = TraeUsuarioUAT(pagotarjetaDTO.UAT);
                var personaId = 0;
				if (usuario == null)
					return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"no existe UAT de Usuario" });

                if (usuario.Personas!=null)
                {
					personaId=usuario.Personas.Id;
				}else if (usuario.Clientes!=null)
                {
                    if (usuario.Clientes.Persona!=null)
                    {
						personaId=usuario.Clientes.Persona.Id;
					}
                }
                else
                {
					return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"Error al encontrar los comprobantes" });
				}


				var comprobantes = _context.PagoTarjeta.Where(x => x.Persona.Id == personaId).ToList();

                if (comprobantes.Count()>0)
                {
                    pagotarjetaDTO.ListComprobantes = new List<ListComprobantesDTO>();

                    foreach (var item in comprobantes)
                    {
                        pagotarjetaDTO.ListComprobantes.Add(new ListComprobantesDTO()
                        {
                            NroTarjeta = item.NroTarjeta,
                            FechaVencimiento = item.FechaVencimiento.ToString(),
                            MontoAdeudado = item.MontoAdeudado.ToString(),
                            FechaPagoProximaCuota = item.FechaPagoProximaCuota.ToString(),
                            EstadoPago = item.EstadoPago,
                            EstadoPagoDescripcion = item.EstadoPago.GetType().GetField(item.EstadoPago.ToString()).Name,
                            ComprobantePago = item.ComprobantePago,
                            FechaComprobante = item.FechaComprobante.ToString(),
                            Observacion = item.Observacion,
                        });
                        pagotarjetaDTO.Mensaje = "Listado de Comprobantes";
                        pagotarjetaDTO.Status = 200;
                        pagotarjetaDTO.PersonaId = personaId;

					}
					return new JsonResult(pagotarjetaDTO);
				}
                else
                {
                    return new JsonResult(new PagoTarjetaDTO { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = "No hay comprobante cargado" });

                }

			}
			catch (Exception e)
			{
				Log.Error($"Error en creacion de tarjeta - {e.Message}");
				return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"Error al obtener los comprobantes" });
			}

		}

		[HttpPost("TraeDetalleSolicitudPago")]
        public IActionResult TraeDetalleSolicitudPago([FromBody] PagoTarjetaDTO pagotarjetaDTO)
        {
            try
            {
                var usuario = TraeUsuarioUAT(pagotarjetaDTO.UAT);
                if (usuario == null)
                    return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"no existe UAT de Usuario" });

                var pagotarjeta = _context.PagoTarjeta.Where(x => x.Id == pagotarjetaDTO.id && x.EstadoPago == EstadoPago.Pagado).FirstOrDefault();

                if (pagotarjeta != null)
                {
                    if (pagotarjeta.ComprobantePago != null)
                    {
                        
                        return new JsonResult(new PagoTarjetaDTO { Status = 200, UAT = pagotarjetaDTO.UAT, Mensaje = "Comprobante cargado con exito" , ComprobantePago = pagotarjeta.ComprobantePago, MontoAdeudado = pagotarjeta.MontoAdeudado.ToString(), FechaVencimiento= pagotarjeta.FechaVencimiento.ToString(), FechaPagoProximaCuota =pagotarjeta.FechaPagoProximaCuota.ToString(), EstadoPago = pagotarjeta.EstadoPago , Persona = pagotarjeta.Persona.Id , NroTarjeta = pagotarjeta.NroTarjeta});
                    }
                    else
                    {
                        return new JsonResult(new PagoTarjetaDTO { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = "No hay comprobante cargado" });

                    }
                }
                else
                    return new JsonResult(new PagoTarjetaDTO { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = "No tiene pagos pendientes para subir archivo" });

            }
            catch (Exception e)
            {
                Log.Error($"Error en creacion de tarjeta - {e.Message}");
                return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"Error al subir comprobante" });
            }

        }
     
        [HttpPost("RegistrarPago")]
        public async Task<IActionResult> RegistrarPago([FromBody] MConciliacionDePagoDTO pagotarjetaDTO)
        {
            try
            {
                var usuario = TraeUsuarioUAT(pagotarjetaDTO.UAT);
                if (usuario == null)
                    return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"no existe UAT de Usuario" });

                Payment payment = await _mp.GetPago(pagotarjetaDTO.MercadoPagoId);
                if (payment==null)
                {
                    return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"No se pudo guardar el pago" });

                }
                ConciliacionDePago conciliacionDePago = new ConciliacionDePago
                {
                    Fecha = DateTime.Now,
                    Monto = Convert.ToDecimal(payment.TransactionAmount),
                    Usuario = usuario,
                    MercadoPagoId = pagotarjetaDTO.MercadoPagoId.ToString(),
                    Descripcion = payment.Description
                };
                conciliacionDePago.SetEstado(payment.Status);

                _context.ConciliacionDePago.Add(conciliacionDePago);
                _context.SaveChanges();
                return new JsonResult(new RespuestaAPI { Status = 200, UAT = pagotarjetaDTO.UAT, Mensaje = $"Se guardo el Pago correctamente" });
            }
            catch (Exception e)
            {
                Log.Error($"Error en creacion de tarjeta - {e.Message}");
                return new JsonResult(new RespuestaAPI { Status = 500, UAT = pagotarjetaDTO.UAT, Mensaje = $"Error al obtener los comprobantes" });
            }
        }


        [HttpPost("Solicitar")]
        [OmitirChequeaUat]
        public async Task<IActionResult> Solicitar([FromBody] DAL.DTOs.API.SolicitarTarjetaDTO solicitudDTO)
        {
            try
            {
                SolicitudDeTarjeta solicitudDeTarjeta = new SolicitudDeTarjeta
                {
                    Nombre = solicitudDTO.Nombre,
                    Apellido = solicitudDTO.Apellido,
                    DNI = solicitudDTO.DNI,
                    Email = solicitudDTO.Email,
                    FechaNacimiento = solicitudDTO.FechaNacimiento,
                    Calle = solicitudDTO.Calle,
                    Altura = solicitudDTO.Altura,
                    PisoDepto = solicitudDTO.PisoDepto,
                    CodigoPostal = solicitudDTO.CodigoPostal,
                    Localidad = _context.Localidad.Find(solicitudDTO.LocalidadId),
                    Provincia = _context.Provincia.Find(solicitudDTO.ProvinciaId),
                    FechaSolicitud = DateTime.Now,
                    FrenteDNI = solicitudDTO.FrenteDNI,
                    DorsoDNI = solicitudDTO.DorsoDNI,
                    Selfie = solicitudDTO.Selfie,
                    Estado = _context.EstadoSolicitudDeTarjeta.Where(e => e.Id == 1).FirstOrDefault()
                };

                _context.SolicitudDeTarjeta.Add(solicitudDeTarjeta);              
                await _context.SaveChangesAsync();

                return new JsonResult(new RespuestaAPI { Status = 200, Mensaje = "Tarjeta solicitada con exito" });
            }
            catch (Exception e)
            {
                Log.Error($"Error en creacion de tarjeta - {e.Message}");
                return new JsonResult(new RespuestaAPI { Status = 500, Mensaje = $"Error al solicitar la tarjeta" });
            }

        }


        private bool VerificarVencimiento(string fecha)
        {
            SetearCultureInfoES();
            // Obtener la fecha actual
            DateTime fechaActual = DateTime.Now;
            //DateTime fechaActual = DateTime.Today;            			

			DateTime fechaIngresada;

            var periodoActual = _context.Periodo
                .FirstOrDefault(p => fechaActual >= p.FechaDesde && fechaActual <= p.FechaHasta);


            if (DateTime.TryParse(fecha, out fechaIngresada))
			{

                if (fechaIngresada<=periodoActual.FechaHasta)
                {
                    return true;
                }
                return false;


                // Comparar la fecha ingresada con la fecha actual
                if (fechaActual>fechaIngresada)
				{
                    return true;
				}
				return false;
			}
			else
			{
				return false;
			}
		}

        private DateTime ConvertirFecha(string fecha)
        {
            SetearCultureInfoES();
            DateTime fechaIngresada;
            if (DateTime.TryParse(fecha, out fechaIngresada))
            {
                return fechaIngresada;
            }
            else
            {
                return fechaIngresada;
            }
        }

        private DateTime FechaActual()
        {
            SetearCultureInfoES();
            DateTime fecha = DateTime.Now.Date;
            return fecha;
        }

        private DateTime FechaActual2()
        {
            string fecha = DateTime.Now.ToString();  
            DateTime fechaParseada;
            if (DateTime.TryParse(fecha, out fechaParseada))
            {
                return fechaParseada;
            }
            else
            {
                return fechaParseada;
            }
        }

        private void SetearCultureInfoES()
		{
            CultureInfo cultura = new CultureInfo("es-ES");
            CultureInfo.CurrentCulture = cultura;
            CultureInfo.CurrentUICulture = cultura;
        }

        private void SetearCultureInfoUS()
        {
            CultureInfo cultura = new CultureInfo("en-US");
            CultureInfo.CurrentCulture = cultura;
            CultureInfo.CurrentUICulture = cultura;
        }

        static string InvertirFecha(string fechaOriginal)
        {
            // Dividir la cadena por el carácter "-"
            string[] partes = fechaOriginal.Split('-');

            // Reconstruir la cadena en el orden deseado
            string fechaReversa = partes[2] + "-" + partes[1] + "-" + partes[0];

            return fechaReversa;
        }

        private string ConvertirNumeroAMes(int numeroMes)
        {
            CultureInfo culturaAR = new CultureInfo("es-AR");
            return culturaAR.DateTimeFormat.GetMonthName(numeroMes);
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
                textoModificado = textoModificado.Replace("TextoMesEscritoReemplazar", mesNombre);

                return textoModificado;
            }
        }
 
    }
}
