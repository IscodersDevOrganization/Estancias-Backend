using DAL.Data;
using DAL.DTOs;
using DAL.DTOs.ApiCpeCreditos;
using DAL.DTOs.Servicios;
using DAL.DTOs.Servicios.DatosTarjeta;
using DAL.Mobile;
using DAL.Models;
using EstanciasCore.API.Filters;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Areas.Core.Controllers
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/movimientos-tarjeta")]

    public class MovimientoTarjetaEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;
        private readonly IDatosTarjetaService _datosServices;

        public MovimientoTarjetaEndpointController(EstanciasContext context, IDatosTarjetaService datosServices)
        {
            _context = context;
            _datosServices = datosServices;
        }

        // Mantener los permisos y la autenticación del módulo al integrar este archivo.
        // POST /endpoint/movimientos-tarjeta/buscar
        // JSON: { "busqueda": "30123456" }
        [HttpPost("buscar")]
        public async Task<IActionResult> Buscar([FromBody] BuscarMovimientosTarjetaRequest model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            string busqueda = model?.Busqueda;

            decimal MontoCuota = 0;
            decimal MontoProximaCuota = 0;
            decimal MontoPunitorios = 0;
            decimal DeudaTotal = 0;
            decimal MontoDisponible = 0;
            List<MovimientoTarjetaDTO> comprasAgrupadas;

            var fechaMesActualCuotas = DateTime.Now;
            int diasEnMes = DateTime.DaysInMonth(fechaMesActualCuotas.Year, fechaMesActualCuotas.Month);

            if (string.IsNullOrWhiteSpace(busqueda))
            {
                return BadRequest(new { mensaje = "Ingresá un DNI o número de tarjeta para buscar." });
            }

            var culturaOriginal = CultureInfo.CurrentCulture;
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
                    personaLocal = _context.Personas.FirstOrDefault(p =>
                        p.NroTarjeta == busquedaTrimmed || p.NroTarjeta == busquedaSinCeros);

                    // TrimStart no se traduce a SQL en todos los proveedores/versiones.
                    // Usar este recorrido sólo si no hubo coincidencia exacta.
                    if (personaLocal == null)
                    {
                        personaLocal = _context.Personas
                            .Where(p => p.NroTarjeta != null)
                            .AsEnumerable()
                            .FirstOrDefault(p => p.NroTarjeta.TrimStart('0') == busquedaSinCeros);
                    }
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
                    return StatusCode(500, new { mensaje = "No se encontraron los datos de configuración de la empresa." });
                }

                if (string.IsNullOrWhiteSpace(empresa.UsernameWS) || string.IsNullOrWhiteSpace(empresa.PasswordWS))
                    return StatusCode(500, new { mensaje = "Falta configurar el usuario o la contraseña del servicio LOAN." });

                var datosMovimientos = await _datosServices.ConsultarMovimientos(empresa.UsernameWS.ToLower(), empresa.PasswordWS, nroDocumento, nroTarjeta, 100, 0);

                if (datosMovimientos == null || datosMovimientos.Detalle == null)
                    return StatusCode(502, new { mensaje = "LOAN no devolvió una respuesta válida." });

                if (datosMovimientos.Detalle.Resultado != "EXITO")
                    return StatusCode(502, new { mensaje = "LOAN no pudo completar la consulta de movimientos." });

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

                // Misma fuente y mismo criterio que el resumen de tarjeta (DatosTarjetaService)
                ResponseObtenerConsultaDTO montosConPunitorios = await _datosServices.ObtenerConsultaCobranzasAsync(nroDocumento);

                decimal totalDeudaDec = DatosTarjetaService.CobranzasDelSaldo(montosConPunitorios, DateTime.Today)
                    .Sum(x => Convert.ToDecimal(x.importe));
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

                return Ok(new
                {
                    datos = resultadoModel,
                    montoCuota = MontoCuota.ToString("N2", new CultureInfo("es-AR")),
                    montoPunitorios = MontoPunitorios.ToString("N2", new CultureInfo("es-AR")),
                    deudaTotal = DeudaTotal.ToString("N2", new CultureInfo("es-AR")),
                    datosMovimientosRaw = datosMovimientos,
                    montosConPunitorios = montosConPunitorios
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error en Buscar Movimientos Tarjeta LOAN");
                return StatusCode(500, new { mensaje = "Ocurrió un error al buscar los movimientos." });
            }
            finally
            {
                CultureInfo.CurrentCulture = culturaOriginal;
            }
        }
    }

    public class BuscarMovimientosTarjetaRequest
    {
        public string Busqueda { get; set; }
    }

}
