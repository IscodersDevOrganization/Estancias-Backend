using DAL.Data;
using DAL.DTOs.ApiCpeCreditos;
using DAL.DTOs.Reportes;
using DAL.DTOs.Servicios;
using DAL.Mobile;
using DAL.Models;
using EstanciasCore.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System;  
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using static EstanciasCore.Services.DatosTarjetaService;
namespace EstanciasCore.Services
{
    public class DatosTarjetaService : IDatosTarjetaService
    {
        private EstanciasContext _context { get; set; }
        private readonly IRazorViewEngine _razorViewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider; 
        private readonly ILogger<DatosTarjetaService> _logger; 
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl = "https://sistema.cpecreditos.com.ar/Loan/ServiciosAPI/";

        // Tasa Nominal Anual (TNA) del 98% (Persistir valor en la Base de Datos)
        private const decimal TNA = 0.98m;


        public DatosTarjetaService(IConfiguration configuration, EstanciasContext context, IRazorViewEngine razorViewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider, ILogger<DatosTarjetaService> logger)
        {
            _context=context;
            _razorViewEngine = razorViewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        private async Task<string> ObtenerDatos(string usuario, string clave, long documento, long numeroTarjeta, long cantidadMovimientos)
        {
            string soapRequest =
               $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    <TarjetaRequest xmlns=""http://tempuri.org/"">
                      <TarjetaRequest>
                        <usuario>{usuario}</usuario>
                        <clave>{clave}</clave>
                        <documento>{documento}</documento>
                        <numeroTarjeta>{numeroTarjeta}</numeroTarjeta>
                        <cantidadMovimientos>{cantidadMovimientos}</cantidadMovimientos>
                      </TarjetaRequest>
                    </TarjetaRequest>
                  </soap:Body>
                </soap:Envelope>";

            string url = "http://sistema.cpecreditos.com.ar/Loan/ServiciosWeb/TarjetaWebService.asmx";
            string action = "http://tempuri.org/TarjetaObtenerDatos";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add("SOAPAction", action);
            request.ContentType = "text/xml; charset=utf-8";
            request.Method = "POST";

            using (Stream stream = await request.GetRequestStreamAsync())
            {
                byte[] data = Encoding.UTF8.GetBytes(soapRequest);
                await stream.WriteAsync(data, 0, data.Length);
            }

            string soapResult;
            using (WebResponse response = await request.GetResponseAsync())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    soapResult = await reader.ReadToEndAsync();
                }
            }

            return soapResult;
        }

        /// <summary>
        /// Deja solo los dígitos 0-9 del documento (saca puntos, espacios, espacios duros y caracteres invisibles).
        /// Devuelve null si no queda un número válido.
        /// </summary>
        public static string NormalizarDocumento(string documento)
        {
            if (string.IsNullOrEmpty(documento)) return null;
            var digitos = new string(documento.Where(c => c >= '0' && c <= '9').ToArray()).TrimStart('0');
            return (digitos.Length == 0 || digitos.Length > 9) ? null : digitos;
        }

        /// <summary>Describe los caracteres no numéricos de un documento (para el mensaje de error).</summary>
        public static string DescribirCaracteresInvalidos(string documento)
        {
            if (string.IsNullOrEmpty(documento)) return "el documento está vacío";
            var raros = documento.Where(c => c < '0' || c > '9').Distinct()
                .Select(c => char.IsWhiteSpace(c) || char.IsControl(c) || c > 126 ? $"U+{(int)c:X4}" : $"'{c}'")
                .ToList();
            return raros.Any() ? "contiene caracteres no numéricos: " + string.Join(", ", raros) : $"tiene {documento.Length} dígitos";
        }

