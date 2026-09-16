using Commons.Extensions;
using Commons.Identity.DummyData;
using Commons.Identity.Services;
using DAL.Data;
using DAL.Models;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;

namespace EstanciasCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
            });
            services.AddDbContext<EstanciasContext>(options =>
                options.UseLazyLoadingProxies()
                        .UseSqlServer(
                    Configuration.GetConnectionString("Estancias"),
                    x => x.MigrationsAssembly("DAL")));

            services.Configure<IISServerOptions>(options =>
            {
                options.AutomaticAuthentication = false;

            });

            services.AddAuthentication() // O AddDefaultIdentity o AddIdentity
            .AddCookie(options =>
            {
                options.LoginPath = "/Identity/Account/Login";
                // options.LogoutPath = "/Identity/Account/Logout";
                // options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            });

            services.AddCommonsServices<Usuario, EstanciasContext>();
            services.AddDistributedMemoryCache();
            services.AddCommonsLibraryViews();
            services.AddHttpContextAccessor();
            services.AddTransient<NotificacionAPIService>();
            services.AddTransient<IDatosTarjetaService, DatosTarjetaService>();
            services.AddTransient<IResumenTarjetaService, ResumenTarjetaService>();
            services.AddTransient<IPushService, OneSignalService>();
            services.AddTransient<MercadoPagoServices>();
            services.AddTransient<ObtenerPuntosService>();


            services.AddHttpClient<ResendProviderService>();
            services.AddTransient<BrevoSmtpProviderService>();
            services.AddTransient<IMailService, MailService>();

            //Genera Resumen Mensual
            services.AddHostedService<ResumenMensualWorker>();
            services.AddHostedService<EnvioDeResumenWorker>();
            services.AddHostedService<WonderPushWorker>();

            services.AddSession();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddMvcOptions(options => {
                    options.Conventions.Add(new EstanciasCore.Services.DisableLibraryControllersConvention());
                    options.MaxModelValidationErrors = 50;
                    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(
                        _ => "El campo es obligatorio.");
                })
                .AddRazorPagesOptions(options =>
                {
                    options.AllowAreas = true;
                    options.Conventions.AuthorizeAreaFolder("Identity", "/Account/Manage");
                    options.Conventions.AuthorizeAreaPage("Identity", "/Account/Logout");
                }
                ).AddJsonOptions(options =>
                {
                    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
                });

                services.ConfigureApplicationCookie(options =>
                {
                    options.LoginPath = $"/Identity/Account/Login";
                    options.LogoutPath = $"/Identity/Account/Logout";
                    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
                });
            services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 1;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, UserService<Usuario> userService, EstanciasContext context)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
                //DummyAdmin.Initialize<Usuario>(userService).Wait();
            }
            else
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
                app.UseHsts();
                DummyAdmin.Initialize<Usuario>(userService).Wait();
            }

            app.UseCors("CorsPolicy");
            app.UseStaticFiles();
            app.UseSession();
            app.UseCommonsLibraryScripts();
            app.UseAuthentication();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                  name: "areas",
                  template: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
              );
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
            app.UseCookiePolicy();
        }
    }
}
