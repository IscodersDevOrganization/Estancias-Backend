using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Commons.Models;
using Commons.Identity;
using DAL.Data;
using DAL.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using EstanciasCore.Controllers;

namespace EstanciasCore.Controllers
{
    public class SecurityFunctionsController : EstanciasCoreController
    {
        private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;

        public class RouteGroup
        {
            public string ControllerName { get; set; }
            public string AreaName { get; set; }
            public List<string> Routes { get; set; } = new List<string>();
        }

        public SecurityFunctionsController(
            EstanciasContext context, 
            IActionDescriptorCollectionProvider actionDescriptorCollectionProvider) : base(context)
        {
            _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
            breadcumb.Add(new Message() { DisplayName = "Seguridad" });
        }

        public IActionResult Index()
        {
            breadcumb.Add(new Message() { DisplayName = "Funciones" });
            ViewBag.Breadcrumb = breadcumb;
            var functions = _context.Set<CommonsFunction>().Where(f => f.DeletedDate == null).ToList();
            return View(functions);
        }


        private List<RouteGroup> GetApplicationRoutesGrouped()
        {
            var groups = new Dictionary<string, RouteGroup>();
            foreach (var ad in _actionDescriptorCollectionProvider.ActionDescriptors.Items)
            {
                var controller = ad.RouteValues["controller"];
                var action = ad.RouteValues["action"];
                var area = ad.RouteValues.ContainsKey("area") ? ad.RouteValues["area"] : null;

                var path = "";
                if (ad.AttributeRouteInfo != null && !string.IsNullOrEmpty(ad.AttributeRouteInfo.Template))
                {
                    path = ad.AttributeRouteInfo.Template;
                    if (!path.StartsWith("/")) path = "/" + path;
                }
                else
                {
                    path = "";
                    if (!string.IsNullOrEmpty(area)) path += $"/{area}";
                    path += $"/{controller}/{action}";
                }

                var key = $"{area}_{controller}";
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new RouteGroup
                    {
                        ControllerName = controller,
                        AreaName = area
                    };
                }
                groups[key].Routes.Add(path);
            }

            foreach (var g in groups.Values)
            {
                g.Routes = g.Routes.Distinct().OrderBy(r => r).ToList();
            }

            return groups.Values
                .OrderBy(g => g.AreaName)
                .ThenBy(g => g.ControllerName)
                .ToList();
        }

        public IActionResult _Create()
        {
            var groupedRoutes = GetApplicationRoutesGrouped();
            var allRoutesFlat = groupedRoutes.SelectMany(g => g.Routes).Distinct().ToList();

            ViewBag.RouteGroups = groupedRoutes;
            ViewBag.AllRoutesFlat = allRoutesFlat;
            return PartialView();
        }

        [HttpPost]
        public async Task<IActionResult> _Create(CommonsFunction function, List<string> selectedRoutes)
        {
            try
            {
                ModelState.Remove("Id");
                if (ModelState.IsValid)
                {
                    function.Id = Guid.NewGuid().ToString();
                    function.CreationDate = DateTime.Now;
                    function.LastEditTime = DateTime.Now;
                    function.Display = true;
                    function.Show = true;
                    var routeObjs = (selectedRoutes ?? new List<string>()).Select(r => new CommonsRoute(r)).ToList();
                    function.RoutesJson = JsonConvert.SerializeObject(routeObjs);

                    await _context.Set<CommonsFunction>().AddAsync(function);
                    await _context.SaveChangesAsync();
                    AddPageAlerts(PageAlertType.Success, "Se cargó correctamente la Función " + function.Name + ".");
                    return RedirectToAction("Index");
                }

                var groupedRoutes = GetApplicationRoutesGrouped();
                var allRoutesFlat = groupedRoutes.SelectMany(g => g.Routes).Distinct().ToList();

                ViewBag.RouteGroups = groupedRoutes;
                ViewBag.AllRoutesFlat = allRoutesFlat;
                ViewBag.SelectedRoutes = selectedRoutes ?? new List<string>();
                return PartialView(function);
            }
            catch (Exception ex)
            {
                AddPageAlerts(PageAlertType.Error, "Hubo un error al cargar la Función. " + ex.Message);
                return RedirectToAction("Index");
            }
        }