        public async Task<CombinedData> ConsultarMovimientos(string usuario, string clave, string documento, long numeroTarjeta, long cantidadMovimientos, int tipomovimientotarjeta)
        {
            var cliente = new TarjetaObtenerDatosClient();

            var documentoLimpio = NormalizarDocumento(documento);
            if (documentoLimpio == null)
                throw new FormatException($"Documento inválido '{documento}': {DescribirCaracteresInvalidos(documento)}.");

            string soapResponse = await ObtenerDatos(usuario, clave, Convert.ToInt32(documentoLimpio), numeroTarjeta, cantidadMovimientos);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(soapResponse);

            XmlNodeList DatosNodes = xmlDoc.GetElementsByTagName("TarjetaObtenerDatosResult");
            ListaMovimientoTarjetaDTO datos = new ListaMovimientoTarjetaDTO();

            XDocument doc = XDocument.Parse(soapResponse);
            XNamespace ns = "http://tempuri.org/";

            #region Guardar Archivo XML (opcional)
            ////------------------------------------//
            //// --- Para guardar el archivo de forma segura ---

            //// 1. Obtiene la ruta a la carpeta del Escritorio del usuario actual.
            //string rutaEscritorio = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            //// 2. Define el nombre del archivo.
            //string nombreArchivo = "respuesta_tarjeta.xml";

            //// 3. Combina la ruta del escritorio y el nombre del archivo. 
            ////    Path.Combine se asegura de que la ruta sea correcta.
            //string rutaCompleta = Path.Combine(rutaEscritorio, nombreArchivo);

            //// 4. Escribe el contenido en el archivo en el Escritorio.
            // File.WriteAllText(rutaCompleta, soapResponse);
            ////------------------------------------//
            #endregion

            Detail detalles = new Detail();
            var resultadosConsulta = doc.Descendants(ns + "resultadoServicioWeb")
                                .Select(m => new Detail()
                                {
                                    Resultado = (string)m.Element(ns + "resultado"),
                                    Mensaje = (string)m.Element(ns + "mensaje"),
                                }).FirstOrDefault();

            // Si el WS no responde EXITO se devuelve su resultado y su mensaje EXACTOS
            // (ej.: FRACASO / "El documento es obligatoria"), en lugar de un mensaje genérico.
            CombinedData combinedResults = new CombinedData()
            {
                Detalle = resultadosConsulta != null
                    ? new Detail()
                    {
                        Resultado = string.IsNullOrWhiteSpace(resultadosConsulta.Resultado) ? "ERROR" : resultadosConsulta.Resultado,
                        Mensaje = string.IsNullOrWhiteSpace(resultadosConsulta.Mensaje) ? "El WS no informó un mensaje." : resultadosConsulta.Mensaje
                    }
                    : new Detail()
                    {
                        Resultado = "ERROR",
                        Mensaje = "La respuesta del WS no trae 'resultadoServicioWeb'. Respuesta recibida: " +
                                  (soapResponse.Length > 1500 ? soapResponse.Substring(0, 1500) + "..." : soapResponse)
                    }
            };

            if (resultadosConsulta != null && resultadosConsulta.Resultado=="EXITO")
            {
                detalles = doc.Descendants(ns + "TarjetaObtenerDatosResult")
                                               .Select(m => new Detail()
                                               {
                                                   Documento = m.Element(ns + "documento").Value,
                                                   Nombre = m.Element(ns + "nombre").Value,
                                                   Direccion = m.Element(ns + "direccion").Value,
                                                   MontoAdeudado = m.Element(ns + "montoAdeudado").Value,
                                                   MontoDisponible = m.Element(ns + "MontoDisponible").Value,
                                                   ProximaFechaPago = m.Element(ns + "proximaFechaPago").Value != "0:00:00" ? DateTime.ParseExact(m.Element(ns + "proximaFechaPago").Value, "dd/MM/yyyy", null) : DateTime.Parse("11/11/1111"),
                                                   TotalProximaCuota = m.Element(ns + "totalProximaCuota").Value,
                                                   FechaPagoProximaCuota = m.Element(ns + "fechaPagoProximaCuota").Value != "0:00:00" ? DateTime.ParseExact(m.Element(ns + "fechaPagoProximaCuota").Value, "dd/MM/yyyy", null) : DateTime.Parse("11/11/1111"),
                                               }).FirstOrDefault();

                detalles.Resultado = resultadosConsulta.Resultado;

                XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
                var movements = doc.Descendants(ns + "Movimiento")
                     .Where(m => m.Attribute(xsi + "nil") == null || m.Attribute(xsi + "nil").Value != "true")
                     .Select(m => new MovementDetail
                     {
                         Fecha = DateTime.ParseExact(m.Element(ns + "fecha")?.Value, "dd/MM/yyyy", null),
                         Descripcion = m.Element(ns + "descripcion")?.Value,
                         Monto = m.Element(ns + "monto")?.Value,
                         Recargo = m.Element(ns + "recargo")?.Value
                     });


                // El WS a veces devuelve solicitudes o cuotas vacías (xsi:nil="true") o sin alguno de sus campos.
                // Se descartan (igual que ya se hacía con Movimiento) en lugar de romper todo el resumen del cliente.
                Func<XElement, bool> noEsNil = x => x.Attribute(xsi + "nil") == null || x.Attribute(xsi + "nil").Value != "true";

                var detallesSolicitud = doc.Descendants(ns + "DetalleSolicitud")
                                       .Where(noEsNil)
                                       .Where(d => !string.IsNullOrWhiteSpace((string)d.Element(ns + "numeroSolicitud")))
                                       .Select(d => new SolicitudDetail()
                                       {
                                           NumeroSolicitud = (string)d.Element(ns + "numeroSolicitud"),
                                           NombreComercio = (string)d.Element(ns + "nombreComercio") ?? "",
                                           DetallesCuota = d.Descendants(ns + "DetalleCuota")
                                               .Where(noEsNil)
                                               .Where(e => !string.IsNullOrWhiteSpace((string)e.Element(ns + "fechaCuota")))
                                               .Select(e => new DetalleCuota()
                                               {
                                                   Fecha = DateTime.ParseExact((string)e.Element(ns + "fechaCuota"), "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
                                                   NumeroCuota = (string)e.Element(ns + "numeroCuota") ?? "0",
                                                   Monto = (string)e.Element(ns + "montoCuota") ?? "0"
                                               }).ToList()
                                       });

                combinedResults = new CombinedData()
                {
                    Detalle = detalles,
                    Movimientos = movements.ToList(),
                    DetallesSolicitud = detallesSolicitud.ToList(),
                };
            }
            return combinedResults;
        }

        public async Task<decimal> CalcularMontoCuota(CombinedData datosMovimientos, DateTime fechaActualCuotas)
        {
            List<MovimientoTarjetaDTO> comprasAgrupadas = new List<MovimientoTarjetaDTO>();
            CultureInfo.CurrentCulture = new CultureInfo("es-AR");

            comprasAgrupadas = datosMovimientos.Movimientos.Where(x => x.Descripcion=="PAGOS DE CUOTA REGULAR")
                        .GroupBy(m => new { m.Descripcion, m.Fecha })
                        .Select(g => new MovimientoTarjetaDTO
                        {
                            Monto =  (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ","))==null ? g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", "."))).ToString().Replace(".", ",") : (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ",")),
                            TipoMovimiento = g.Key.Descripcion,
                            Fecha = g.Key.Fecha.Date.ToString("dd/MM/yyyy")
                        })
                        .ToList();

            comprasAgrupadas.AddRange(datosMovimientos.Movimientos.Where(x => x.Descripcion!="PAGOS DE CUOTA REGULAR")
            .Select(g => new MovimientoTarjetaDTO
            {
                Monto = g.Monto.Replace(",", ".").ToString().Replace(".", ","),
                TipoMovimiento = g.Descripcion,
                Fecha = g.Fecha.Date.ToString("dd/MM/yyyy")
            }).ToList());

            //Montos a Pagar con Deuda pero sin Punitorios
            var totalDetallesCuota = datosMovimientos.DetallesSolicitud
            .Where(result => result?.DetallesCuota != null)
            .SelectMany(result => result.DetallesCuota)
            .Where(detalle => (common.ConvertirFecha(detalle.Fecha) <= fechaActualCuotas))
            .Select(e => new { monto = e.Monto }).ToList();

            var totalMontoCuota = totalDetallesCuota.Sum(e => Convert.ToDecimal(e.monto.Replace(".", ",")));
            return totalMontoCuota;
        }

