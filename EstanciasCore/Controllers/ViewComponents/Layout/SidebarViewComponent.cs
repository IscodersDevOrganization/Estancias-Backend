using Castle.Core.Internal;
using Commons.Extensions;
using Commons.Helpers;
using Commons.Identity.Extensions;
using Commons.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using DAL.Data;

namespace EstanciasCore.Controllers.ViewComponents.Layout
{
    public class SidebarViewComponent : ViewComponent
    {
        private readonly EstanciasContext _context;

        public SidebarViewComponent(EstanciasContext context)
        {
            _context = context;
        }
        private bool UserHasAnyRoute(params string[] routes)
        {
            if (routes == null || routes.Length == 0) return false;
            foreach (var r in routes)
            {
                if (string.IsNullOrEmpty(r)) continue;
                if (HttpContext.UserHasRoute(r)) return true;

                if (!r.StartsWith("/Core/", StringComparison.OrdinalIgnoreCase) &&
                    !r.StartsWith("/Administracion/", StringComparison.OrdinalIgnoreCase) &&
                    !r.StartsWith("/Reportes/", StringComparison.OrdinalIgnoreCase))
                {
                    if (HttpContext.UserHasRoute("/Core" + (r.StartsWith("/") ? r : "/" + r))) return true;
                    if (HttpContext.UserHasRoute("/Administracion" + (r.StartsWith("/") ? r : "/" + r))) return true;
                    if (HttpContext.UserHasRoute("/Reportes" + (r.StartsWith("/") ? r : "/" + r))) return true;
                }
                else
                {
                    var slashIdx = r.IndexOf('/', 1);
                    if (slashIdx > 0)
                    {
                        var withoutArea = r.Substring(slashIdx);
                        if (HttpContext.UserHasRoute(withoutArea)) return true;
                    }
                }
            }
            return false;
        }

        public IViewComponentResult Invoke(string filter)
        {

            bool userIsAdmin = ((ClaimsPrincipal)User).IsAdmin();
            System.Security.Principal.IPrincipal currentUser = this.User;
            var sidebars = new List<SidebarMenu>();
            sidebars.Add(MenuHelpers.AddHeader("Menú Principal"));
            var datos = MenuHelpers.AddTree("Datos", "fa fa-database");
            var generales = MenuHelpers.AddTree("Generales", "fa fa-circle-o text-green");
            var especificos = MenuHelpers.AddTree("Específicos", "fa fa-circle-o text-green");
            var notificaciones = MenuHelpers.AddTree("Notificaciones", "fa fa-circle-o");
            var puntosycupones = MenuHelpers.AddTree("Puntos y Cupones", "fa fa-circle-o");
            var gestion = MenuHelpers.AddTree("Gestión", "fa fa-cubes");
            //var reportes = MenuHelpers.AddTree("Reportes", "fa fa-print text-white");
            var sesion = MenuHelpers.AddTree("Sesión", "fa fa-key");
            var sistema = MenuHelpers.AddTree("Sistema", "fa fa-gear");
            var reportes = MenuHelpers.AddTree("Reportes", "fa fa-print");

            var rolesusuario = _context.Users.Where(x => x.UserName == User.Identity.Name).FirstOrDefault();
            if (userIsAdmin == true || rolesusuario.Administradores == true)
            {
                generales.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Tipos de Documento", "/Core/TiposDocumentos/"),
                    MenuHelpers.AddModule("Monedas", "/Core/Monedas/"),
                    MenuHelpers.AddModule("Sucursales", "/Core/Sucursales/"),
                    MenuHelpers.AddModule("Paises", "/Core/Paises/"),
                    MenuHelpers.AddModule("Provincias", "/Core/Provincias/"),
                    MenuHelpers.AddModule("Localidad", "/Core/Localidad/"),
                    MenuHelpers.AddModule("Tipos de Clientes", "/Core/TiposClientes/"),
                    MenuHelpers.AddModule("Grupos", "/Core/Grupos/"),
                    MenuHelpers.AddModule("Empresas", "/Core/Empresas/"),
                    MenuHelpers.AddModule("Conceptos", "/Core/Conceptos/"),
                    MenuHelpers.AddModule("Cuentas Corrientes", "/Core/CuentasCorrientes/"),
                    MenuHelpers.AddModule("Tipos Movimientos Billetera", "/Core/TipoMovimientoBilletera/"),
                    MenuHelpers.AddModule("Movimientos de Billetera", "/Core/MovimientoBilletera/"),
                    MenuHelpers.AddModule("Billetera", "/Core/Billetera/"),
                    //MenuHelpers.AddModule("Inversores", "/Core/Inversores/"),
                    MenuHelpers.AddModule("Leyendas", "/Core/LeyendaTipoMovimiento/"),
                    MenuHelpers.AddModule("Categoria de Usuarios", "/Core/UsuarioCategorias"),
                };
                datos.TreeChild.Add(generales);
                especificos.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Proveedor - Rubros", "/Core/Rubro/"),
                };
                datos.TreeChild.Add(especificos);