        public static List<string> ParseRoutesJson(string routesJson)
        {
            if (string.IsNullOrEmpty(routesJson)) return new List<string>();
            routesJson = routesJson.Trim();
            if (routesJson.StartsWith("[") && routesJson.EndsWith("]"))
            {
                try
                {
                    var commonsRoutes = JsonConvert.DeserializeObject<List<CommonsRoute>>(routesJson);
                    if (commonsRoutes != null && commonsRoutes.Any(r => !string.IsNullOrEmpty(r?.Url)))
                    {
                        return commonsRoutes.Select(r => r.Url).Where(u => !string.IsNullOrEmpty(u)).ToList();
                    }
                    return JsonConvert.DeserializeObject<List<string>>(routesJson);
                }
                catch
                {
                    // Fallback to split
                }
            }

            return routesJson.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(r => r.Trim())
                             .Where(r => !string.IsNullOrEmpty(r))
                             .ToList();
        }

        public IActionResult _Update(string id)
        {
            var function = _context.Set<CommonsFunction>().Find(id);
            if (function == null) return NotFound();

            var routes = ParseRoutesJson(function.RoutesJson);

            var groupedRoutes = GetApplicationRoutesGrouped();
            var allRoutesFlat = groupedRoutes.SelectMany(g => g.Routes).Distinct().ToList();

            ViewBag.RouteGroups = groupedRoutes;
            ViewBag.AllRoutesFlat = allRoutesFlat;
            ViewBag.SelectedRoutes = routes;
            return PartialView(function);
        }

        [HttpPost]
        public async Task<IActionResult> _Update(CommonsFunction function, List<string> selectedRoutes)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var existingFunction = await _context.Set<CommonsFunction>().FindAsync(function.Id);
                    if (existingFunction == null) return NotFound();

                    existingFunction.Name = function.Name;
                    existingFunction.Description = function.Description;
                    existingFunction.Display = function.Display;
                    existingFunction.Show = function.Show;
                    existingFunction.LastEditTime = DateTime.Now;
                    var routeObjs = (selectedRoutes ?? new List<string>()).Select(r => new CommonsRoute(r)).ToList();
                    existingFunction.RoutesJson = JsonConvert.SerializeObject(routeObjs);

                    _context.Set<CommonsFunction>().Update(existingFunction);
                    await _context.SaveChangesAsync();
                    AddPageAlerts(PageAlertType.Success, "Se modificó correctamente la Función.");
                    return RedirectToAction("Index");
                }

                var groupedRoutes = GetApplicationRoutesGrouped();
                var allRoutesFlat = groupedRoutes.SelectMany(g => g.Routes).Distinct().ToList();

                ViewBag.RouteGroups = groupedRoutes;
                ViewBag.AllRoutesFlat = allRoutesFlat;
                ViewBag.SelectedRoutes = selectedRoutes ?? new List<string>();
                return PartialView(function);
            }
            catch (Exception ex)
            {
                AddPageAlerts(PageAlertType.Error, "Hubo un error al modificar la Función. " + ex.Message);
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var function = await _context.Set<CommonsFunction>().FindAsync(id);
                if (function != null)
                {
                    // Soft delete function
                    function.DeletedDate = DateTime.Now;
                    
                    // Also delete role-function associations
                    var roleFuncs = _context.Set<CommonsRoleFunction>()
                                            .Include(rf => rf.Function)
                                            .Where(rf => rf.Function.Id == id)
                                            .ToList();
                    _context.Set<CommonsRoleFunction>().RemoveRange(roleFuncs);

                    _context.Set<CommonsFunction>().Update(function);
                    await _context.SaveChangesAsync();
                    AddPageAlerts(PageAlertType.Success, "Se eliminó correctamente la Función " + function.Name + ".");
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                AddPageAlerts(PageAlertType.Error, "Hubo un error al eliminar la Función. " + ex.Message);
                return RedirectToAction("Index");
            }
        }
    }
}