        public async Task<decimal> CalcularMontoProximaCuota(CombinedData datosMovimientos, DateTime fechaActualCuotasProximo)
        {
            DateTime fechaInicio = new DateTime(fechaActualCuotasProximo.Year, fechaActualCuotasProximo.Month, 1);
            List<MovimientoTarjetaDTO> comprasAgrupadas = new List<MovimientoTarjetaDTO>();
            CultureInfo.CurrentCulture = new CultureInfo("es-AR");

            var totalDetallesCuotaProximoMes = datosMovimientos.DetallesSolicitud
                   .Where(result => result?.DetallesCuota != null)
                   .SelectMany(result => result.DetallesCuota)
                   .Where(detalle => (common.ConvertirFecha(detalle.Fecha) >= fechaInicio && common.ConvertirFecha(detalle.Fecha)<=fechaActualCuotasProximo))
                   .Select(e => new { monto = e.Monto }).ToList();

            var totalMontoProximaCuota = totalDetallesCuotaProximoMes.Sum(e => Convert.ToDecimal(e.monto.Replace(".", ",")));
            return totalMontoProximaCuota;
        }

        public async Task<decimal> CalcularPunitorios(List<SolicitudDetail> cuotas)
        {
            int anioActual = DateTime.Now.Year; // -> 2025
            int mesActual = DateTime.Now.Month; // -> 8

            // El servicio calcula automáticamente fa fecha de corte para los Punitorios.
            DateTime fechaCorteMora = ObtenerFechaDeCalculoCorrecta();

            // 1. Calcular la tasa diaria
            int diasDelAnio = DateTime.IsLeapYear(fechaCorteMora.Year) ? 366 : 365;
            decimal tasaDiaria = (TNA / diasDelAnio);

            List<DetalleCuota> cuotasEnMora = cuotas
                .SelectMany(result => result.DetallesCuota)
                .Where(c => common.ConvertirFecha(c.Fecha) <= fechaCorteMora.Date)
                .ToList();

            if (!cuotasEnMora.Any())
            {
                //No se encontraron cuotas en mora para la fecha de corte especificada.
                return 0;
            }

            decimal totalPunitorios = 0;

            // 3. Calcular el punitorio para cada cuota vencida
            foreach (var cuota in cuotasEnMora)
            {
                // Se calculan los días de mora desde el vencimiento hasta la fecha de cálculo
                int diasEnMora = (int)(fechaCorteMora.Date -  common.ConvertirFecha(cuota.Fecha)).TotalDays;

                // Se multiplica el monto por la tasa diaria y por los días en mora
                decimal monto = Convert.ToDecimal(cuota.Monto.Replace(".", ","));
                decimal punitorioCuota = monto * tasaDiaria * diasEnMora;

                totalPunitorios += punitorioCuota;
            }
            return totalPunitorios;
        }

