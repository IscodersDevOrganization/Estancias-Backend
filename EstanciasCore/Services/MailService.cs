using DAL.Data;
using EstanciasCore.Interface;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

public class MailService : IMailService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EstanciasContext _context;

    public MailService(IServiceProvider serviceProvider, EstanciasContext context)
    {
        _serviceProvider = serviceProvider;
        _context = context;
    }

    public async Task<bool> EnviarAsync(MailAPI mail, byte[] adjunto = null)
    {
        var resultado = await EnviarConResultadoAsync(mail, adjunto);
        return resultado.Exito;
    }

    public async Task<ResultadoEnvio> EnviarConResultadoAsync(MailAPI mail, byte[] adjunto = null, string nombreAdjunto = "Resumen.pdf", string idempotencyKey = null)
    {
        try
        {
            var config = _context.MailConfig.FirstOrDefault(c => c.Activo);
            if (config == null)
                return ResultadoEnvio.Falla("SIN_CONFIG", "No hay ninguna configuración de mail activa en la tabla MailConfig.", reintentable: true, codigoError: "sin_configuracion");

            IMailProvider provider;
            if ((config.CodigoProveedor ?? "").ToUpper() == "RESEND")
                provider = _serviceProvider.GetService<ResendProviderService>();
            else
                provider = _serviceProvider.GetService<BrevoSmtpProviderService>();

            if (provider == null)
                return ResultadoEnvio.Falla(config.CodigoProveedor, "No se pudo resolver el proveedor de mail en el contenedor de dependencias.", reintentable: true, codigoError: "sin_proveedor");

            return await provider.EnviarAsync(mail, config, adjunto, nombreAdjunto, idempotencyKey);
        }
        catch (Exception ex)
        {
            return ResultadoEnvio.Falla("DESCONOCIDO", ex.ToString(), reintentable: true, codigoError: "excepcion");
        }
    }
}
