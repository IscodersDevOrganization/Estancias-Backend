using DAL.DTOs.Servicios;
using DAL.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EstanciasCore.Interface
{
    public class ResultadoGeneracionResumen
    {
        public int? EjecucionId { get; set; }
        public int? PeriodoId { get; set; }
        public string Estado { get; set; }      // DAL.Models.EstadosEjecucion
        public int Total { get; set; }
        public int Generados { get; set; }
        public int Fallidos { get; set; }
        public int Encolados { get; set; }      // Resúmenes que quedaron Pendientes de envío
        public int OmitidosEnvio { get; set; }  // Resúmenes que no se van a enviar (no recibe, sin saldo, sin mail)
        public string Error { get; set; }
    }

    public interface IResumenTarjetaService
    {
        /// <summary>
        /// Genera el resumen (PDF) de cada usuario pendiente del período actual.
        /// Por cada usuario registra el resultado en ResumenGeneracion y, si se generó,
        /// deja el envío por mail en la cola (EnvioResumenMail). No envía mails.
        /// Es idempotente: los usuarios que ya tienen resumen en el período se saltean.
        /// </summary>
        Task<ResultadoGeneracionResumen> GenerarResumenTarjetas(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Genera el resumen de un usuario para un período con la MISMA lógica que la generación mensual,
        /// pero NO guarda nada: devuelve el PDF con la marca de agua "SIMULACIÓN".
        /// fechaGeneracion reemplaza a "hoy" (mes de las cuotas y corte de mora de los punitorios).
        /// </summary>
        Task<ResultadoSimulacionResumen> SimularResumenAsync(UsuarioParaProcesarDTO usuario, Periodo periodo, DateTime fechaGeneracion, string leyenda);
    }

    public class ResultadoSimulacionResumen
    {
        public bool Ok { get; set; }
        public string CodigoError { get; set; }
        public string Mensaje { get; set; }
        public string Detalle { get; set; }
        public string ResultadoWs { get; set; }

        public string Nombre { get; set; }
        public string NroDocumento { get; set; }
        public string NroTarjeta { get; set; }
        public string Mail { get; set; }

        public string Periodo { get; set; }
        public DateTime PeriodoDesde { get; set; }
        public DateTime PeriodoHasta { get; set; }
        public DateTime Vencimiento { get; set; }
        public DateTime FechaGeneracion { get; set; }

        public decimal SaldoPendiente { get; set; }
        public decimal SaldoActual { get; set; }
        public decimal SaldoTotal { get; set; }
        public int CantidadConsumosAnteriores { get; set; }
        public int CantidadConsumosDelMes { get; set; }

        public byte[] Pdf { get; set; }
    }
}