        public async Task<List<MovimientoTarjetaDTO>> ObtieneUltimosMovimientos(CombinedData datosMovimientos, int top)
        {
            List<MovimientoTarjetaDTO> comprasAgrupadas = new List<MovimientoTarjetaDTO>();
            CultureInfo.CurrentCulture = new CultureInfo("es-AR");

            comprasAgrupadas = datosMovimientos.Movimientos.Where(x => x.Descripcion=="PAGOS DE CUOTA REGULAR")
                        .GroupBy(m => new { m.Descripcion, m.Fecha })
                        .Select(g => new MovimientoTarjetaDTO
                        {
                            //Monto =  (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ","))==null ? g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", "."))).ToString().Replace(".", ",") : (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ",")),
                            Monto =  ((g.Sum(m => Convert.ToDecimal(m.Monto)))+(g.Sum(m => Convert.ToDecimal(m.Recargo)))).ToString(),
                            TipoMovimiento = g.Key.Descripcion,
                            Fecha = g.Key.Fecha.Date.ToString("dd/MM/yyyy")
                        })
                        .ToList();

            comprasAgrupadas.AddRange(datosMovimientos.Movimientos.Where(x => x.Descripcion!="PAGOS DE CUOTA REGULAR")
            .Select(g => new MovimientoTarjetaDTO
            {
                Monto = g.Monto.Replace(",", ".").ToString().Replace(".", ","),
                TipoMovimiento = g.Descripcion,
                Fecha = g.Fecha.Date.ToString("dd/MM/yyyy")
            }).ToList());

            List<MovimientoTarjetaDTO> MovientosOrdenadosPorFecha = comprasAgrupadas.OrderByDescending(x => common.ConvertirFecha(x.Fecha)).Take(Convert.ToInt32(20)).ToList();
            return MovientosOrdenadosPorFecha;
        }

