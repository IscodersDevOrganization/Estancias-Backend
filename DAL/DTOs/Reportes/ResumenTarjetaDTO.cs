using DAL.DTOs.Servicios;
using System;
using System.Collections.Generic;

namespace DAL.DTOs.Reportes
{
    public class ResumenTarjetaDTO
    {
        public int Id { get; set; }

        public string NroTarjeta { get; set; } = "";
        public string Periodo { get; set; } = "";

        public int PeriodoId { get; set; }
        public string UsuarioId { get; set; } = "";

        public string FechaVencimiento { get; set; } = "";

        public decimal Monto { get; set; }
        public decimal Punitorios { get; set; }
        public decimal MontoTotal { get; set; }

        public string MontoTexto { get; set; } = "";
        public string PunitoriosTexto { get; set; } = "";
        public string MontoTotalTexto { get; set; } = "";

        public string DescargarResumenUrl { get; set; } = "";
        public string DescargarResumenArchivoUrl { get; set; } = "";
        public string VerComprobanteUrl { get; set; } = "";

        public string Accion { get; set; } = "";
    }

    public class DetallesCuotasResumenDTO
    {
        public string Fecha { get; set; } = "";
        public string Concepto { get; set; } = "";
        public string NroSolicitud { get; set; } = "";
        public string NroCuota { get; set; } = "";
        public string TotalDeCuotas { get; set; } = "";

        public decimal Monto { get; set; }
    }

    public class TempalteResumenDTO
    {
        public decimal SaldoAnterior { get; set; }
        public decimal Pagos { get; set; }
        public decimal SaldoActual { get; set; }
        public decimal SaldoPendiente { get; set; }
        public decimal SaldoTotal { get; set; }
        public decimal Intereses { get; set; }
        public decimal Impuestos { get; set; }

        public string Nombre { get; set; } = "";
        public string NroDocumento { get; set; } = "";
        public string Mail { get; set; } = "";
        public string NroSocio { get; set; } = "";
        public string NroTarjeta { get; set; } = "";
        public string Domicilio { get; set; } = "";
        public string PeriodoDesde { get; set; } = "";
        public string PeriodoHasta { get; set; } = "";
        public string Vencimiento { get; set; } = "";

        public List<DetallesCuotasResumenDTO> DetallesCuotas { get; set; } =
            new List<DetallesCuotasResumenDTO>();

        public List<ResultadoCuotasDTO> ConsumosAnteriores { get; set; } =
            new List<ResultadoCuotasDTO>();

        public List<ResultadoCuotasDTO> ConsumosDelMes { get; set; } =
            new List<ResultadoCuotasDTO>();
    }

    public class FiltroResumenTarjetaRequestDTO
    {
        public string NroTarjetaFiltro { get; set; } = "";
        public string NroDocumentoFiltro { get; set; } = "";

        // Alias para tarjeta
        public string NroTarjeta { get; set; } = "";
        public string Tarjeta { get; set; } = "";

        // Alias para documento
        public string NroDocumento { get; set; } = "";
        public string Documento { get; set; } = "";
        public string Dni { get; set; } = "";
        public string DNI { get; set; } = "";

        // Alias para CUIT / CUIL
        public string Cuit { get; set; } = "";
        public string CUIT { get; set; } = "";
        public string Cuil { get; set; } = "";
        public string CUIL { get; set; } = "";

        // Búsqueda general
        public string Buscar { get; set; } = "";
        public string Busqueda { get; set; } = "";

        public int Pagina { get; set; } = 1;
        public int Cantidad { get; set; } = 50;
    }

    public class ResumenTarjetaListadoResponseDTO
    {
        public int Status { get; set; }
        public string Mensaje { get; set; } = "";
        public string UsuarioId { get; set; } = "";

        public List<ResumenTarjetaDTO> Data { get; set; } =
            new List<ResumenTarjetaDTO>();
    }

    public class ResumenArchivoDTO
    {
        public int Status { get; set; }
        public string Mensaje { get; set; } = "";

        public int PeriodoId { get; set; }
        public string UsuarioId { get; set; } = "";

        public string Base64 { get; set; } = "";
        public string ContentType { get; set; } = "";
        public string FileName { get; set; } = "";
    }

    public class MovimientoResumenDeudaDTO
    {
        public string Fecha { get; set; } = "";
        public string TipoMovimiento { get; set; } = "";
        public string Monto { get; set; } = "";
    }

    public class DetalleCuotaConSolicitudDTO
    {
        public string NroSolicitud { get; set; } = "";
        public string Fecha { get; set; } = "";
        public string Monto { get; set; } = "";
        public string NroCuota { get; set; } = "";
    }

    public class ResumenDeudaResponseDTO
    {
        public int Status { get; set; }
        public string Mensaje { get; set; } = "";

        public string UsuarioId { get; set; } = "";
        public string NroDocumento { get; set; } = "";
        public string NroTarjeta { get; set; } = "";

        public string MontoDisponible { get; set; } = "";
        public string FechaVencimiento { get; set; } = "";
        public decimal MontoPunitoriosTotal { get; set; }

        public List<MovimientoResumenDeudaDTO> Movimientos { get; set; } =
            new List<MovimientoResumenDeudaDTO>();

        public List<DetalleCuotaConSolicitudDTO> DetallesCuotas { get; set; } =
            new List<DetalleCuotaConSolicitudDTO>();
    }

    public class ResumenDeudaListadoResponseDTO
    {
        public int Status { get; set; }
        public string Mensaje { get; set; } = "";

        public int TotalRegistros { get; set; }
        public int PaginaActual { get; set; }
        public int CantidadPorPagina { get; set; }
        public int TotalPaginas { get; set; }

        public List<ResumenDeudaResponseDTO> Data { get; set; } =
            new List<ResumenDeudaResponseDTO>();
    }
    public class SimularResumenTarjetaRequestDTO
    {
        public string DocumentoOTarjeta { get; set; } = "";

        // "id:123" para un período existente o "nuevo:2026-12-15" para uno calculado.
        public string Periodo { get; set; } = "";
        public DateTime? FechaGeneracion { get; set; }
    }

    public class PeriodoSimulacionResumenDTO
    {
        public string Value { get; set; } = "";
        public string Text { get; set; } = "";
        public bool Selected { get; set; }
    }
}