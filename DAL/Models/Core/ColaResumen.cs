using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Models
{
    /// <summary>
    /// Una fila por cada corrida de un proceso (generación o envío de resúmenes).
    /// Permite saber si el proceso corrió, cuándo, cuánto tardó y cómo terminó.
    /// </summary>
    public class EjecucionProceso
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string Codigo { get; set; }            // "GenerarResumen" / "EnvioResumen"

        public int? PeriodoId { get; set; }
        [ForeignKey("PeriodoId")]
        public virtual Periodo Periodo { get; set; }

        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }

        [MaxLength(30)]
        public string Estado { get; set; }            // EstadosEjecucion

        public int Total { get; set; }
        public int Exitosos { get; set; }
        public int Fallidos { get; set; }
        public int Omitidos { get; set; }

        public string ErrorGeneral { get; set; }

        [MaxLength(100)]
        public string Servidor { get; set; }
    }

    /// <summary>
    /// Estado de la generación del resumen de un usuario en un período.
    /// Única por (PeriodoId, UsuarioId): al reintentar solo se procesan los que no están "Generado".
    /// </summary>
    public class ResumenGeneracion
    {
        public int Id { get; set; }

        public int PeriodoId { get; set; }
        [ForeignKey("PeriodoId")]
        public virtual Periodo Periodo { get; set; }

        [MaxLength(450)]
        public string UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public virtual Usuario Usuario { get; set; }

        public int? EjecucionProcesoId { get; set; }  // Última ejecución que lo tocó
        [ForeignKey("EjecucionProcesoId")]
        public virtual EjecucionProceso EjecucionProceso { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; }            // EstadosGeneracion

        [MaxLength(50)]
        public string CodigoError { get; set; }       // CodigosErrorGeneracion

        public string Mensaje { get; set; }
        public string Detalle { get; set; }           // Excepción completa

        public int Intentos { get; set; }

        public int? ResumenTarjetaId { get; set; }
        [ForeignKey("ResumenTarjetaId")]
        public virtual ResumenTarjeta ResumenTarjeta { get; set; }

        public DateTime FechaCreacion { get; set; }
        public DateTime FechaUltimoIntento { get; set; }
    }

    /// <summary>
    /// Cola de envío de resúmenes por mail (patrón "outbox").
    /// Cada resumen generado deja una fila acá; el EnvioDeResumenWorker las despacha.
    /// Única por ResumenTarjetaId para los envíos reales (EsPrueba = false).
    /// </summary>
    public class EnvioResumenMail
    {
        public int Id { get; set; }

        public int ResumenTarjetaId { get; set; }
        [ForeignKey("ResumenTarjetaId")]
        public virtual ResumenTarjeta ResumenTarjeta { get; set; }

        public int PeriodoId { get; set; }
        [ForeignKey("PeriodoId")]
        public virtual Periodo Periodo { get; set; }

        [MaxLength(450)]
        public string UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public virtual Usuario Usuario { get; set; }

        [MaxLength(256)]
        public string EmailDestino { get; set; }      // A quién se mandó (en prueba: la casilla de prueba)

        [MaxLength(256)]
        public string EmailCliente { get; set; }      // Mail real del cliente

        public bool EsPrueba { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; }            // EstadosEnvio

        [MaxLength(50)]
        public string MotivoOmision { get; set; }     // Si Estado = Omitido

        [MaxLength(30)]
        public string Proveedor { get; set; }
        [MaxLength(200)]
        public string ProveedorMessageId { get; set; }
        public int? HttpStatus { get; set; }
        [MaxLength(100)]
        public string CodigoErrorProveedor { get; set; } // Ej. Resend: rate_limit_exceeded, validation_error, daily_quota_exceeded
        public string Error { get; set; }                // Último error, exacto (respuesta completa del proveedor)

        public int Intentos { get; set; }
        public DateTime? ProximoIntento { get; set; }
        public DateTime? FechaUltimoIntento { get; set; }

        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaEnvio { get; set; }

        /// <summary>Todos los intentos con su error exacto (no se pisan entre reintentos).</summary>
        public virtual ICollection<EnvioResumenMailIntento> HistorialIntentos { get; set; }
    }

    /// <summary>
    /// Un registro por cada intento de envío, exitoso o no, con la respuesta exacta del proveedor.
    /// </summary>
    public class EnvioResumenMailIntento
    {
        public int Id { get; set; }

        public int EnvioResumenMailId { get; set; }
        [ForeignKey("EnvioResumenMailId")]
        public virtual EnvioResumenMail EnvioResumenMail { get; set; }

        public int NroIntento { get; set; }
        public DateTime Fecha { get; set; }
        public long DuracionMs { get; set; }

        [MaxLength(256)]
        public string EmailDestino { get; set; }

        [MaxLength(30)]
        public string Proveedor { get; set; }

        public bool Exito { get; set; }
        public int? HttpStatus { get; set; }

        [MaxLength(100)]
        public string CodigoErrorProveedor { get; set; }

        public string Error { get; set; }             // Texto exacto: respuesta del proveedor o excepción completa

        [MaxLength(200)]
        public string ProveedorMessageId { get; set; }

        [MaxLength(300)]
        public string IdempotencyKey { get; set; }

        public bool Reintentable { get; set; }
    }

    public static class CodigosProceso
    {
        public const string GenerarResumen = "GenerarResumen";
        public const string EnvioResumen = "EnvioResumen";
    }

    public static class EstadosEjecucion
    {
        public const string EnCurso = "EnCurso";
        public const string Completado = "Completado";
        public const string CompletadoConErrores = "CompletadoConErrores";
        public const string Fallido = "Fallido";
        public const string Cancelado = "Cancelado";
    }

    public static class EstadosGeneracion
    {
        public const string Generado = "Generado";
        public const string Fallido = "Fallido";
    }

    public static class CodigosErrorGeneracion
    {
        public const string TarjetaInvalida = "TARJETA_INVALIDA";
        public const string WsSinExito = "WS_SIN_EXITO";
        public const string DatosNulos = "DATOS_NULOS";
        public const string SinConfiguracion = "SIN_CONFIGURACION";
        public const string Excepcion = "EXCEPCION";
    }

    public static class EstadosEnvio
    {
        public const string Pendiente = "Pendiente";
        public const string Enviando = "Enviando";
        public const string Enviado = "Enviado";
        public const string Fallido = "Fallido";                     // Se reintenta
        public const string FallidoDefinitivo = "FallidoDefinitivo"; // No se reintenta más
        public const string Omitido = "Omitido";
    }

    public static class MotivosOmision
    {
        public const string NoRecibeResumen = "NO_RECIBE_RESUMEN";
        public const string SinSaldo = "SIN_SALDO";
        public const string SinEmail = "SIN_EMAIL";
        public const string SinPdf = "SIN_PDF";
        public const string PeriodoVencido = "PERIODO_VENCIDO";
    }
}