        public async Task<ResponseObtenerDatosPersonaDTO> ObtenerPersona(string dni)
        {
            var requestBody = new ObtenerPersonaRequestDTO
            {
                LoginInterface = new LoginInterface
                {
                    Login = "appestancias",
                    Clave = "appcpe01"
                },
                Documento = dni
            };

            var jsonPayload = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(this._apiBaseUrl+"api/ecommerce/ObtenerPersona", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ResponseObtenerDatosPersonaDTO>(jsonResponse);
                }
                else
                {
                    throw new Exception($"Error al llamar a la API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error en la comunicación con el servicio de personas", ex);
            }
        }

        public async Task<ResponseObtenerCreditosDTO> ObtenerCreditos(int idPersona)
        {
            var requestBody = new ObtenerCreditosRequestDTO
            {
                LoginInterface = new LoginInterface
                {
                    Login = "appestancias",
                    Clave = "appcpe01"
                },
                IdPersona = idPersona
            };

            var jsonPayload = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(this._apiBaseUrl+"api/ecommerce/ObtenerCreditos", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ResponseObtenerCreditosDTO>(jsonResponse);
                }
                else
                {
                    throw new Exception($"Error al llamar a la API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error en la comunicación con el servicio de personas", ex);
            }
        }





        public async Task<ResponseObtenerCreditosDetallesDTO> ObtenerCreditosDetalles(int idSolicitud)
        {
            var requestBody = new ObtenerCreditosDetallesRequestDTO
            {
                LoginInterface = new LoginInterface
                {
                    Login = "appestancias",
                    Clave = "appcpe01"
                },
                IdSolicitud = idSolicitud
            };

            var jsonPayload = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(this._apiBaseUrl+"api/ecommerce/obtenerDetalleOperacion", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ResponseObtenerCreditosDetallesDTO>(jsonResponse);
                }
                else
                {
                    throw new Exception($"Error al llamar a la API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error en la comunicación con el servicio de personas", ex);
            }
        }


        public async Task<ResponseObtenerOperacionDetallesDTO> ObtenerOperacionDetalles(string numOperacion)
        {
            var requestBody = new ObteneOperacionDetallesRequestDTO
            {
                LoginServicio = new LoginServicio
                {
                    Login = "appestancias",
                    Clave = "appcpe01"
                },
                numeroOperacion = numOperacion
            };

            var jsonPayload = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(this._apiBaseUrl+"api/collection/obtenerDetalleOperacion", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var detalles = JsonConvert.DeserializeObject<ResponseObtenerOperacionDetallesDTO>(jsonResponse);
                    return detalles;
                }
                else
                {
                    throw new Exception($"Error al llamar a la API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error en la comunicación con el servicio de personas", ex);
            }
        }

        //Trae los montos con punitorios de la persona, para el resumen de tarjeta
        public async Task<ResponseObtenerConsultaDTO> ObtenerConsulta(string dni, string sexo)
        {
            var requestBody = new ObtenerConsultaRequestDTO
            {
                LoginInterface = new LoginInterface
                {
                    Login = "appestancias",
                    Clave = "appcpe01"
                },
                documento = dni,
                sexo = sexo
            };

            var jsonPayload = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(this._apiBaseUrl + "integracion/api/consulta", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ResponseObtenerConsultaDTO>(jsonResponse);
                }
                else
                {
                    throw new Exception($"Error al llamar a la API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error en la comunicación con el servicio de personas", ex);
            }
        }








        #region Saldo a pagar (fuente única: cobranzas de la API de consulta de LOAN)

