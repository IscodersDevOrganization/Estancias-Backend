using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace EstanciasCore
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                // Los workers de resúmenes loguean también a nivel Information (antes se perdía todo)
                .MinimumLevel.Override("ResumenMensualWorker", LogEventLevel.Information)
                .MinimumLevel.Override("EnvioDeResumenWorker", LogEventLevel.Information)
                .MinimumLevel.Override("ResumenTarjetaService", LogEventLevel.Information)
                .MinimumLevel.Override("EstanciasCore.Services.EnvioResumenService", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Debug(LogEventLevel.Warning)
                .WriteTo.File("Logs/log.txt", LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("Starting web host");
                CreateWebHostBuilder(args).Build().Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

        }


        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseIISIntegration()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseSerilog();
    }
}