                notificaciones.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Notificación", "/Core/Notificaciones/"),
                    MenuHelpers.AddModule("Plantillas", "/Core/NotificacionesPlantillas/"),
                    MenuHelpers.AddModule("Lista de Distribución", "/Core/ListaDistribucion/")
                };

                puntosycupones.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Validar Cupón", "/Core/Cupon"),
                    MenuHelpers.AddModule("Cupones", "/Core/Premios"),
                    MenuHelpers.AddModule("Puntos Clientes", "/Core/PuntosObtenidosClientes"),
                    MenuHelpers.AddModule("Categorias de Cupones", "/Core/Categorias"),
                    MenuHelpers.AddModule("Relación Puntos", "/Core/RelacionPuntos/"),
                };



                gestion.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Clientes", "/Core/Clientes"),
					MenuHelpers.AddModule("Pagos Tarjeta", "/Core/PagoTarjeta"),
					MenuHelpers.AddModule("Histórico de Pagos Tarjeta", "/Core/PagoTarjetaHistorico"),
                    notificaciones,
                    puntosycupones,
                    //MenuHelpers.AddModule("Datos Empresa", "/Core/DatosEmpresa/"),
                    MenuHelpers.AddModule("Vendedores", "/Core/Vendedores/"),
                    MenuHelpers.AddModule("Novedades", "/Core/Novedades"),
                    MenuHelpers.AddModule("Promociones", "/Core/Promociones"),
                    MenuHelpers.AddModule("Banners", "/Core/Banners"),
                    MenuHelpers.AddModule("Proveedor", "/Core/Proveedor"),
                    MenuHelpers.AddModule("Imagen de Intro", "/Core/ImagenIntro"),
                    MenuHelpers.AddModule("Catálogo", "/Core/Catalogo"),
                    MenuHelpers.AddModule("Marcas", "/Core/Marcas/"),
                    MenuHelpers.AddModule("Menú Mobile", "/Core/MenuMobile/"),
                    MenuHelpers.AddModule("PreRegistro de Categoria", "/Core/PreRegistroCategorias"),
                    MenuHelpers.AddModule("Morosos", "/Core/Morosos"),
                    MenuHelpers.AddModule("Solicitudes de Tarjeta", "/Core/SolicitudDeTarjeta"),
                    MenuHelpers.AddModule("Movimientos Tarjeta (LOAN)", "/Core/MovimientoTarjeta")
            };
                //reportes.TreeChild = new List<SidebarMenu>()
                //{
                //    MenuHelpers.AddModule("Prestamos por App Estancias", "/Core/PrestamosApp/"),
                //};

                reportes.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Pagos Tarjeta", "/Reportes/PagoTarjetaReportes/"),
                    MenuHelpers.AddModule("Clientes", "/Reportes/ClientesReportes/"),
                    MenuHelpers.AddModule("Resumen Tarjeta", "/Reportes/ResumenTarjetaReportes/"),
                    MenuHelpers.AddModule("Cupones", "/Reportes/CuponesReportes/"),
                    //MenuHelpers.AddModule("Resumen de Deuda", "/Reportes/ResumenDeuda/")
                };

                sesion.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Cierra Sesión", "javascript:document.getElementById('logoutForm').submit()", "fa fa-sign-out"),
                };
                sistema.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Usuarios", "/Administracion/Usuarios", "fa fa-circle-o text-green"),
                    MenuHelpers.AddModule("Funciones", "/SecurityFunctions", "fa fa-circle-o text-green"),
                    MenuHelpers.AddModule("Rol", "/SecurityRoles", "fa fa-circle-o text-green"),
                };
                sidebars.Add(datos);
                sidebars.Add(gestion);
                sidebars.Add(reportes);
                //sidebars.Add(reportes);
                sidebars.Add(sistema);
                sidebars.Add(sesion);
            }
            else
            {
                datos.TreeChild = new List<SidebarMenu>();
                generales.TreeChild = new List<SidebarMenu>();
                especificos.TreeChild = new List<SidebarMenu>();

                #region DATOS GENERALES
                if (UserHasAnyRoute("/Core/TiposDocumentos/Index", "/TiposDocumentos/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Tipos de Documento", "/Core/TiposDocumentos/"));

                if (UserHasAnyRoute("/Core/DestinosFondos/Index", "/DestinosFondos/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Destinos Fondos", "/Core/DestinosFondos"));

                if (UserHasAnyRoute("/Core/Sucursales/Index", "/Sucursales/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Sucursales", "/Core/Sucursales"));

                if (UserHasAnyRoute("/Core/Monedas/Index", "/Monedas/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Monedas", "/Core/Monedas"));

                if (UserHasAnyRoute("/Core/Paises/Index", "/Paises/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Paises", "/Core/Paises"));

                if (UserHasAnyRoute("/Core/Provincias/Index", "/Provincias/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Provincias", "/Core/Provincias/"));

                if (UserHasAnyRoute("/Core/Localidad/Index", "/Localidad/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Localidad", "/Core/Localidad/"));

                if (UserHasAnyRoute("/Core/TiposClientes/Index", "/TiposClientes/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Tipos de Clientes", "/Core/TiposClientes/"));

                if (UserHasAnyRoute("/Core/Grupos/Index", "/Grupos/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Grupos", "/Core/Grupos/"));

                if (UserHasAnyRoute("/Core/Empresas/Index", "/Empresas/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Empresas", "/Core/Empresas/"));

                if (UserHasAnyRoute("/Core/LineasPrestamos/Index", "/LineasPrestamos/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Lineas de Prestamos", "/Core/LineasPrestamos/"));

                if (UserHasAnyRoute("/Core/Conceptos/Index", "/Conceptos/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Conceptos", "/Core/Conceptos/"));

                if (UserHasAnyRoute("/Core/CuentasCorrientes/Index", "/CuentasCorrientes/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Cuentas Corrientes", "/Core/CuentasCorrientes/"));

                if (UserHasAnyRoute("/Core/TipoMovimientoBilletera/Index", "/TipoMovimientoBilletera/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Tipos Movimientos Billetera", "/Core/TipoMovimientoBilletera/"));

                if (UserHasAnyRoute("/Core/MovimientoBilletera/Index", "/MovimientoBilletera/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Movimientos de Billetera", "/Core/MovimientoBilletera/"));

                if (UserHasAnyRoute("/Core/Billetera/Index", "/Billetera/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Billetera", "/Core/Billetera/"));

                if (UserHasAnyRoute("/Core/LeyendaTipoMovimiento/Index", "/LeyendaTipoMovimiento/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Leyendas", "/Core/LeyendaTipoMovimiento/"));

                if (UserHasAnyRoute("/Core/UsuarioCategorias/Index", "/UsuarioCategorias/Index"))
                    generales.TreeChild.Add(MenuHelpers.AddModule("Categoria de Usuarios", "/Core/UsuarioCategorias"));

                if (generales.TreeChild.Count != 0)
                    datos.TreeChild.Add(generales);
                #endregion

                #region DATOS ESPECIFICOS
                if (UserHasAnyRoute("/Core/Rubro/Index", "/Rubro/Index"))
                    especificos.TreeChild.Add(MenuHelpers.AddModule("Proveedor - Rubros", "/Core/Rubro/"));

                if (UserHasAnyRoute("/Core/Marcas/Index", "/Marcas/Index"))
                    especificos.TreeChild.Add(MenuHelpers.AddModule("Marcas", "/Core/Marcas/"));

                if (especificos.TreeChild.Count != 0)
                    datos.TreeChild.Add(especificos);

                if (datos.TreeChild.Count != 0)
                    sidebars.Add(datos);
                #endregion

                #region GESTION
                gestion.TreeChild = new List<SidebarMenu>();

                if (UserHasAnyRoute("/Core/Clientes/Index", "/Clientes/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Clientes", "/Core/Clientes"));

                if (UserHasAnyRoute("/Core/PagoTarjeta/Index", "/PagoTarjeta/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Pagos Tarjeta", "/Core/PagoTarjeta"));

                if (UserHasAnyRoute("/Core/PagoTarjetaHistorico/Index", "/PagoTarjetaHistorico/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Histórico de Pagos Tarjeta", "/Core/PagoTarjetaHistorico"));

                #region NOTIFICACIONES
                notificaciones.TreeChild = new List<SidebarMenu>();

                if (UserHasAnyRoute("/Core/Notificaciones/Index", "/Notificaciones/Index"))
                    notificaciones.TreeChild.Add(MenuHelpers.AddModule("Notificación", "/Core/Notificaciones/"));

                if (UserHasAnyRoute("/Core/NotificacionesPlantillas/Index", "/NotificacionesPlantillas/Index"))
                    notificaciones.TreeChild.Add(MenuHelpers.AddModule("Plantillas", "/Core/NotificacionesPlantillas/"));

                if (UserHasAnyRoute("/Core/ListaDistribucion/Index", "/ListaDistribucion/Index"))
                    notificaciones.TreeChild.Add(MenuHelpers.AddModule("Lista de Distribución", "/Core/ListaDistribucion/"));

                if (notificaciones.TreeChild.Count != 0)
                    gestion.TreeChild.Add(notificaciones);
                #endregion

                #region PUNTOS Y CUPONES
                puntosycupones.TreeChild = new List<SidebarMenu>();

                if (UserHasAnyRoute("/Core/Cupon/Validar", "/Core/Cupon/Index", "/Cupon/Validar", "/Cupon/Index"))
                    puntosycupones.TreeChild.Add(MenuHelpers.AddModule("Validar Cupón", "/Core/Cupon"));

                if (UserHasAnyRoute("/Core/Premios/Index", "/Core/Premio/Index", "/Premios/Index", "/Premio/Index"))
                    puntosycupones.TreeChild.Add(MenuHelpers.AddModule("Cupones", "/Core/Premios"));

                if (UserHasAnyRoute("/Core/PuntosObtenidosClientes/Index", "/PuntosObtenidosClientes/Index"))
                    puntosycupones.TreeChild.Add(MenuHelpers.AddModule("Puntos Clientes", "/Core/PuntosObtenidosClientes"));

                if (UserHasAnyRoute("/Core/Categorias/Index", "/Categorias/Index"))
                    puntosycupones.TreeChild.Add(MenuHelpers.AddModule("Categorias de Cupones", "/Core/Categorias"));

                if (UserHasAnyRoute("/Core/RelacionPuntos/Index", "/RelacionPuntos/Index"))
                    puntosycupones.TreeChild.Add(MenuHelpers.AddModule("Relación Puntos", "/Core/RelacionPuntos/"));

                if (puntosycupones.TreeChild.Count != 0)
                    gestion.TreeChild.Add(puntosycupones);
                #endregion

                if (UserHasAnyRoute("/Core/Vendedores/Index", "/Vendedores/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Vendedores", "/Core/Vendedores/"));

                if (UserHasAnyRoute("/Core/Novedades/Index", "/Novedades/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Novedades", "/Core/Novedades"));

                if (UserHasAnyRoute("/Core/Promociones/Index", "/Promociones/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Promociones", "/Core/Promociones"));

                if (UserHasAnyRoute("/Core/Banners/Index", "/Banners/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Banners", "/Core/Banners"));

                if (UserHasAnyRoute("/Core/Proveedor/Index", "/Proveedor/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Proveedor", "/Core/Proveedor"));

                if (UserHasAnyRoute("/Core/ImagenIntro/Index", "/ImagenIntro/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Imagen de Intro", "/Core/ImagenIntro"));

                if (UserHasAnyRoute("/Core/Catalogo/Index", "/Catalogo/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Catálogo", "/Core/Catalogo"));

                if (UserHasAnyRoute("/Core/MenuMobile/Index", "/MenuMobile/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Menú Mobile", "/Core/MenuMobile/"));

                if (UserHasAnyRoute("/Core/PreRegistroCategorias/Index", "/PreRegistroCategorias/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("PreRegistro de Categoria", "/Core/PreRegistroCategorias"));

                if (UserHasAnyRoute("/Core/Morosos/Index", "/Morosos/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Morosos", "/Core/Morosos"));

                if (UserHasAnyRoute("/Core/SolicitudDeTarjeta/Index", "/SolicitudDeTarjeta/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Solicitudes de Tarjeta", "/Core/SolicitudDeTarjeta"));

                if (UserHasAnyRoute("/Core/MovimientoTarjeta/Index", "/MovimientoTarjeta/Index"))
                    gestion.TreeChild.Add(MenuHelpers.AddModule("Movimientos Tarjeta (LOAN)", "/Core/MovimientoTarjeta"));

                if (gestion.TreeChild.Count != 0)
                    sidebars.Add(gestion);
                #endregion

                #region REPORTES
                reportes.TreeChild = new List<SidebarMenu>();

                if (UserHasAnyRoute("/Reportes/PagoTarjetaReportes/Index", "/PagoTarjetaReportes/Index"))
                    reportes.TreeChild.Add(MenuHelpers.AddModule("Pagos Tarjeta", "/Reportes/PagoTarjetaReportes/"));

                if (UserHasAnyRoute("/Reportes/ClientesReportes/Index", "/ClientesReportes/Index"))
                    reportes.TreeChild.Add(MenuHelpers.AddModule("Clientes", "/Reportes/ClientesReportes/"));

                if (UserHasAnyRoute("/Reportes/ResumenTarjetaReportes/Index", "/ResumenTarjetaReportes/Index"))
                    reportes.TreeChild.Add(MenuHelpers.AddModule("Resumen Tarjeta", "/Reportes/ResumenTarjetaReportes/"));

                if (UserHasAnyRoute("/Reportes/CuponesReportes/Index", "/CuponesReportes/Index"))
                    reportes.TreeChild.Add(MenuHelpers.AddModule("Cupones", "/Reportes/CuponesReportes/"));

                if (reportes.TreeChild.Count != 0)
                    sidebars.Add(reportes);
                #endregion

                #region SISTEMA
                sistema.TreeChild = new List<SidebarMenu>();

                if (UserHasAnyRoute("/Administracion/Usuarios/Index", "/Usuarios/Index"))
                    sistema.TreeChild.Add(MenuHelpers.AddModule("Usuarios", "/Administracion/Usuarios", "fa fa-users text-red", new Tuple<int, int, int>(0, 0, 0)));

                if (UserHasAnyRoute("/SecurityFunctions/Index", "/Securityfunctions/Index"))
                    sistema.TreeChild.Add(MenuHelpers.AddModule("Funciones", "/SecurityFunctions", "fa fa-key text-red", new Tuple<int, int, int>(0, 0, 0)));

                if (UserHasAnyRoute("/SecurityRoles/Index"))
                    sistema.TreeChild.Add(MenuHelpers.AddModule("Roles", "/SecurityRoles", "fa fa-key text-red", new Tuple<int, int, int>(0, 0, 0)));

                if (!sistema.TreeChild.IsNullOrEmpty())
                    sidebars.Add(sistema);
                #endregion

                #region SESION
                sesion.TreeChild = new List<SidebarMenu>()
                {
                    MenuHelpers.AddModule("Cierra Sesión", "javascript:document.getElementById('logoutForm').submit()", "fa fa-sign-out")
                };
                sidebars.Add(sesion);
                #endregion
            }
            return View("LayoutSidebar", sidebars);
        }
    }
}