        /// <summary>
        /// Obtiene las cobranzas (cuotas pendientes con punitorios calculados por LOAN) de un documento.
        /// Misma lógica que usaba /Core/MovimientoTarjeta: se toma el sexo de ObtenerPersona y,
        /// si con "F" no hay cobranzas, se reintenta con "M".
        /// La usan /Core/MovimientoTarjeta y la generación/simulación de resúmenes, para que el saldo sea el mismo.
        /// </summary>
        public async Task<ResponseObtenerConsultaDTO> ObtenerConsultaCobranzasAsync(string nroDocumento)
        {
            string letraSexo = "F";
            var datosPersona = await ObtenerPersona(nroDocumento);
            if (datosPersona != null && datosPersona.Persona != null && datosPersona.Persona.Sexo != null)
            {
                if (datosPersona.Persona.Sexo.Id == 2)
                {
                    letraSexo = "M";
                }
            }

            var consulta = await ObtenerConsulta(nroDocumento, letraSexo);
            if ((consulta == null || consulta.cobranzas == null || !consulta.cobranzas.Any()) && letraSexo == "F")
            {
                consulta = await ObtenerConsulta(nroDocumento, "M");
            }
            return consulta;
        }

        /// <summary>
        /// Cobranzas que integran el saldo a pagar: las que vencen hasta el último día del mes de fechaReferencia.
        /// </summary>
        public static List<CobranzaDto> CobranzasDelSaldo(ResponseObtenerConsultaDTO consulta, DateTime fechaReferencia)
        {
            if (consulta == null || consulta.cobranzas == null) return new List<CobranzaDto>();
            var finDeMes = new DateTime(fechaReferencia.Year, fechaReferencia.Month, DateTime.DaysInMonth(fechaReferencia.Year, fechaReferencia.Month));
            return consulta.cobranzas.Where(x => x.fechaVencimiento.Date <= finDeMes).ToList();
        }

        #endregion

        #region Métodos Para Resumen Tarjeta    
        //Métodos Para Resumen Tarjeta        
        public async Task<List<ResultadoCuotasDTO>> CuotasDetallesResumen(CombinedData datosMovimientos, DateTime fechaActualCuotas)
        {
            List<MovimientoTarjetaDTO> comprasAgrupadas = new List<MovimientoTarjetaDTO>();
            CultureInfo.CurrentCulture = new CultureInfo("es-AR");

            comprasAgrupadas = datosMovimientos.Movimientos.Where(x => x.Descripcion=="PAGOS DE CUOTA REGULAR")
                        .GroupBy(m => new { m.Descripcion, m.Fecha })
                        .Select(g => new MovimientoTarjetaDTO
                        {
                            Monto =  (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ","))==null ? g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", "."))).ToString().Replace(".", ",") : (g.Sum(m => Convert.ToDecimal(m.Monto.Replace(",", ".")) + Convert.ToDecimal(m.Recargo.Replace(",", "."))).ToString().Replace(".", ",")),
                            TipoMovimiento = g.Key.Descripcion,
                            Fecha = g.Key.Fecha.Date.ToString("dd/MM/yyyy")
                        })
                        .ToList();

            comprasAgrupadas.AddRange(datosMovimientos.Movimientos.Where(x => x.Descripcion!="PAGOS DE CUOTA REGULAR")
            .Select(g => new MovimientoTarjetaDTO
            {
                Monto = g.Monto.Replace(",", ".").ToString().Replace(".", ","),
                TipoMovimiento = g.Descripcion,
                Fecha = g.Fecha.Date.ToString("dd/MM/yyyy")
            }).ToList());

            List<ResultadoCuotasDTO> totalDetallesCuota = datosMovimientos.DetallesSolicitud
              .Where(result => result?.DetallesCuota != null)
              .SelectMany(result => result.DetallesCuota
                .Where(detalle => common.ConvertirFecha(detalle.Fecha) <= fechaActualCuotas)
                .Select(detalle => new ResultadoCuotasDTO
                {
                    Descripcion = result.NombreComercio,
                    Codigo = result.NumeroSolicitud,
                    Fecha = detalle.Fecha,
                    NumeroCuota = detalle.NumeroCuota,
                    Monto = detalle.Monto,
                    NumeroCuotaTotal = datosMovimientos.DetallesSolicitud.Where(x=>x.NumeroSolicitud==result.NumeroSolicitud).Max(e => Convert.ToInt32(e.DetallesCuota.Max(c => c.NumeroCuota))).ToString()
                })).OrderBy(e => e.Fecha).OrderBy(d => d.Codigo)
              .ToList();

