using DAL.Models;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

namespace EstanciasCore.Interface
{
    /// <summary>
    /// Resultado detallado de un envío de mail. Permite guardar en la base
    /// el error exacto y decidir si conviene reintentar.
    /// </summary>
    public class ResultadoEnvio
    {
        public bool Exito { get; set; }
        public string Proveedor { get; set; }
        public string MessageId { get; set; }
        public int? StatusCode { get; set; }

        /// <summary>Código de error del proveedor (en Resend, el campo "name": rate_limit_exceeded, validation_error, etc.).</summary>
        public string CodigoError { get; set; }

        /// <summary>Error exacto: respuesta completa del proveedor o la excepción completa.</summary>
        public string Error { get; set; }

        /// <summary>
        /// true = error temporal o de configuración de la cuenta (429, 5xx, API key, dominio): se reintenta.
        /// false = error del mail puntual (destinatario inválido, datos inválidos): no se reintenta.
        /// </summary>
        public bool EsReintentable { get; set; }

        public static ResultadoEnvio Ok(string proveedor, string messageId = null, int? statusCode = null) =>
            new ResultadoEnvio { Exito = true, Proveedor = proveedor, MessageId = messageId, StatusCode = statusCode };

        public static ResultadoEnvio Falla(string proveedor, string error, bool reintentable, int? statusCode = null, string codigoError = null) =>
            new ResultadoEnvio { Exito = false, Proveedor = proveedor, Error = error, EsReintentable = reintentable, StatusCode = statusCode, CodigoError = codigoError };
    }

    public interface IMailProvider
    {
        Task<ResultadoEnvio> EnviarAsync(MailAPI mail, MailConfig config, byte[] adjunto = null, string nombreAdjunto = "Resumen.pdf", string idempotencyKey = null);
    }

    public interface IMailService
    {
        /// <summary>Compatibilidad con el código existente (controllers).</summary>
        Task<bool> EnviarAsync(MailAPI mail, byte[] adjunto = null);

        /// <summary>
        /// Devuelve el detalle del envío (proveedor, id, status, error exacto).
        /// idempotencyKey: si se repite la misma clave con el mismo contenido dentro de 24 h, Resend no vuelve a mandar el mail.
        /// </summary>
        Task<ResultadoEnvio> EnviarConResultadoAsync(MailAPI mail, byte[] adjunto = null, string nombreAdjunto = "Resumen.pdf", string idempotencyKey = null);
    }
}
