using Commons.Models;
using DAL.Data;
using DAL.DTOs;
using DAL.DTOs.Reportes;
using DAL.Mobile;
using DAL.Models;
using DAL.Models.Core;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using Renci.SshNet.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace EstanciasCore.Controllers
{
    [Area("Core")]

	public class PagoTarjetaController : EstanciasCoreController
    {
        private readonly IPushService _wonderPushService;
        public PagoTarjetaController(EstanciasContext context, IPushService wonderPushService) : base(context)
        {
            breadcumb.Add(new Commons.Models.Message() { DisplayName = "Datos" });
            _wonderPushService = wonderPushService;
        }

        public IActionResult Index()
        {
            breadcumb.Add(new Commons.Models.Message() { DisplayName = "Pago" });
            ViewBag.Breadcrumb = breadcumb;
            return View();
        }

        public async Task<IActionResult> _ListadoPagoTarjeta(Page<PagoTarjeta> page)
        {
            var today = DateTime.Today.AddDays(-7); ;

            var c = _context.PagoTarjeta.Where(x => x.FechaComprobante >= today && x.EstadoPago != EstadoPago.Pendiente).Count();

            if (c < 1) { c = 1; }
            page.SelectPage("/Clientes/_ListadoPagoTarjeta",
                _context.PagoTarjeta.Where(x => x.FechaComprobante >= today && x.EstadoPago != EstadoPago.Pendiente).OrderByDescending(x=>x.FechaComprobante), c);

            return PartialView("_ListadoPagoTarjeta", page);
        }

        public async Task<IActionResult> _ListadoPagoTarjetaNew()
        {
            return PartialView("_ListadoPagoTarjetaNew");
        }

        public async Task<IActionResult> _ListadoPagoTarjetaDataTable()
        {
            var today = DateTime.Today.AddDays(-7); ;

            var query = from p in _context.PagoTarjeta
                        where p.FechaComprobante >= today && p.EstadoPago != EstadoPago.Pendiente
                        orderby p.FechaComprobante descending
                        select new DAL.DTOs.PagoTarjetaDataTableDTO
                        {
                            Id = p.Id,
                            Persona = (p.Persona!=null) ? p.Persona.Apellido+" "+p.Persona.Nombres : "---",
                            NroDocumento =(p.Persona!=null) ? p.Persona.NroDocumento : "---",
                            Usuario = (p.Persona!=null) ? p.Persona.Email : "---",
                            NroTarjeta =(p.Persona!=null) ? p.Persona.NroTarjeta : "---",
                            MontoAdeudado = p.MontoAdeudado,
                            MontoInformado = p.MontoInformado,
                            FechaVencimiento = (p.FechaVencimiento ?? DateTime.MinValue).ToString("dd/MM/yyyy"),
                            FechaComprobante = (p.FechaComprobante ?? DateTime.MinValue).ToString("dd/MM/yyyy"),
                            EstadoPago = p.EstadoPago.ToString(),
                            EstadoPagoId = ((int)p.EstadoPago),
                            ComprobantePago = p.ComprobantePago !=null ? true : false,
                            FechaOrden = Convert.ToInt32((p.FechaComprobante ?? DateTime.MinValue).ToString("yyyyMMdd")),
                            Observacion = (p.Observacion!=null) ? p.Observacion : "",
                            FechaDePago = (p.FechaDePago ?? DateTime.MinValue).ToString("dd/MM/yyyy"),
                        }; 
            var lista = query.ToList();
            return DataTable<PagoTarjetaDataTableDTO>(lista.AsQueryable());

            //return PartialView(page);
        }

        public IActionResult _Create()
        {
            return PartialView();
        }     


        public async Task<IActionResult> _VerComprobante(int Id)
        {

            PagoTarjeta pagoTarjeta = await _context.PagoTarjeta.FindAsync(Id);
            string comprobante = Convert.ToBase64String(pagoTarjeta.ComprobantePago);
            return PartialView("_VerComprobante", comprobante);
        }



        public async Task<bool> AprobarComprobante(int id)
            {
                
            try
            {
                PagoTarjeta pagoTarjeta = _context.PagoTarjeta.Where(s => s.Id == id).First();
                pagoTarjeta.EstadoPago = EstadoPago.Aprobado;
                _context.PagoTarjeta.Update(pagoTarjeta);
                Clientes cliente = _context.Clientes.Where(x => x.Persona.Id == pagoTarjeta.Persona.Id).FirstOrDefault();

                NotificacionesPersonas notificacion = new NotificacionesPersonas()
                {
                    Cliente = cliente,
                    Titulo = "Pago Aprobado",
                    Descripcion = "Se aprobo su comprobante de Pago",
                    FechaHora = DateTime.Now,
                    TomaConocimiento = null
                };
                _context.NotificacionesPersonas.Add(notificacion);
                _context.SaveChanges();
                Notificaciones notificaciones = _context.Notificaciones.Where(n => n.Codigo == "PA").FirstOrDefault();
                NotificacionViewModelDTO notificacionesDTO = new NotificacionViewModelDTO()
                {
                    Titulo = notificaciones.NotificacionesPlantillas.Titulo,
                    Mensaje = notificaciones.NotificacionesPlantillas.Mensaje,
                    ImagenUrl = notificaciones.NotificacionesPlantillas.ImagenUrl,
                    DeepLink = notificaciones.NotificacionesPlantillas.DeepLink
                };
                List<string> instalationId = new List<string>() { cliente.Usuario.DeviceId } ; 
                await _wonderPushService.EnviarNotificacionAIds(notificacionesDTO, instalationId);
                return true;
            }
            catch (System.Exception e)
            {
                AddPageAlerts(PageAlertType.Success, "Hubo un error al Aprobar el Pago.");
                return false;
            }
        }


        public bool RechazarComprobante([FromBody] RechazarComprobanteDTO dto)
        {
            try
            {
                PagoTarjeta pagoTarjeta = _context.PagoTarjeta.Where(s => s.Id == dto.Id).First();
                pagoTarjeta.EstadoPago = EstadoPago.Rechazado;
                pagoTarjeta.Observacion = dto.Observacion;
                _context.PagoTarjeta.Update(pagoTarjeta);
                _context.SaveChanges();
                Clientes cliente = _context.Clientes.Where(x => x.Persona.Id == pagoTarjeta.Persona.Id).FirstOrDefault();

                NotificacionesPersonas notificacion = new NotificacionesPersonas()
                {
                    Cliente = cliente,
                    Titulo = "Pago Rechazado",
                    Descripcion = "Se rechazo su comprobante de Pago",
                    FechaHora = DateTime.Now,
                    TomaConocimiento = null
                };
                _context.NotificacionesPersonas.Add(notificacion);
                _context.SaveChanges();
                Notificaciones notificaciones = _context.Notificaciones.Where(n => n.Codigo == "PR").FirstOrDefault();
                NotificacionViewModelDTO notificacionesDTO = new NotificacionViewModelDTO()
                {
                    Titulo = notificaciones.NotificacionesPlantillas.Titulo,
                    Mensaje = notificaciones.NotificacionesPlantillas.Mensaje,
                    ImagenUrl = notificaciones.NotificacionesPlantillas.ImagenUrl,
                    DeepLink = notificaciones.NotificacionesPlantillas.DeepLink
                };
                List<string> instalationId = new List<string>() { cliente.Usuario.DeviceId };
                _wonderPushService.EnviarNotificacionAIds(notificacionesDTO, instalationId);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        [HttpPost]
        public JsonResult AprobarMasivo([FromBody] List<int> ids)
        {
            try
            {
                var pagosAprobar = _context.PagoTarjeta.Where(s => ids.Contains(s.Id)).ToList();
                foreach (var pagoTarjeta in pagosAprobar)
                {
                    pagoTarjeta.EstadoPago = EstadoPago.Aprobado;
                    _context.PagoTarjeta.Update(pagoTarjeta);
                }
                _context.SaveChanges();
                List<string> instalationId = new List<string>();
                foreach (var pagoTarjeta in pagosAprobar)
                {
                    Clientes cliente = _context.Clientes.Where(x => x.Persona.Id == pagoTarjeta.Persona.Id).FirstOrDefault();
                    if (cliente != null)
                    {
                        NotificacionesPersonas notificacion = new NotificacionesPersonas()
                        {
                            Cliente = cliente,
                            Titulo = "Pago Aprobado",
                            Descripcion = "Se aprobó su comprobante de Pago",
                            FechaHora = DateTime.Now,
                            TomaConocimiento = null
                        };
                        _context.NotificacionesPersonas.Add(notificacion);
                        
                    }
                    if (cliente.Usuario.DeviceId != null) { instalationId.Add(cliente.Usuario.DeviceId); }
                    
                }
                _context.SaveChanges();
                Notificaciones notificaciones = _context.Notificaciones.Where(n => n.Codigo == "PA").FirstOrDefault();
                NotificacionViewModelDTO notificacionesDTO = new NotificacionViewModelDTO()
                {
                    Titulo = notificaciones.NotificacionesPlantillas.Titulo,
                    Mensaje = notificaciones.NotificacionesPlantillas.Mensaje,
                    ImagenUrl = notificaciones.NotificacionesPlantillas.ImagenUrl,
                    DeepLink = notificaciones.NotificacionesPlantillas.DeepLink
                };
                if (instalationId.Count() != 0) { _wonderPushService.EnviarNotificacionAIds(notificacionesDTO, instalationId); }
                 
                
                return Json(new { success = true, message = "Los pagos fueron aprobados." });
            }
            catch (Exception e)
            {
                return Json(new { success = false, message = "Error al aprobar los pagos." });
            }
        }

        [HttpPost]
        public JsonResult RechazarMasivo([FromBody] RechazarComprobanteMasivoDTO dto)
        {
            try
            {
                var pagosRechazar = _context.PagoTarjeta.Where(s => dto.Ids.Contains(s.Id)).ToList();
                foreach (var pagoTarjeta in pagosRechazar)
                {
                    pagoTarjeta.EstadoPago = EstadoPago.Rechazado;
                    pagoTarjeta.Observacion = dto.Observacion;
                    _context.PagoTarjeta.Update(pagoTarjeta);
                }
                _context.SaveChanges();

                List<string> instalationId = new List<string>();
                foreach (var pagoTarjeta in pagosRechazar)
                {
                    Clientes cliente = _context.Clientes.Where(x => x.Persona.Id == pagoTarjeta.Persona.Id).FirstOrDefault();
                    if (cliente != null)
                    {
                        NotificacionesPersonas notificacion = new NotificacionesPersonas()
                        {
                            Cliente = cliente,
                            Titulo = "Pago Rechazado",
                            Descripcion = "Se rechazó su comprobante de Pago. Motivo: " + dto.Observacion,
                            FechaHora = DateTime.Now,
                            TomaConocimiento = null
                        };
                        _context.NotificacionesPersonas.Add(notificacion);
                        // common.EnviaNotificationWonderPushId("Pago Rechazado", "Se rechazó su comprobante de Pago. Motivo: " + dto.Observacion, new string[] { cliente.Usuario.DeviceId });
                    }
                    if (cliente.Usuario.DeviceId != null) { instalationId.Add(cliente.Usuario.DeviceId); }
                }
                _context.SaveChanges();
                Notificaciones notificaciones = _context.Notificaciones.Where(n => n.Codigo == "PR").FirstOrDefault();
                NotificacionViewModelDTO notificacionesDTO = new NotificacionViewModelDTO()
                {
                    Titulo = notificaciones.NotificacionesPlantillas.Titulo,
                    Mensaje = notificaciones.NotificacionesPlantillas.Mensaje,
                    ImagenUrl = notificaciones.NotificacionesPlantillas.ImagenUrl,
                    DeepLink = notificaciones.NotificacionesPlantillas.DeepLink
                };
                if (instalationId.Count() != 0) { _wonderPushService.EnviarNotificacionAIds(notificacionesDTO, instalationId); }
                return Json(new { success = true, message = "Los pagos han sido rechazados." });
            }
            catch (Exception e)
            {
                return Json(new { success = false, message = "Hubo un error al rechazar los pagos." });
            }
        }

        [HttpPost]
        public IActionResult ExportarExcel(string ids)
        {
            try
            {
                var listaIds = ids.Split(',').Select(int.Parse).ToList();
                var query = _context.PagoTarjeta.Where(p => listaIds.Contains(p.Id));
                query = ApplyOrder(query, "FechaComprobante", "asc");
                var pagos = query.ToList();
                byte[] excelBytes = _generateXlsxBytes(pagos);

                string excelName = $"Comprobantes_{DateTime.Now.ToString("yyyyMMddHHmmss")}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
            }
            catch (Exception e)
            {
                return BadRequest("Hubo un error al generar el archivo Excel: " + e.Message);
            }
        }

        private IQueryable<PagoTarjeta> ApplyOrder(IQueryable<PagoTarjeta> query, string sortColumn, string sortDirection)
        {
            bool ascending = sortDirection == "asc";
            switch (sortColumn)
            {
                case "FechaComprobante":
                    return ascending ? query.OrderBy(p => p.FechaComprobante) : query.OrderByDescending(p => p.FechaComprobante);
                case "Persona.Apellido":
                    return ascending ? query.OrderBy(p => p.Persona.Apellido) : query.OrderByDescending(p => p.Persona.Apellido);
                default:
                    return query.OrderByDescending(p => p.FechaComprobante);
            }
        }

        private byte[] _generateXlsxBytes(List<PagoTarjeta> datos)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Pagos");
                var dataToExport = datos.Select(p => new
                {
                    Cliente = $"{p.Persona?.Apellido}, {p.Persona?.Nombres}",
                    NroDocumento = p.Persona?.NroDocumento,
                    FechaVencimiento = p.FechaDePago?.ToString("dd/MM/yyyy") ?? "",
                    FechaComprobante = p.FechaComprobante?.ToString("dd/MM/yyyy HH:mm") ?? "",
                    MontoInformado = p.MontoInformado,
                    Estado = p.EstadoPago.ToString()
                }).ToList();

                worksheet.Cells.LoadFromCollection(dataToExport, true);

                worksheet.Cells["A1"].Value = "Cliente";
                worksheet.Cells["B1"].Value = "NroDocumento.";
                worksheet.Cells["C1"].Value = "Fecha Informada";
                worksheet.Cells["D1"].Value = "Fecha de Carga";
                worksheet.Cells["E1"].Value = "Monto Informado";
                worksheet.Cells["F1"].Value = "Estado";

                worksheet.Column(5).Style.Numberformat.Format = "$ #,##0.00";
                worksheet.Column(6).Style.Numberformat.Format = "$ #,##0.00";
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                return package.GetAsByteArray();
            }
        }

    }
}
