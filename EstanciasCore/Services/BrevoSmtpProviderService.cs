using DAL.Models;
using EstanciasCore.Interface;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

public class BrevoSmtpProviderService : IMailProvider
{
    private const string Nombre = "BREVO";

    // idempotencyKey no aplica a SMTP (se ignora)
    public async Task<ResultadoEnvio> EnviarAsync(MailAPI mail, MailConfig config, byte[] adjunto = null, string nombreAdjunto = "Resumen.pdf", string idempotencyKey = null)
    {
        try
        {
            using (var smtp = new SmtpClient(config.SmtpHost, config.SmtpPort))
            using (var message = new MailMessage
            {
                From = new MailAddress(config.SenderEmail, config.SenderName),
                Subject = mail.Titulo,
                Body = mail.Html,
                IsBodyHtml = true
            })
            {
                smtp.Credentials = new NetworkCredential(config.SmtpUser, config.SmtpPass);
                smtp.EnableSsl = true;
                message.To.Add(mail.Mail);

                // Antes el PDF nunca se adjuntaba con Brevo
                if (adjunto != null && adjunto.Length > 0)
                {
                    message.Attachments.Add(new Attachment(new MemoryStream(adjunto), nombreAdjunto, "application/pdf"));
                }

                await smtp.SendMailAsync(message);
                return ResultadoEnvio.Ok(Nombre);
            }
        }
        catch (FormatException ex)
        {
            // Dirección de mail mal formada: no tiene sentido reintentar
            return ResultadoEnvio.Falla(Nombre, $"Mail inválido: {ex.Message}", reintentable: false);
        }
        catch (SmtpFailedRecipientException ex)
        {
            bool definitivo = ex.StatusCode == SmtpStatusCode.MailboxUnavailable
                           || ex.StatusCode == SmtpStatusCode.MailboxNameNotAllowed
                           || ex.StatusCode == SmtpStatusCode.UserNotLocalTryAlternatePath;
            return ResultadoEnvio.Falla(Nombre, $"SMTP {(int)ex.StatusCode} ({ex.StatusCode}): {ex.Message}", reintentable: !definitivo, statusCode: (int)ex.StatusCode);
        }
        catch (SmtpException ex)
        {
            return ResultadoEnvio.Falla(Nombre, $"SMTP {(int)ex.StatusCode} ({ex.StatusCode}): {ex}", reintentable: true, statusCode: (int)ex.StatusCode);
        }
        catch (Exception ex)
        {
            return ResultadoEnvio.Falla(Nombre, ex.ToString(), reintentable: true);
        }
    }
}
