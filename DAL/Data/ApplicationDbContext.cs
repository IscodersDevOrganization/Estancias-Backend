using System.Collections.Generic;
using Commons.Identity;
using DAL.Models;
using DAL.Models.Core;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data
{
    public class EstanciasContext : CommonsDbContext<Usuario>
    {
        public EstanciasContext(DbContextOptions<EstanciasContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        //BASICAS
        public DbSet<DatosEstructura> DatosEstructura { get; set; }
        public DbSet<Provincia> Provincia { get; set; }
        public DbSet<Localidad> Localidad { get; set; }
        public DbSet<TipoDocumento> TipoDocumento { get; set; }
        public DbSet<Paises> Paises { get; set; }
        public DbSet<LeyendaTipoMovimiento> LeyendaTipoMovimiento { get; set; }

        //CLIENTES
        public DbSet<Clientes> Clientes { get; set; }
        public DbSet<TiposClientes> TiposClientes { get; set; }
        public DbSet<Referencia> Referencias { get; set; }

        //EMPRESAS
        public DbSet<Empresas> Empresas { get; set; }
        public DbSet<Grupos> Grupos { get; set; }
        public DbSet<Sucursales> Sucursales { get; set; }
        public DbSet<SucursalesMarcas> SucursalesMarcas { get; set; }

        public DbSet<Vendedores> Vendedores { get; set; }
        public DbSet<Monedas> Monedas { get; set; }

        //CORE
        public DbSet<UAT> UAT { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Persona> Personas { get; set; }
        public DbSet<Marcas> Marcas { get; set; }

        //MOBILE
        public DbSet<Invitaciones> Invitaciones { get; set; }
        public DbSet<Servicios> Servicios { get; set; }
        public DbSet<ClientesServicios> ClientesServicios { get; set; }
        public DbSet<Novedades> Novedades { get; set; }
        public DbSet<Banners> Banners { get; set; }
        public DbSet<ImagenIntro> ImagenIntro { get; set; }
        public DbSet<Promociones> Promociones { get; set; }
        public DbSet<PromocionesQR> PromocionesQR { get; set; }
        public DbSet<NotificacionesPersonas> NotificacionesPersonas { get; set; }
        public DbSet<Reservas> Reservas { get; set; }
        public DbSet<Horarios> Horarios { get; set; }
        public DbSet<CuentaCorriente> CuentaCorriente { get; set; }
        public DbSet<Conceptos> Conceptos { get; set; }
        public DbSet<PuestosCodigos> PuestosCodigos { get; set; }
        public DbSet<Accesos> Accesos { get; set; }
        public DbSet<EstadosDeudas> EstadosDeudas { get; set; }
        public DbSet<UsuariosCategorias> UsuariosCategorias { get; set; }
        public DbSet<MenuMobile> MenuMobile { get; set; }
        public DbSet<MenuMobileUsuariosHabilitados> MenuMobileUsuariosHabilitados { get; set; }

        //CAMPANAS PUBLICITARIAS
        public DbSet<Campanas> Campanas { get; set; }
        public DbSet<CampanasRenglones> CampanasRenglones { get; set; }

        //Billetera
        public DbSet<Billetera> Billeteras { get; set; }
        public DbSet<MovimientoBilletera> MovimientosBilletera { get; set; }
        public DbSet<TipoMovimientoBilletera> TipoMovimientoBilletera { get; set; }
        public DbSet<Compras> ComprasProductos { get; set; }
        public DbSet<PagoTarjeta> PagoTarjeta { get; set; }
        public DbSet<TipoMovimientoTarjeta> TipoMovimientoTarjeta { get; set; }

        //NOTIFICACIONE
        public DbSet<ListaDistribucion> ListaDistribucion { get; set; }
        public DbSet<DistribucionDestinatarios> DistribucionDestinatarios { get; set; }
        public DbSet<EnvioNotificacionesDestinatarios> EnvioNotificacionesDestinatarios { get; set; }
        public DbSet<EnvioNotificaciones> EnvioNotificaciones { get; set; }

        //NEW
        public DbSet<Notificaciones> Notificaciones { get; set; }
        public DbSet<TipoNotificacionesProcedimientos> TipoNotificacionesProcedimientos { get; set; }
        public DbSet<NotificacionesPlantillas> NotificacionesPlantillas { get; set; }

        //PremiosCanje
        public DbSet<Categorias> Categorias { get; set; }
        public DbSet<Premios> Premios { get; set; }
        public DbSet<RelacionPuntos> RelacionPuntos { get; set; }
        public DbSet<PuntosClientes> PuntosClientes { get; set; }
        public DbSet<HistorialCanje> HistorialCanje { get; set; }
        public DbSet<HistorialDePuntos> HistorialDePuntos { get; set; }
        public DbSet<FotosPremios> FotosPremios { get; set; }
        public DbSet<PuntosObtenidosClientes> PuntosObtenidosClientes { get; set; }



        public DbSet<Catalogo> Catalogo { get; set; }
        public DbSet<Configuracion> Configuracion { get; set; }
        public DbSet<ConciliacionDePago> ConciliacionDePago { get; set; }
        public DbSet<Periodo> Periodo { get; set; }
        public DbSet<MovimientoTarjeta> MovimientoTarjeta { get; set; }

        //para ejecutar procedimientos almacenados o validaciones
        public DbSet<Procedimientos> Procedimientos { get; set; }
        public DbSet<LogProcedimientos> LogProcedimientos { get; set; }
        public DbSet<LogResumenesTarjetas> LogResumenesTarjetas { get; set; }
        public DbSet<ResumenTarjeta> ResumenTarjeta { get; set; }
        public DbSet<DistribucionResumen> DistribucionResumen { get; set; }

        //Pre Resgitro de Categorias
        public DbSet<PreRegistroCategorias> PreRegistroCategorias { get; set; }
        public DbSet<Morosos> Morosos { get; set; }
        public DbSet<HistoricoDeCargaMorosos> HistoricoDeCargaMorosos { get; set; }
        public DbSet<SolicitudDeTarjeta> SolicitudDeTarjeta { get; set; }
        public DbSet<EstadoSolicitudDeTarjeta> EstadoSolicitudDeTarjeta { get; set; }

        //MAIL
        public DbSet<MailConfig> MailConfig { get; set; }


        //MOCK API EXTERNA DE PAGOS Y COBROS
        public DbSet<MockServicio> MockServicios { get; set; }

        #region Proveedores
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<Rubro> Rubros { get; set; }
        public DbSet<ProveedorRubro> ProveedorRubros { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Financiacion> FinanciacionProductos { get; set; }
        public DbSet<ProductoPrecompra> ProductosPrecompras { get; set; }
        public DbSet<Talles> Talles { get; set; }
        public DbSet<TallesProductos> TallesProductos { get; set; }
        public DbSet<TipoProducto> TipoProducto { get; set; }

        #endregion

        #region Productos de Clientes
        public DbSet<ProductoCliente> ProductosClientes { get; set; }
        public DbSet<ProductoClienteContacto> ProductosClientesContactos { get; set; }
        public DbSet<ProductoAdjuntos> ProductoAdjuntos { get; set; }
        public IEnumerable<object> Tarjetas { get; set; }
        #endregion
        public override List<IWorkSpace> GetIWorkSpaces()
        {
            return new List<IWorkSpace>();
        }



        protected override void OnModelCreating(ModelBuilder builder)
        {
            //base.Database.SetCommandTimeout(0);
            base.OnModelCreating(builder);
            builder.Entity<Usuario>()
                .HasOne(c => c.Clientes)
                .WithOne(u => u.Usuario)
                .HasForeignKey<Clientes>(x => x.UsuarioId);
        }


    }
}
