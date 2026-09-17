using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DAL.Data;
using DAL.DTOs;
using DAL.DTOs.ApiCpeCreditos;
using DAL.DTOs.Servicios;
using DAL.DTOs.Servicios.DatosTarjeta;
using DAL.Mobile;
using DAL.Models;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace EstanciasCore.Areas.Core.Controllers
{
    [Area("Core")]
    public class MovimientoTarjetaController : Controller
    {
        private readonly EstanciasContext _context;
        private readonly IDatosTarjetaService _datosServices;

        public MovimientoTarjetaController(EstanciasContext context, IDatosTarjetaService datosServices)
        {
            _context = context;
            _datosServices = datosServices;
        }

        // GET: Core/MovimientoTarjeta
        public IActionResult Index()
        {
            return View();
        }

        // POST: Core/MovimientoTarjeta/Buscar
        [HttpPost]
        public async Task<IActionResult> Buscar(string busqueda)
        {

            decimal MontoCuota = 0;
            decimal MontoProximaCuota = 0;
            decimal MontoPunitorios = 0;
            decimal DeudaTotal = 0;
            decimal MontoDisponible = 0;
            string totalDeuda = "0";
            List<MovimientoTarjetaDTO> comprasAgrupadas;

            var fechaMesActualCuotas = DateTime.Now;
            int diasEnMes = DateTime.DaysInMonth(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month);

            if (string.IsNullOrWhiteSpace(busqueda))
            {
                ViewBag.Error = "Por favor ingrese un DNI o Número de Tarjeta para buscar.";
                return PartialView("_ResultadoMovimientos", null);
            }

            try
            {
                string busquedaTrimmed = busqueda.Trim();
                string busquedaSinCeros = busquedaTrimmed.TrimStart('0');
                if (string.IsNullOrEmpty(busquedaSinCeros))
                {
                    busquedaSinCeros = busquedaTrimmed;
                }

                string nroDocumento = "";
                long nroTarjeta = 0;
                DAL.Models.Persona personaLocal = null;
                PersonaLoan personaLoan = null;

                // 1. Intentar buscar en DB local (_context.Personas)
                personaLocal = _context.Personas.FirstOrDefault(p => p.NroDocumento == busquedaTrimmed || p.NroDocumento == busquedaSinCeros);

                if (personaLocal == null)
                {
                    personaLocal = _context.Personas.FirstOrDefault(p => p.NroTarjeta != null && 
                        (p.NroTarjeta == busquedaTrimmed || 
                         p.NroTarjeta.TrimStart('0') == busquedaSinCeros || 
                         busquedaTrimmed.TrimStart('0') == p.NroTarjeta.TrimStart('0')));
                }

                if (personaLocal != null)
                {
                    nroDocumento = personaLocal.NroDocumento;
                    if (!string.IsNullOrEmpty(personaLocal.NroTarjeta))
                    {
                        long.TryParse(personaLocal.NroTarjeta.TrimStart('0'), out nroTarjeta);
                    }
                }

                // 2. Consultar BDExternaPersonalService (LOAN DB MySQL) si faltan datos
                BDExternaPersonalService bdExterna = new BDExternaPersonalService(_context);

                if (string.IsNullOrEmpty(nroDocumento) || nroTarjeta == 0)
                {
                    var loanByCard = bdExterna.getPersonaloanByNroTarjeta(busquedaTrimmed);
                    if (loanByCard != null && loanByCard.Count > 0)
                    {
                        personaLoan = loanByCard.FirstOrDefault();
                        if (string.IsNullOrEmpty(nroDocumento) && !string.IsNullOrEmpty(personaLoan.NroDocumento))
                        {
                            nroDocumento = personaLoan.NroDocumento;
                        }
                        if (nroTarjeta == 0 && !string.IsNullOrEmpty(personaLoan.NroTarjeta))
                        {
                            long.TryParse(personaLoan.NroTarjeta.TrimStart('0'), out nroTarjeta);
                        }
                    }
                }

                if (string.IsNullOrEmpty(nroDocumento) || nroTarjeta == 0)
                {
                    var loanByDni = bdExterna.getPersonaloan(busquedaTrimmed);
                    if (loanByDni == null || loanByDni.Count == 0)
                    {
                        loanByDni = bdExterna.getPersonaloan(busquedaSinCeros);
                    }

                    if (loanByDni != null && loanByDni.Count > 0)
                    {
                        personaLoan = loanByDni.FirstOrDefault();
                        if (string.IsNullOrEmpty(nroDocumento) && !string.IsNullOrEmpty(personaLoan.NroDocumento))
                        {
                            nroDocumento = personaLoan.NroDocumento;
                        }
                        if (nroTarjeta == 0 && !string.IsNullOrEmpty(personaLoan.NroTarjeta))
                        {
                            long.TryParse(personaLoan.NroTarjeta.TrimStart('0'), out nroTarjeta);
                        }
                    }
                }

                // Fallbacks si aún falta DNI o Tarjeta
                if (string.IsNullOrEmpty(nroDocumento))
                {
                    nroDocumento = busquedaSinCeros;
                }
                if (nroTarjeta == 0 && long.TryParse(busquedaSinCeros, out long tarjetaDirecta))
                {
                    nroTarjeta = tarjetaDirecta;
                }

                // 3. Consultar Servicio SOAP LOAN a través de _datosServices
                DatosEstructura empresa = _context.DatosEstructura.FirstOrDefault();
                if (empresa == null)
                {
                    ViewBag.Error = "No se encontraron los datos de configuración de la empresa.";
                    return PartialView("_ResultadoMovimientos", null);
                }

                var datosMovimientos = await _datosServices.ConsultarMovimientos(empresa.UsernameWS.ToLower(), empresa.PasswordWS, nroDocumento, nroTarjeta, 100, 0);

                if (datosMovimientos.Detalle.Resultado == "EXITO")
                {
                    //    string msg = datosMovimientos?.Detalle?.Mensaje ?? "No se encontraron movimientos para los datos ingresados.";
                    //    ViewBag.Error = $"Error LOAN: {msg}";
                    //    return PartialView("_ResultadoMovimientos", null);
               

                    // 4. Calcular datos idéntico a /api/MTarjetas/MovimientoTarjeta
                


                    DateTime fechaActualCuotas = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, diasEnMes);
                    DateTime fechaActualCuotasProximo = fechaActualCuotas.AddMonths(1);

                    CultureInfo.CurrentCulture = new CultureInfo("es-AR");

                    if (!string.IsNullOrEmpty(datosMovimientos.Detalle.MontoDisponible))
                    {
                        decimal.TryParse(datosMovimientos.Detalle.MontoDisponible.Replace(".", ","), out decimal disp);
                        MontoDisponible = Math.Round(disp, 2);
                    }

                    MontoCuota = await _datosServices.CalcularMontoCuota(datosMovimientos, fechaActualCuotas);
                    MontoProximaCuota = await _datosServices.CalcularMontoProximaCuota(datosMovimientos, fechaActualCuotasProximo);
                    MontoPunitorios = await _datosServices.CalcularPunitorios(datosMovimientos.DetallesSolicitud);
                }

                ResponseObtenerConsultaDTO montosConPunitorios = null;
                string letraSexo = "F";
                var datosPersona = await _datosServices.ObtenerPersona(nroDocumento);
                if (datosPersona != null && datosPersona.Persona != null && datosPersona.Persona.Sexo != null)
                {
                    if (datosPersona.Persona.Sexo.Id == 2)
                    {
                        letraSexo = "M";
                    }
                }

                montosConPunitorios = await _datosServices.ObtenerConsulta(nroDocumento, letraSexo);
                if ((montosConPunitorios == null || montosConPunitorios.cobranzas == null || !montosConPunitorios.cobranzas.Any()) && letraSexo == "F")
                {
                    montosConPunitorios = await _datosServices.ObtenerConsulta(nroDocumento, "M");
                }

                decimal totalDeudaDec = 0;

                if (montosConPunitorios != null && montosConPunitorios.cobranzas != null && montosConPunitorios.cobranzas.Any())
                {
                    DateTime hoy = DateTime.Today;
                    int diasEnElMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
                    DateTime fechaActual = new DateTime(hoy.Year, hoy.Month, diasEnElMes);

                    // Sumamos directamente convirtiendo a decimal (si importe no era decimal)
                    totalDeudaDec = montosConPunitorios.cobranzas
                        .Where(x => x.fechaVencimiento.Date <= fechaActual)
                        .Sum(x => Convert.ToDecimal(x.importe));
                }
                if (datosMovimientos.Movimientos != null)
                {
                    comprasAgrupadas = await _datosServices.ObtieneUltimosMovimientos(datosMovimientos, 20);
                }
                else
                {
                    comprasAgrupadas = new List<MovimientoTarjetaDTO>();
                }
                DeudaTotal = MontoCuota + MontoPunitorios;

                var fechaVencimiento = new DateTime(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month, 10);

                string nombreTitular = datosMovimientos.Detalle.Nombre;
                if (personaLocal != null)
                {
                    nombreTitular = personaLocal.GetNombreCompleto();
                }
                else if (personaLoan != null)
                {
                    nombreTitular = $"{personaLoan.Apellido} {personaLoan.Nombres}".Trim();
                }

                string montoAdeudadoFormat = totalDeudaDec.ToString("N2", new CultureInfo("es-AR"));

                var resultadoModel = new ListaMovimientoTarjetaDTO
                {
                    Status = 200,
                    Resultado = "Exito",
                    NroTarjeta = nroTarjeta,
                    Nombre = nombreTitular,
                    NroDocumento = long.TryParse(nroDocumento, out long docParsed) ? docParsed : 0,
                    Direccion = datosMovimientos.Detalle.Direccion,
                    MontoAdeudado = montoAdeudadoFormat,
                    ProximaFechaPago = fechaVencimiento.ToString("dd/MM/yyyy"),
                    CuotaVencida = true,
                    TotalProximaCuota = MontoProximaCuota.ToString("N2", new CultureInfo("es-AR")),
                    FechaPagoProximaCuota = fechaVencimiento.AddMonths(1).ToString("dd/MM/yyyy"),
                    MontoDisponible = MontoDisponible.ToString("N2", new CultureInfo("es-AR")),
                    CantMovimientos = comprasAgrupadas != null ? comprasAgrupadas.Count() : 0,
                    MovimientosTarjeta = comprasAgrupadas
                };

                ViewBag.MontoCuota = MontoCuota.ToString("N2", new CultureInfo("es-AR"));
                ViewBag.MontoPunitorios = MontoPunitorios.ToString("N2", new CultureInfo("es-AR"));
                ViewBag.DeudaTotal = DeudaTotal.ToString("N2", new CultureInfo("es-AR"));
                ViewBag.DatosMovimientosRaw = datosMovimientos;
                ViewBag.MontosConPunitorios = montosConPunitorios;

                return PartialView("_ResultadoMovimientos", resultadoModel);
            }
            catch (Exception ex)
            {
                Log.Error($"Error en Buscar Movimientos Tarjeta LOAN: {ex.Message}", ex);
                ViewBag.Error = $"Ocurrió un error al buscar movimientos: {ex.Message}";
                return PartialView("_ResultadoMovimientos", null);
            }
        }
    }
}
