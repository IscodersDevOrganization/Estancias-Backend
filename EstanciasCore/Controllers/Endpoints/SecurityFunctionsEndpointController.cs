using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Commons.Identity;
using Commons.Models;
using DAL.Data;
using DAL.DTOs;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EstanciasCore.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/security-functions")]
    [ApiController]
    public class SecurityFunctionsEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;
        private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;

        public SecurityFunctionsEndpointController(
            EstanciasContext context,
            IActionDescriptorCollectionProvider actionDescriptorCollectionProvider)
        {
            _context = context;
            _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
        }

        // Equivalente al Index() del controller MVC.
        // GET: endpoint/security-functions
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var functions = await _context.Set<CommonsFunction>()
                .Where(f => f.DeletedDate == null)
                .ToListAsync();

            var data = functions
                .Select(MapFunction)
                .ToList();

            return Ok(new
            {
                ok = true,
                data = data
            });
        }

        // Equivalente al GET _Create() del controller MVC.
        // Devuelve las rutas necesarias para armar el formulario de alta.
        // GET: endpoint/security-functions/routes
        [HttpGet("routes")]
        public IActionResult GetRoutes()
        {
            var groupedRoutes = GetApplicationRoutesGrouped();
            var allRoutesFlat = groupedRoutes
                .SelectMany(g => g.Routes)
                .Distinct()
                .ToList();

            return Ok(new
            {
                ok = true,
                routeGroups = groupedRoutes,
                allRoutesFlat = allRoutesFlat
            });
        }

        // Equivalente al GET _Update(string id) del controller MVC.
        // GET: endpoint/security-functions/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var function = await _context.Set<CommonsFunction>().FindAsync(id);

            if (function == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la Función."
                });
            }

            var routes = ParseRoutesJson(function.RoutesJson);
            var groupedRoutes = GetApplicationRoutesGrouped();
            var allRoutesFlat = groupedRoutes
                .SelectMany(g => g.Routes)
                .Distinct()
                .ToList();

            return Ok(new
            {
                ok = true,
                data = MapFunction(function),
                routeGroups = groupedRoutes,
                allRoutesFlat = allRoutesFlat,
                selectedRoutes = routes
            });
        }

        // Equivalente al POST _Create() del controller MVC.
        // POST: endpoint/security-functions
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SecurityFunctionCreateDTO request)
        {
            try
            {
                var function = new CommonsFunction
                {
                    Name = request != null ? request.Name : null,
                    Description = request != null ? request.Description : null
                };

                TryValidateModel(function);
                ModelState.Remove("Id");

                if (!ModelState.IsValid)
                {
                    var groupedRoutes = GetApplicationRoutesGrouped();
                    var allRoutesFlat = groupedRoutes
                        .SelectMany(g => g.Routes)
                        .Distinct()
                        .ToList();

                    return BadRequest(new
                    {
                        ok = false,
                        message = "Los datos de la Función no son válidos.",
                        errors = ModelState,
                        routeGroups = groupedRoutes,
                        allRoutesFlat = allRoutesFlat,
                        selectedRoutes = request != null && request.SelectedRoutes != null
                            ? request.SelectedRoutes
                            : new List<string>()
                    });
                }

                function.Id = Guid.NewGuid().ToString();
                function.CreationDate = DateTime.Now;
                function.LastEditTime = DateTime.Now;
                function.Display = true;
                function.Show = true;
                var routeObjs = (request != null && request.SelectedRoutes != null ? request.SelectedRoutes : new List<string>()).Select(r => new CommonsRoute(r)).ToList();
                function.RoutesJson = JsonConvert.SerializeObject(routeObjs);

                await _context.Set<CommonsFunction>().AddAsync(function);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    ok = true,
                    message = "Se cargó correctamente la Función " + function.Name + ".",
                    data = MapFunction(function)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    message = "Hubo un error al cargar la Función. " + ex.Message
                });
            }
        }

        // Equivalente al POST _Update() del controller MVC.
        // PUT: endpoint/security-functions/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] SecurityFunctionUpdateDTO request)
        {
            try
            {
                var functionToValidate = new CommonsFunction
                {
                    Id = id,
                    Name = request != null ? request.Name : null,
                    Description = request != null ? request.Description : null,
                    Display = request != null && request.Display,
                    Show = request != null && request.Show
                };

                TryValidateModel(functionToValidate);

                if (!ModelState.IsValid)
                {
                    var groupedRoutes = GetApplicationRoutesGrouped();
                    var allRoutesFlat = groupedRoutes
                        .SelectMany(g => g.Routes)
                        .Distinct()
                        .ToList();

                    return BadRequest(new
                    {
                        ok = false,
                        message = "Los datos de la Función no son válidos.",
                        errors = ModelState,
                        routeGroups = groupedRoutes,
                        allRoutesFlat = allRoutesFlat,
                        selectedRoutes = request != null && request.SelectedRoutes != null
                            ? request.SelectedRoutes
                            : new List<string>()
                    });
                }

                var existingFunction = await _context.Set<CommonsFunction>().FindAsync(id);

                if (existingFunction == null)
                {
                    return NotFound(new
                    {
                        ok = false,
                        message = "No se encontró la Función."
                    });
                }

                existingFunction.Name = request.Name;
                existingFunction.Description = request.Description;
                existingFunction.Display = request.Display;
                existingFunction.Show = request.Show;
                existingFunction.LastEditTime = DateTime.Now;
                var routeObjs = (request.SelectedRoutes ?? new List<string>()).Select(r => new CommonsRoute(r)).ToList();
                existingFunction.RoutesJson = JsonConvert.SerializeObject(routeObjs);

                _context.Set<CommonsFunction>().Update(existingFunction);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    ok = true,
                    message = "Se modificó correctamente la Función.",
                    data = MapFunction(existingFunction)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    message = "Hubo un error al modificar la Función. " + ex.Message
                });
            }
        }

        // Equivalente al Delete(string id) del controller MVC.
        // Mantiene el soft delete y elimina las asociaciones rol-función.
        // DELETE: endpoint/security-functions/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var function = await _context.Set<CommonsFunction>().FindAsync(id);

                if (function != null)
                {
                    function.DeletedDate = DateTime.Now;

                    var roleFuncs = _context.Set<CommonsRoleFunction>()
                        .Include(rf => rf.Function)
                        .Where(rf => rf.Function.Id == id)
                        .ToList();

                    _context.Set<CommonsRoleFunction>().RemoveRange(roleFuncs);
                    _context.Set<CommonsFunction>().Update(function);

                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        ok = true,
                        message = "Se eliminó correctamente la Función " + function.Name + "."
                    });
                }

                return Ok(new
                {
                    ok = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    message = "Hubo un error al eliminar la Función. " + ex.Message
                });
            }
        }

        private List<SecurityFunctionRouteGroupDTO> GetApplicationRoutesGrouped()
        {
            var groups = new Dictionary<string, SecurityFunctionRouteGroupDTO>();

            foreach (var ad in _actionDescriptorCollectionProvider.ActionDescriptors.Items)
            {
                var controller = ad.RouteValues["controller"];
                var action = ad.RouteValues["action"];
                var area = ad.RouteValues.ContainsKey("area")
                    ? ad.RouteValues["area"]
                    : null;

                var path = "";

                if (ad.AttributeRouteInfo != null &&
                    !string.IsNullOrEmpty(ad.AttributeRouteInfo.Template))
                {
                    path = ad.AttributeRouteInfo.Template;

                    if (!path.StartsWith("/"))
                    {
                        path = "/" + path;
                    }
                }
                else
                {
                    path = "";

                    if (!string.IsNullOrEmpty(area))
                    {
                        path += "/" + area;
                    }

                    path += "/" + controller + "/" + action;
                }

                var key = area + "_" + controller;

                if (!groups.ContainsKey(key))
                {
                    groups[key] = new SecurityFunctionRouteGroupDTO
                    {
                        ControllerName = controller,
                        AreaName = area
                    };
                }

                groups[key].Routes.Add(path);
            }

            foreach (var group in groups.Values)
            {
                group.Routes = group.Routes
                    .Distinct()
                    .OrderBy(route => route)
                    .ToList();
            }

            return groups.Values
                .OrderBy(group => group.AreaName)
                .ThenBy(group => group.ControllerName)
                .ToList();
        }

        public static List<string> ParseRoutesJson(string routesJson)
        {
            if (string.IsNullOrEmpty(routesJson))
            {
                return new List<string>();
            }

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
                }
                catch
                {
                    // Fallback to string array
                }

                try
                {
                    return JsonConvert.DeserializeObject<List<string>>(routesJson);
                }
                catch
                {
                    // Mantiene el fallback original al split.
                }
            }

            return routesJson
                .Split(
                    new[] { ',', ';', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(route => route.Trim())
                .Where(route => !string.IsNullOrEmpty(route))
                .ToList();
        }

        private static SecurityFunctionDTO MapFunction(CommonsFunction function)
        {
            return new SecurityFunctionDTO
            {
                Id = function.Id,
                Name = function.Name,
                Description = function.Description,
                Display = function.Display,
                Show = function.Show,
                CreationDate = function.CreationDate,
                LastEditTime = function.LastEditTime,
                DeletedDate = function.DeletedDate,
                RoutesJson = function.RoutesJson,
                SelectedRoutes = ParseRoutesJson(function.RoutesJson)
            };
        }
    }
}
