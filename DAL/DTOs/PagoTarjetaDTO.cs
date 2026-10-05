using DAL.Models.Core;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace DAL.DTOs
{
    public class PagoTarjetaDTO
    {
        public int Id { get; set; }
        public string Persona { get; set; }
        public string NroDocumento { get; set; }
        public string Usuario { get; set; }
        public string NroTarjeta { get; set; }
        public DateTime FechaVencimiento { get; set; } = new DateTime();
        public Decimal MontoAdeudado { get; set; }
        public DateTime FechaPagoProximaCuota { get; set; } = new DateTime();
        public virtual EstadoPago EstadoPago { get; set; }
        public byte[] ComprobantePago { get; set; }
    }

    public class PagoTarjetaDataTableDTO
    {
        public int Id { get; set; }
        public string Persona { get; set; }
        public string NroDocumento { get; set; }
        public string Usuario { get; set; }
        public string NroTarjeta { get; set; }
        public string FechaDePago { get; set; }
        public string FechaVencimiento { get; set; }
        public decimal MontoAdeudado { get; set; }
        public decimal MontoInformado { get; set; }
        public string FechaPagoProximaCuota { get; set; }
        public string FechaComprobante { get; set; }
        public string EstadoPago { get; set; }
        public int EstadoPagoId { get; set; }
        public bool ComprobantePago { get; set; }
        public int FechaOrden { get; set; }
        public string Observacion { get; set; }
    }

    public class RechazarComprobanteDTO
    {
        public int Id { get; set; }

        [JsonPropertyName("observacion")]
        public string Observacion { get; set; }
    }

    public class RechazarComprobanteMasivoDTO
    {
        public List<int> Ids { get; set; }
        public string Observacion { get; set; }
    }

    public class ExportarPagoTarjetaExcelRequestDTO
    {
        /// <summary>
        /// Si es true, exporta todos los registros encontrados
        /// utilizando los filtros enviados.
        /// </summary>
        public bool ExportarTodos { get; set; }

        /// <summary>
        /// IDs seleccionados manualmente.
        /// Se utiliza cuando ExportarTodos es false.
        /// </summary>
        public List<int> Ids { get; set; } = new List<int>();

        public string Buscar { get; set; }

        public int? Estado { get; set; }

        public DateTime? Fecha { get; set; }
    }

    public class PagoTarjetaExcelFilaDTO
    {
        public string Cliente { get; set; }

        public string NroDocumento { get; set; }

        public DateTime? FechaInformada { get; set; }

        public DateTime? FechaDeCarga { get; set; }

        public decimal MontoInformado { get; set; }

        public int EstadoPagoId { get; set; }
    }
}