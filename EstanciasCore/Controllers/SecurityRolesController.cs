using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Commons.Models;
using Commons.Identity;
using DAL.Data;
using DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using EstanciasCore.Controllers;

namespace EstanciasCore.Controllers
{
    public class SecurityRolesController : EstanciasCoreController
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;

        public class RouteGroup
        {
            public string ControllerName { get; set; }
            public string AreaName { get; set; }
            public List<string> Routes { get; set; } = new List<string>();
        }

        public SecurityRolesController(EstanciasContext context, UserManager<Usuario> userManager, IActionDescriptorCollectionProvider actionDescriptorCollectionProvider = null) : base(context)
        {
            _userManager = userManager;
            _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
            breadcumb.Add(new Message() { DisplayName = "Seguridad" });
        }

        private List<RouteGroup> GetApplicationRoutesGrouped()
        {
            if (_actionDescriptorCollectionProvider == null) return new List<RouteGroup>();

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
                    path = (!string.IsNullOrEmpty(area) ? $"/{area}" : "") + $"/{controller}/{action}";
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

        public IActionResult Index()
        {
            breadcumb.Add(new Message() { DisplayName = "Roles" });
            ViewBag.Breadcrumb = breadcumb;
            var roles = _context.Set<CommonsRole>().ToList();

            // Get all functions assigned to roles
            var roleFunctions = _context.Set<CommonsRoleFunction>()
                                        .Include(rf => rf.Role)
                                        .Include(rf => rf.Function)
                                        .Where(rf => rf.DeletedDate == null)
                                        .ToList();

            var roleFunctionsDict = roles.ToDictionary(
                r => r.Id,
                r => roleFunctions.Where(rf => rf.Role != null && rf.Role.Id == r.Id && rf.Function != null)
                                  .Select(rf => rf.Function.Name)
                                  .ToList()
            );

            ViewBag.RoleFunctions = roleFunctionsDict;
            return View(roles);
        }

        public IActionResult _Create()
        {
            var functions = _context.Set<CommonsFunction>().Where(f => f.DeletedDate == null).OrderBy(f => f.Name).ToList();
            ViewBag.Functions = functions;
            ViewBag.RouteGroups = GetApplicationRoutesGrouped();
            return PartialView();
        }

        [HttpPost]
        public async Task<IActionResult> _Create(
            string name, 
            string showName, 
            string description, 
            List<string> selectedFunctionIds, 
            string category = null, 
            string selectedController = null, 
            List<string> selectedRoutes = null, 
            string id = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(showName))
                {
                    if (!string.IsNullOrWhiteSpace(selectedController))
                    {
                        showName = selectedController;
                    }
                    else
                    {
                        AddPageAlerts(PageAlertType.Error, "El nombre a mostrar es requerido.");
                        return RedirectToAction("Index");
                    }
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "Default WorkSpace: " + showName.Trim();
                }

                var cat = !string.IsNullOrWhiteSpace(category) ? category.Trim() : "Gestión";
                var cleanDesc = description?.Trim() ?? "";
                if (cleanDesc.StartsWith("[") && cleanDesc.Contains("]"))
                {
                    var closeIdx = cleanDesc.IndexOf("]");
                    cleanDesc = cleanDesc.Substring(closeIdx + 1).Trim();
                }
                var finalDesc = $"[{cat}]" + (!string.IsNullOrWhiteSpace(cleanDesc) ? " " + cleanDesc : "");

                var role = (CommonsRole)Activator.CreateInstance(typeof(CommonsRole), true);
                role.Id = Guid.NewGuid().ToString();
                role.Name = name.Trim();
                role.ShowName = showName.Trim();
                role.Description = finalDesc;
                role.NormalizedName = role.Name.ToUpper();
                role.CreatedDate = DateTime.Now;
                role.Enabled = true;
                role.Show = true;

                await _context.Set<CommonsRole>().AddAsync(role);

                if (selectedFunctionIds == null)
                {
                    selectedFunctionIds = new List<string>();
                }

                // Unificación: Si se seleccionó controlador o rutas, crear o actualizar automáticamente la función asociada
                if (!string.IsNullOrEmpty(selectedController) || (selectedRoutes != null && selectedRoutes.Any()))
                {
                    var funcName = role.ShowName;
                    var func = await _context.Set<CommonsFunction>()
                                             .FirstOrDefaultAsync(f => f.Name == funcName && f.DeletedDate == null);
                    if (func == null)
                    {
                        var routeObjs = (selectedRoutes ?? new List<string>()).Select(r => new CommonsRoute(r)).ToList();
                        func = new CommonsFunction
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = funcName,
                            Description = $"[{cat}] Rutas del controlador {selectedController}",
                            Display = true,
                            Show = true,
                            CreationDate = DateTime.Now,
                            LastEditTime = DateTime.Now,
                            RoutesJson = JsonConvert.SerializeObject(routeObjs)
                        };
                        await _context.Set<CommonsFunction>().AddAsync(func);
                    }
                    else if (selectedRoutes != null && selectedRoutes.Any())
                    {
                        var routeObjs = selectedRoutes.Select(r => new CommonsRoute(r)).ToList();
                        func.RoutesJson = JsonConvert.SerializeObject(routeObjs);
                        func.LastEditTime = DateTime.Now;
                        _context.Set<CommonsFunction>().Update(func);
                    }

                    if (!selectedFunctionIds.Contains(func.Id))
                    {
                        selectedFunctionIds.Add(func.Id);
                    }
                }

