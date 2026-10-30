using DAL.Models;
using System;
using System.Collections.Generic;

namespace DAL.DTOs.Reportes
{
    public class ColaResumenesPanelDTO
    {
        public List<PeriodoOpcionDTO> Periodos { get; set; } = new List<PeriodoOpcionDTO>();
        public int? PeriodoId { get; set; }
        public string PeriodoDescripcion { get; set; }
        public DateTime? PeriodoVencimiento { get; set; }

        // Configuración actual
        public string ModoEnvio { get; set; }
        public string EmailsPrueba { get; set; }
        public bool EnvioActivo { get; set; }
        public bool GeneracionActiva { get; set; }
        public int? DiaGeneracion { get; set; }
        public DateTime? UltimaGeneracionExitosa { get; set; }

        // Generación
        public int Generados { get; set; }
        public int FallidosGeneracion { get; set; }
        public List<ConteoDTO> FallosGeneracionPorCodigo { get; set; } = new List<ConteoDTO>();

        // Envío (sin contar pruebas)
        public int Pendientes { get; set; }
        public int Enviando { get; set; }
        public int Enviados { get; set; }
        public int Reintentando { get; set; }
        public int FallidosDefinitivos { get; set; }
        public int Omitidos { get; set; }
        public int EnviosPrueba { get; set; }
        public DateTime? ProximoReintento { get; set; }
        public List<ConteoDTO> OmitidosPorMotivo { get; set; } = new List<ConteoDTO>();
        public List<ConteoDTO> ErroresPorCodigo { get; set; } = new List<ConteoDTO>();

        public List<EjecucionProceso> Ejecuciones { get; set; } = new List<EjecucionProceso>();
    }

    public class PeriodoOpcionDTO
    {
        public int Id { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaVencimiento { get; set; }
    }

    public class ConteoDTO
    {
        public string Clave { get; set; }
        public int Cantidad { get; set; }
    }

    public class GeneracionItemDTO
    {
        public int Id { get; set; }
        public string UsuarioId { get; set; }
        public string Email { get; set; }
        public string NroDocumento { get; set; }
        public string NroTarjeta { get; set; }
        public string Estado { get; set; }
        public string CodigoError { get; set; }
        public string Mensaje { get; set; }
        public string Detalle { get; set; }
        public int Intentos { get; set; }
        public DateTime FechaUltimoIntento { get; set; }
        public int? ResumenTarjetaId { get; set; }
        public int? EjecucionProcesoId { get; set; }
    }

    public class EnvioItemDTO
    {
        public int Id { get; set; }
        public int ResumenTarjetaId { get; set; }
        public int PeriodoId { get; set; }
        public string UsuarioId { get; set; }
        public string EmailCliente { get; set; }
        public string EmailDestino { get; set; }
        public string NroDocumento { get; set; }
        public bool EsPrueba { get; set; }
        public string Estado { get; set; }
        public string MotivoOmision { get; set; }
        public string Proveedor { get; set; }
        public int? HttpStatus { get; set; }
        public string CodigoErrorProveedor { get; set; }
        public string Error { get; set; }
        public string ProveedorMessageId { get; set; }
        public int Intentos { get; set; }
        public DateTime? FechaUltimoIntento { get; set; }
        public DateTime? ProximoIntento { get; set; }
        public DateTime? FechaEnvio { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class EnvioDetalleDTO
    {
        public EnvioItemDTO Envio { get; set; }
        public List<EnvioResumenMailIntento> HistorialIntentos { get; set; } = new List<EnvioResumenMailIntento>();
    }
}
