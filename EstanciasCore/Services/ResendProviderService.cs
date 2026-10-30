using DAL.Models;
using EstanciasCore.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

/// <summary>
/// Envío por la API de Resend (POST /emails).
/// Ante un error se devuelve la respuesta exacta de Resend (status, "name", "message" y el cuerpo completo).
/// Referencia de errores: https://resend.com/docs/api-reference/errors
/// </summary>
public class ResendProviderService : IMailProvider
{
    private const string Nombre = "RESEND";
    private readonly HttpClient _httpClient;
    public ResendProviderService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<ResultadoEnvio> EnviarAsync(MailAPI mail, MailConfig config, byte[] adjunto = null, string nombreAdjunto = "Resumen.pdf", string idempotencyKey = null)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, config.SmtpHost);
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            // Si se reintenta el mismo envío dentro de 24 h, Resend devuelve el resultado original sin mandarlo de nuevo
            if (!string.IsNullOrEmpty(idempotencyKey))
                request.Headers.Add("Idempotency-Key", idempotencyKey);

            var body = new ResendRequest
            {
                From = $"{config.SenderName} <{config.SenderEmail}>",
                To = mail.Mail,
                Subject = mail.Titulo,
                Html = mail.Html,
            };

            if (adjunto != null)
            {
                body.Attachments = new List<Attachments>
                {
                    new Attachments
                    {
                        Filename = nombreAdjunto,
                        Content = Convert.ToBase64String(adjunto)
                    }
                };
            }

            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(request);
            var contenido = await response.Content.ReadAsStringAsync();
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                string messageId = null;
                try { messageId = JObject.Parse(contenido)["id"]?.ToString(); } catch { /* respuesta sin id */ }
                return ResultadoEnvio.Ok(Nombre, messageId, status);
            }

            // Resend responde { "statusCode": ..., "name": "...", "message": "..." }
            string codigo = null, mensaje = null;
            try
            {
                var json = JObject.Parse(contenido);
                codigo = json["name"]?.ToString();
                mensaje = json["message"]?.ToString();
            }
            catch { /* el cuerpo no es JSON: se guarda tal cual */ }

            var error = $"HTTP {status} {response.ReasonPhrase}" +
                        (codigo != null ? $" | {codigo}" : "") +
                        (mensaje != null ? $" | {mensaje}" : "") +
                        $"{Environment.NewLine}Respuesta de Resend: {contenido}";

            return ResultadoEnvio.Falla(Nombre, error, EsReintentable(status, codigo), status, codigo ?? $"http_{status}");
        }
        catch (TaskCanceledException ex)
        {
            return ResultadoEnvio.Falla(Nombre, $"Timeout al llamar a Resend.{Environment.NewLine}{ex}", reintentable: true, codigoError: "timeout");
        }
        catch (HttpRequestException ex)
        {
            return ResultadoEnvio.Falla(Nombre, $"Error de red al llamar a Resend.{Environment.NewLine}{ex}", reintentable: true, codigoError: "error_red");
        }
        catch (Exception ex)
        {
            return ResultadoEnvio.Falla(Nombre, ex.ToString(), reintentable: true, codigoError: "excepcion");
        }
    }

    /// <summary>
    /// Decide si un error de Resend se reintenta.
    /// - 429 (rate_limit_exceeded, daily_quota_exceeded, monthly_quota_exceeded) y 5xx: temporales.
    /// - 401/403 (API key inválida o suspendida, dominio no verificado, modo testing): son de configuración
    ///   de la cuenta y afectan a todos los mails; se reintentan para que salgan solos cuando se corrija.
    /// - 409 concurrent_idempotent_requests: temporal. invalid_idempotent_request: definitivo.
    /// - 400/422 (validation_error, missing_required_field, invalid_attachment...): problema de este mail puntual.
    /// </summary>
    private static bool EsReintentable(int status, string codigo)
    {
        if (status == 429 || status == 408 || status >= 500) return true;
        if (status == 401 || status == 403) return true;
        if (status == 409) return codigo != "invalid_idempotent_request";
        return false;
    }
}