            return totalDetallesCuota;
        }
        public Task<List<ResultadoCuotasDTO>> CalcularPunitoriosResumen(List<ResultadoCuotasDTO> cuotas)
        {
            return CalcularPunitoriosResumen(cuotas, DateTime.Now);
        }

        /// <summary>
        /// Igual que CalcularPunitoriosResumen, pero calculando la mora como si hoy fuera fechaReferencia.
        /// Lo usa la simulación de resúmenes para reproducir la generación de un período.
        /// </summary>
        public async Task<List<ResultadoCuotasDTO>> CalcularPunitoriosResumen(List<ResultadoCuotasDTO> cuotas, DateTime fechaReferencia)
        {
            var culturaAR = new CultureInfo("es-AR");
            List<ResultadoCuotasDTO> cuotasConPunitorios = new List<ResultadoCuotasDTO>();

            DateTime fechaCorteMora = ObtenerFechaDeCalculoCorrecta(fechaReferencia);


            int diasDelAnio = DateTime.IsLeapYear(fechaCorteMora.Year) ? 366 : 365;
            decimal tasaDiaria = (TNA / diasDelAnio);

            foreach (var cuota in cuotas)
            {
                ResultadoCuotasDTO resultadoCuotas = new ResultadoCuotasDTO();
                resultadoCuotas = cuota;
                if (common.ConvertirFecha(cuota.Fecha) <= fechaCorteMora.Date)
                {
                    int diasEnMora = (int)(fechaCorteMora.Date - common.ConvertirFecha(cuota.Fecha)).TotalDays;
                    decimal monto = Convert.ToDecimal(cuota.Monto.Replace(".", ","));
                    decimal punitorioCuota = monto * tasaDiaria * diasEnMora; 
                    decimal montoTotal = monto + punitorioCuota; // Realiza la suma de dos decimales
                    resultadoCuotas.Monto = montoTotal.ToString();
                }
                cuotasConPunitorios.Add(resultadoCuotas);
            }
            return cuotasConPunitorios;
        }
        public async Task<TempalteResumenDTO> PrepararDatosResumen(CombinedData datosMovimientos, List<ResultadoCuotasDTO> datos, Periodo periodo, UsuarioParaProcesarDTO usuario)
        {
            TempalteResumenDTO tempalteResumenDTO = new TempalteResumenDTO();
            List<DetallesCuotasResumenDTO>  detallesCuotasResumenDTO = new List<DetallesCuotasResumenDTO>();
            //Usuario user = _context.Usuarios.Where(x=>x.Personas.NroDocumento==datosMovimientos.Detalle.Documento).FirstOrDefault();
            //ResumenTarjeta resumenTarjeta = _context.ResumenTarjeta.Where(x=>x.Usuario.Personas.NroDocumento==datosMovimientos.Detalle.Documento).OrderByDescending(r => r.Id).FirstOrDefault();
            //tempalteResumenDTO.SaldoAnterior = resumenTarjeta!=null?(resumenTarjeta.Monto+resumenTarjeta.MontoAdeudado):0;
            DateTime fechaVencimiento = new DateTime(periodo.FechaVencimiento.Year, periodo.FechaVencimiento.Month, 10);
            tempalteResumenDTO.SaldoActual = 0; 
            tempalteResumenDTO.SaldoPendiente = 0; 
            tempalteResumenDTO.SaldoTotal = 0; 
            tempalteResumenDTO.Pagos = 0; 
            tempalteResumenDTO.Intereses = 0; 
            tempalteResumenDTO.Impuestos = 0; 
            tempalteResumenDTO.Nombre = datosMovimientos.Detalle.Nombre;
            tempalteResumenDTO.NroDocumento =  datosMovimientos.Detalle.Documento;
            tempalteResumenDTO.Mail =  usuario.UserName; 
            tempalteResumenDTO.NroSocio = usuario.Id; 
            tempalteResumenDTO.NroTarjeta = usuario.NroTarjeta; 
            tempalteResumenDTO.Domicilio =  datosMovimientos.Detalle.Direccion; 
            tempalteResumenDTO.PeriodoDesde = periodo.FechaDesde.ToString("dd/MMM/yyyy"); 
            tempalteResumenDTO.PeriodoHasta = periodo.FechaHasta.ToString("dd/MMM/yyyy");
            tempalteResumenDTO.Vencimiento = fechaVencimiento.ToString("dd/MMM/yyyy");   

            var consumosAnteriores = datos.Where(result => result?.Fecha != null)
                .Where(detalle => (common.ConvertirFecha(detalle.Fecha).Date < periodo.FechaVencimiento.Date)).ToList();

            var consumosDelMes = datos.Where(result => result?.Fecha != null)
                .Where(detalle => (common.ConvertirFecha(detalle.Fecha).Date >= periodo.FechaVencimiento.Date)).ToList();

            tempalteResumenDTO.ConsumosAnteriores = consumosAnteriores;
            tempalteResumenDTO.ConsumosDelMes = consumosDelMes;

            tempalteResumenDTO.SaldoPendiente = consumosAnteriores.Sum(x=>Convert.ToDecimal(x.Monto));
            tempalteResumenDTO.SaldoActual = consumosDelMes.Sum(x => Convert.ToDecimal(x.Monto));
            tempalteResumenDTO.SaldoTotal = tempalteResumenDTO.SaldoPendiente+tempalteResumenDTO.SaldoActual;

            return tempalteResumenDTO;
        }

        public async Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model)
        {
            var actionContext = GetActionContext();
            var view = FindView(actionContext, viewName);

            using (var output = new StringWriter())
            {
                var viewContext = new ViewContext(
                    actionContext,
                    view,
                    new ViewDataDictionary<TModel>(
                        metadataProvider: new EmptyModelMetadataProvider(),
                        modelState: new ModelStateDictionary())
                    {
                        Model = model
                    },
                    new TempDataDictionary(
                        actionContext.HttpContext,
                        _tempDataProvider),
                    output,
                    new HtmlHelperOptions());

                await view.RenderAsync(viewContext);

                return output.ToString();
            }
        }
        #endregion

        #region Extra 
        private IView FindView(ActionContext actionContext, string viewName)
            {
                var getViewResult = _razorViewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: true);
                if (getViewResult.Success)
                {
                    return getViewResult.View;
                }

                var findViewResult = _razorViewEngine.FindView(actionContext, viewName, isMainPage: true);
                if (findViewResult.Success)
                {
                    return findViewResult.View;
                }

                throw new ArgumentNullException($"No se pudo encontrar la vista {viewName}. Se buscó en las siguientes ubicaciones: {string.Join(", ", findViewResult.SearchedLocations)}");
            }

        private ActionContext GetActionContext()
        {
            var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
            return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        }

        /// <summary>
        /// Calcula el último día de un mes y año específicos.
        /// </summary>
        /// <param name="anio">El año.</param>
        /// <param name="mes">El mes.</param>
        /// <returns>El último día hábil del mes.</returns>
        private DateTime ObtenerUltimoDia(int anio, int mes)
        {
            DateTime ultimoDia = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes));
            return ultimoDia;
        }

        public DateTime ObtenerFechaDeCalculoCorrecta()
        {
            return ObtenerFechaDeCalculoCorrecta(DateTime.Now);
        }

        public DateTime ObtenerFechaDeCalculoCorrecta(DateTime hoy)
        {
            if (hoy.Day <= 15)
            {
                // Si es antes del día 15, la fecha de cálculo es el 15 del mes actual.
                return new DateTime(hoy.Year, hoy.Month, 15);
            }
            else
            {
                // Si es día 15 o posterior, la fecha de cálculo es el último día del mes actual.
                return ObtenerUltimoDia(hoy.Year, hoy.Month);
            }
        }

        public DateTime ObtenerFechaDeCalculoCorrectaResumen()
        {
            DateTime hoy = DateTime.Now;
            return new DateTime(hoy.Year, hoy.AddMonths(1).Month, 01);
                
        }

   
        #endregion


    }


}