                // Asignar funciones seleccionadas
                if (selectedFunctionIds != null && selectedFunctionIds.Any())
                {
                    foreach (var funcId in selectedFunctionIds.Distinct())
                    {
                        var func = await _context.Set<CommonsFunction>().FindAsync(funcId);
                        if (func != null)
                        {
                            await _context.Set<CommonsRoleFunction>().AddAsync(new CommonsRoleFunction
                            {
                                Id = Guid.NewGuid().ToString(),
                                Role = role,
                                Function = func,
                                CreationDate = DateTime.Now
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                AddPageAlerts(PageAlertType.Success, "Se cargó correctamente el Rol " + role.ShowName + " con sus funciones correspondientes.");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                AddPageAlerts(PageAlertType.Error, "Hubo un error al cargar el Rol. " + ex.Message);
                return RedirectToAction("Index");
            }
        }

        public IActionResult _Update(string id)
        {
            var role = _context.Set<CommonsRole>().Find(id);
            if (role == null) return NotFound();

            var functions = _context.Set<CommonsFunction>().Where(f => f.DeletedDate == null).OrderBy(f => f.Name).ToList();
            var assignedFuncIds = _context.Set<CommonsRoleFunction>()
                                          .Include(rf => rf.Role)
                                          .Include(rf => rf.Function)
                                          .Where(rf => rf.Role.Id == id && rf.DeletedDate == null)
                                          .Select(rf => rf.Function.Id)
                                          .ToList();

            ViewBag.Functions = functions;
            ViewBag.AssignedFuncIds = assignedFuncIds;
            ViewBag.RouteGroups = GetApplicationRoutesGrouped();
            return PartialView(role);
        }

        [HttpPost]
        public async Task<IActionResult> _Update(
            string id, 
            string name, 
            string showName, 
            string description, 
            bool enabled, 
            bool show, 
            List<string> selectedFunctionIds, 
            string category = null, 
            string selectedController = null, 
            List<string> selectedRoutes = null)
        {
            try
            {
                var existingRole = await _context.Set<CommonsRole>().FindAsync(id);
                if (existingRole == null) return NotFound();

                var cat = !string.IsNullOrWhiteSpace(category) ? category.Trim() : "Gestión";
                var cleanDesc = description?.Trim() ?? "";
                if (cleanDesc.StartsWith("[") && cleanDesc.Contains("]"))
                {
                    var closeIdx = cleanDesc.IndexOf("]");
                    cleanDesc = cleanDesc.Substring(closeIdx + 1).Trim();
                }
                var finalDesc = $"[{cat}]" + (!string.IsNullOrWhiteSpace(cleanDesc) ? " " + cleanDesc : "");
                
                existingRole.Name = name?.Trim();
                existingRole.NormalizedName = name?.ToUpper()?.Trim();
                existingRole.ShowName = showName?.Trim();
                existingRole.Description = finalDesc;
                existingRole.Enabled = enabled;
                existingRole.Show = show;
                
                _context.Set<CommonsRole>().Update(existingRole);

                if (selectedFunctionIds == null)
                {
                    selectedFunctionIds = new List<string>();
                }

                // Unificación: Si se seleccionó controlador o rutas, crear o actualizar automáticamente la función asociada
                if (!string.IsNullOrEmpty(selectedController) || (selectedRoutes != null && selectedRoutes.Any()))
                {
                    var funcName = existingRole.ShowName;
                    var func = await _context.Set<CommonsFunction>()
                                             .FirstOrDefaultAsync(f => f.Name == funcName && f.DeletedDate == null);
                    if (func == null)
                    {
                        var routeObjs = (selectedRoutes ?? new List<string>()).Select(r => new CommonsRoute(r)).ToList();
                        func = new CommonsFunction
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = funcName,
                            Description = $"[{cat}] Rutas del controlador {selectedController}",
                            Display = true,
                            Show = true,
                            CreationDate = DateTime.Now,
                            LastEditTime = DateTime.Now,
                            RoutesJson = JsonConvert.SerializeObject(routeObjs)
                        };
                        await _context.Set<CommonsFunction>().AddAsync(func);
                    }
                    else if (selectedRoutes != null && selectedRoutes.Any())
                    {
                        var routeObjs = selectedRoutes.Select(r => new CommonsRoute(r)).ToList();
                        func.RoutesJson = JsonConvert.SerializeObject(routeObjs);
                        func.LastEditTime = DateTime.Now;
                        _context.Set<CommonsFunction>().Update(func);
                    }

                    if (!selectedFunctionIds.Contains(func.Id))
                    {
                        selectedFunctionIds.Add(func.Id);
                    }
                }

                // Actualizar funciones asignadas
                var currentFuncs = _context.Set<CommonsRoleFunction>()
                                            .Include(rf => rf.Role)
                                            .Include(rf => rf.Function)
                                            .Where(rf => rf.Role.Id == id && rf.DeletedDate == null)
                                            .ToList();

                var toRemove = currentFuncs.Where(rf => !selectedFunctionIds.Contains(rf.Function.Id)).ToList();
                _context.Set<CommonsRoleFunction>().RemoveRange(toRemove);

                foreach (var funcId in selectedFunctionIds.Distinct())
                {
                    if (!currentFuncs.Any(rf => rf.Function.Id == funcId))
                    {
                        var function = await _context.Set<CommonsFunction>().FindAsync(funcId);
                        if (function != null)
                        {
                            await _context.Set<CommonsRoleFunction>().AddAsync(new CommonsRoleFunction
                            {
                                Id = Guid.NewGuid().ToString(),
                                Role = existingRole,
                                Function = function,
                                CreationDate = DateTime.Now
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                AddPageAlerts(PageAlertType.Success, "Se modificó correctamente el Rol y sus funciones asociadas.");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                AddPageAlerts(PageAlertType.Error, "Hubo un error al modificar el Rol. " + ex.Message);
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var role = await _context.Set<CommonsRole>().FindAsync(id);
                if (role != null)
                {
                    // Remove role functions
                    var roleFuncs = _context.Set<CommonsRoleFunction>()
                                            .Include(rf => rf.Role)
                                            .Where(rf => rf.Role.Id == id)
                                            .ToList();

                    _context.Set<CommonsRoleFunction>().RemoveRange(roleFuncs);

                    // Remove user roles
                    var userRoles = _context.UserRoles.Where(ur => ur.RoleId == id).ToList();
                    _context.UserRoles.RemoveRange(userRoles);

                    _context.Set<CommonsRole>().Remove(role);
                    await _context.SaveChangesAsync();
                    AddPageAlerts(PageAlertType.Success, "Se eliminó correctamente el Rol " + role.ShowName + ".");
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                AddPageAlerts(PageAlertType.Error, "Hubo un error al eliminar el Rol. " + ex.Message);
                return RedirectToAction("Index");
            }
        }

        [HttpGet("/SecurityRoles/_Assign/{id?}")]
        public async Task<IActionResult> _Assign(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("El ID de usuario es requerido.");
            }
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("Usuario no encontrado.");

            var allRoles = _context.Set<CommonsRole>().Where(r => r.Enabled).ToList();
            var assignedRoleIds = _context.UserRoles.Where(ur => ur.UserId == id).Select(ur => ur.RoleId).ToList();

            ViewBag.User = user;
            ViewBag.AssignedRoleIds = assignedRoleIds;
            ViewBag.Roles = allRoles;

            return PartialView();
        }

        [HttpPost]
        public async Task<IActionResult> _Assign(string userId, List<string> selectedRoleIds)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Usuario no encontrado." });
                    }
                    return NotFound();
                }

                // Deduplicar role IDs seleccionados
                var distinctSelectedRoleIds = (selectedRoleIds ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                var currentRoles = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
                var currentRoleIds = new HashSet<string>(currentRoles.Select(ur => ur.RoleId));

                // Eliminar roles no seleccionados
                var toRemove = currentRoles.Where(ur => !distinctSelectedRoleIds.Contains(ur.RoleId)).ToList();
                if (toRemove.Count > 0)
                {
                    _context.UserRoles.RemoveRange(toRemove);
                }

                // Agregar únicamente los roles nuevos que no estén ya asociados
                var toAdd = distinctSelectedRoleIds
                    .Where(roleId => !currentRoleIds.Contains(roleId))
                    .Select(roleId => new IdentityUserRole<string> { UserId = userId, RoleId = roleId })
                    .ToList();

                if (toAdd.Count > 0)
                {
                    _context.UserRoles.AddRange(toAdd);
                }

                await _context.SaveChangesAsync();

                // Obtener nombres de roles actualizados para refrescar el mapa en el cliente
                var updatedUserRoles = await _context.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync();
                var allRolesDict = await _context.Set<CommonsRole>().Where(r => r.Enabled).ToDictionaryAsync(r => r.Id, r => !string.IsNullOrEmpty(r.ShowName) ? r.ShowName : r.Name);
                var assignedNames = updatedUserRoles
                    .Where(id => allRolesDict.ContainsKey(id))
                    .Select(id => allRolesDict[id])
                    .ToList();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, userId = userId, roleNames = assignedNames, message = "Se actualizaron correctamente los roles para el usuario " + user.UserName + "." });
                }

                AddPageAlerts(PageAlertType.Success, "Se actualizaron correctamente los roles para el usuario " + user.UserName + ".");
                return Redirect("/Administracion/Usuarios");
            }
            catch (DbUpdateException dbEx) when (dbEx.GetBaseException() is System.Data.SqlClient.SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
            {
                // Concurrencia o clave duplicada: los roles ya existen en la base de datos
                var user = await _context.Users.FindAsync(userId);
                var updatedUserRoles = await _context.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync();
                var allRolesDict = await _context.Set<CommonsRole>().AsNoTracking().Where(r => r.Enabled).ToDictionaryAsync(r => r.Id, r => !string.IsNullOrEmpty(r.ShowName) ? r.ShowName : r.Name);
                var assignedNames = updatedUserRoles
                    .Where(id => allRolesDict.ContainsKey(id))
                    .Select(id => allRolesDict[id])
                    .ToList();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, userId = userId, roleNames = assignedNames, message = "Se actualizaron correctamente los roles para el usuario " + (user?.UserName ?? "") + "." });
                }

                AddPageAlerts(PageAlertType.Success, "Se actualizaron correctamente los roles para el usuario " + (user?.UserName ?? "") + ".");
                return Redirect("/Administracion/Usuarios");
            }
            catch (Exception ex)
            {
                var errorDetail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Hubo un error al asignar los roles: " + errorDetail });
                }
                AddPageAlerts(PageAlertType.Error, "Hubo un error al asignar los roles. " + errorDetail);
                return Redirect("/Administracion/Usuarios");
            }
        }

        public async Task<IActionResult> _AssignFunctions(string id)
        {
            var role = await _context.Set<CommonsRole>().FindAsync(id);
            if (role == null) return NotFound();

            var allFunctions = _context.Set<CommonsFunction>().Where(f => f.DeletedDate == null).ToList();
            var assignedFuncIds = _context.Set<CommonsRoleFunction>()
                                           .Include(rf => rf.Role)
                                           .Include(rf => rf.Function)
                                           .Where(rf => rf.Role.Id == id && rf.DeletedDate == null)
                                           .Select(rf => rf.Function.Id)
                                           .ToList();

            ViewBag.Role = role;
            ViewBag.AssignedFuncIds = assignedFuncIds;
            ViewBag.Functions = allFunctions;

            return PartialView();
        }

        [HttpPost]
        public async Task<IActionResult> _AssignFunctions(string roleId, List<string> selectedFunctionIds)
        {
            try
            {
                var role = await _context.Set<CommonsRole>().FindAsync(roleId);
                if (role == null) return NotFound();

                var currentFuncs = _context.Set<CommonsRoleFunction>()
                                            .Include(rf => rf.Role)
                                            .Include(rf => rf.Function)
                                            .Where(rf => rf.Role.Id == roleId && rf.DeletedDate == null)
                                            .ToList();

                // Remove functions not selected
                var toRemove = currentFuncs.Where(rf => selectedFunctionIds == null || !selectedFunctionIds.Contains(rf.Function.Id)).ToList();
                _context.Set<CommonsRoleFunction>().RemoveRange(toRemove);

                // Add newly selected functions
                if (selectedFunctionIds != null)
                {
                    foreach (var funcId in selectedFunctionIds)
                    {
                        if (!currentFuncs.Any(rf => rf.Function.Id == funcId))
                        {
                            var function = await _context.Set<CommonsFunction>().FindAsync(funcId);
                            _context.Set<CommonsRoleFunction>().Add(new CommonsRoleFunction
                            {
                                Id = Guid.NewGuid().ToString(),
                                Role = role,
                                Function = function,
                                CreationDate = DateTime.Now
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                AddPageAlerts(PageAlertType.Success, "Se asignaron correctamente las funciones al rol " + role.ShowName + ".");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                AddPageAlerts(PageAlertType.Error, "Hubo un error al asignar las funciones. " + ex.Message);
                return RedirectToAction("Index");
            }
        }
    }
}
