using EstanciasCore.Areas.Administracion.ViewModels;
using Commons.Identity.Services;
using DAL.Data;
using DAL.DTOs;
using DAL.DTOs.Servicios;
using DAL.Models;
using EstanciasCore.API.Filters;
using EstanciasCore.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Areas.Administracion.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [ApiController]
    [Route("endpoint/usuarios")]
    public class UsuariosEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;
        private readonly UserService<Usuario> _userService;
        private readonly UserManager<Usuario> _userManager;

        public UsuariosEndpointController(EstanciasContext context, UserService<Usuario> userService, UserManager<Usuario> userManager)
        {
            _context = context;
            _userService = userService;
            _userManager = userManager;
        }

        [HttpGet("listar")]
        public IActionResult Listar(string buscar = "", int pagina = 1, int cantidad = 10,
            string filtroIncobrable = null, string filtroTipoUsuario = null, string filtroRoles = null)
        {
            if (pagina <= 0) pagina = 1;
            if (cantidad <= 0) cantidad = 10;

            var baseQuery = _context.Users.Where(x => x.UserName != "admin@admin.com");

            if (!string.IsNullOrEmpty(filtroIncobrable))
            {
                bool isIncobrable = filtroIncobrable == "1";
                baseQuery = baseQuery.Where(x => x.Incobrable == isIncobrable);
            }

            if (!string.IsNullOrEmpty(filtroTipoUsuario))
            {
                if (filtroTipoUsuario == "comun")
                    baseQuery = baseQuery.Where(x => x.UsuarioSucursal == false);
                else if (filtroTipoUsuario == "sucursal")
                    baseQuery = baseQuery.Where(x => x.UsuarioSucursal == true);
            }

            if (!string.IsNullOrEmpty(filtroRoles))
            {
                if (filtroRoles == "__con_roles__")
                {
                    var userIdsWithRoles = _context.UserRoles.Select(ur => ur.UserId).Distinct().ToList();
                    baseQuery = baseQuery.Where(x => userIdsWithRoles.Contains(x.Id));
                }
                else if (filtroRoles == "__sin_roles__")
                {
                    var userIdsWithRoles = _context.UserRoles.Select(ur => ur.UserId).Distinct().ToList();
                    baseQuery = baseQuery.Where(x => !userIdsWithRoles.Contains(x.Id));
                }
                else
                {
                    var userIdsForRole = _context.UserRoles.Where(ur => ur.RoleId == filtroRoles)
                        .Select(ur => ur.UserId).Distinct().ToList();
                    baseQuery = baseQuery.Where(x => userIdsForRole.Contains(x.Id));
                }
            }

            var query = baseQuery
                .Select(usu => new UsuarioEndpointListadoDTO
                {
                    Id = usu.Id,
                    Usuario = usu.UserName,
                    Nombre = usu.Personas != null ? usu.Personas.Apellido + " " + usu.Personas.Nombres : " ",
                    NroTarjeta = usu.Personas != null ? usu.Personas.NroTarjeta : " ",
                    Empresa = "",
                    Administrador = usu.Administradores,
                    AdministradorTexto = usu.Administradores == true ? "SI" : "NO",
                    NroDocumento = usu.Personas != null ? usu.Personas.NroDocumento : " ",
                    Categoria = usu.UsuariosCategorias != null ? usu.UsuariosCategorias.Nombre : "Sin Categoría",
                    CodigoColor = usu.UsuariosCategorias != null ? usu.UsuariosCategorias.CodigoColor : null,
                    Incobrable = usu.Incobrable,
                    TipoUsuario = usu.UsuarioSucursal ? "Sucursal" : "Común"
                });

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var texto = buscar.Trim().ToLower();
                query = query.Where(x =>
                    (x.Usuario ?? "").ToLower().Contains(texto) ||
                    (x.Nombre ?? "").ToLower().Contains(texto) ||
                    (x.NroDocumento ?? "").ToLower().Contains(texto) ||
                    (x.NroTarjeta ?? "").ToLower().Contains(texto) ||
                    (x.Categoria ?? "").ToLower().Contains(texto));
            }

            var total = query.Count();
            var items = query
                .OrderBy(x => x.Usuario)
                .Skip((pagina - 1) * cantidad)
                .Take(cantidad)
                .ToList();

            return Ok(new UsuarioEndpointListadoResponseDTO
            {
                Items = items,
                TotalRegistros = total,
                PaginaActual = pagina,
                CantidadPorPagina = cantidad,
                TotalPaginas = (int)Math.Ceiling(total / (double)cantidad),
                Buscar = buscar ?? ""
            });
        }

        [HttpPost("crear-sucursal")]
        public async Task<IActionResult> CreateUserSucursal([FromBody] UsuarioSucursalVM vm)
        {
            try
            {
                if (vm == null || string.IsNullOrWhiteSpace(vm.Mail))
                    return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "Debe ingresar el mail." });

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var user = await _userService.FindByEmailAsync(vm.Mail.Trim());
                if (user != null)
                    return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "El mail ya se encuentra registrado." });

                Usuario nuevoUsuario = new Usuario
                {
                    UserName = vm.Mail.Trim(),
                    Email = vm.Mail.Trim(),
                    Mail = vm.Mail.Trim(),
                    Password = vm.Password,
                    Administradores = false,
                    UsuarioSucursal = true,
                    Sucursales = await _context.Sucursales.FindAsync(vm.SucursalId),
                    EmailConfirmed = true
                };

                var result = await _userService.CreateAsync(nuevoUsuario, vm.Password);
                if (!result.Succeeded)
                    return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "Ocurrió un error al crear el Usuario Sucursal.", Data = result.Errors });

                Persona nuevaPersona = new Persona
                {
                    NroDocumento = "",
                    Nombres = vm.Nombre,
                    Apellido = "",
                    Email = vm.Mail.Trim()
                };
                await _context.Personas.AddAsync(nuevaPersona);
                nuevoUsuario.Personas = nuevaPersona;
                _context.Usuarios.Update(nuevoUsuario);
                await _context.SaveChangesAsync();

                return Ok(new UsuarioEndpointResponseDTO { Success = true, Respuesta = true, Message = "Usuario Sucursal creado correctamente.", Data = new { nuevoUsuario.Id } });
            }
            catch (Exception)
            {
                return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "Ocurrió un error al crear el Usuario Sucursal." });
            }
        }

        [HttpPatch("{id}/incobrable")]
        public async Task<IActionResult> ToggleIncobrable(string id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                    return NotFound(new { success = false, mensaje = "Usuario no encontrado." });

                usuario.Incobrable = !usuario.Incobrable;
                _context.Usuarios.Update(usuario);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, incobrable = usuario.Incobrable, mensaje = "Estado Incobrable actualizado correctamente." });
            }
            catch (Exception)
            {
                return BadRequest(new { success = false, mensaje = "Error al actualizar el estado Incobrable." });
            }
        }

        [HttpGet("obtener/{id}")]
        public async Task<IActionResult> Obtener(string id)
        {
            var usuario = await _context.Usuarios
                .Include(x => x.Personas).ThenInclude(x => x.TipoDocumento)
                .Include(x => x.Personas).ThenInclude(x => x.Pais)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (usuario == null)
                return NotFound(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "No se encontró el usuario." });

            return Ok(MapUsuario(usuario));
        }

        [HttpPost("buscar-loan")]
        public IActionResult BuscarUsuarioLoan([FromBody] UsuarioBuscarLoanRequestDTO request)
        {
            var numeroTarjeta = request != null ? request.NumeroTarjeta : "";
            if (string.IsNullOrWhiteSpace(numeroTarjeta))
                return BadRequest(new UsuarioEstanciaEndpointDTO { Error = true, Mensaje = "Debe ingresar el Número de Tarjeta." });

            var persona = _context.Personas
                .FirstOrDefault(x => x.NroTarjeta == numeroTarjeta.TrimStart('0').ToString());

            var newUser = new UsuarioEstanciaEndpointDTO();
            if (persona != null)
            {
                newUser.Error = true;
                newUser.Mensaje = "El Número de Tarjeta ya esta regisrado a un Usuario";
                return Ok(newUser);
            }

            var personaLoan = GetPersonaloanByNroTarjeta(numeroTarjeta.ToString());
            if (personaLoan != null)
            {
                newUser.TarjetaEstancia = numeroTarjeta;
                newUser.NroDocumento = personaLoan.NroDocumento;
                newUser.Apellido = personaLoan.Apellido;
                newUser.Nombre = personaLoan.Nombres;
                newUser.Mail = personaLoan.Email;
                newUser.Error = false;
                newUser.Mensaje = "";
            }
            else
            {
                newUser.Error = true;
                newUser.Mensaje = "No se encontró ninguna Persona con el Número de Tarjeta";
            }

            return Ok(newUser);
        }

        [HttpPost("crear-estancia")]
        public async Task<IActionResult> CrearUsuarioEstancia([FromBody] UsuarioEstanciaEndpointDTO userDTO)
        {
            try
            {
                var empresa = _context.Empresas.FirstOrDefault(x => x.Id == 3);
                var user = await _userService.FindByEmailAsync(userDTO.Mail.ToString().Trim());
                var personaLoan = GetPersonaloanByNroTarjeta(userDTO.TarjetaEstancia.ToString());

                if (user != null && userDTO.TarjetaEstancia != null)
                {
                    if (user.Personas != null)
                    {
                        user.Personas.NroTarjeta = userDTO.TarjetaEstancia.ToString();
                        _context.Usuarios.Update(user);
                        _context.SaveChanges();
                    }
                }

                var clienteLocal = _context.Clientes.FirstOrDefault(x => x.Persona.Email == userDTO.Mail.ToString().Trim());
                int token = new Random().Next(100000, 999999);

                if (userDTO.Password == null)
                    return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "Debe ingresar la contraseña." });

                if (empresa == null)
                    return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "No se encontró la empresa." });

                if (clienteLocal != null)
                {
                    if (clienteLocal.Persona != null)
                    {
                        if (user != null)
                        {
                            if (user.Clientes == null || clienteLocal.Id != user.Clientes.Id)
                            {
                                clienteLocal.RegistroMobile = true;
                                CreateOrUpdateUser(user, clienteLocal, userDTO.Mail.Trim(), token, userDTO.Password);
                                await _userService.ChangePasswordAsync(user, "xahs567g", userDTO.Password);
                            }
                        }
                        else
                        {
                            clienteLocal.RegistroMobile = true;
                            CreateOrUpdateUser(user, clienteLocal, userDTO.Mail.Trim(), token, userDTO.Password);
                        }
                    }
                    else
                    {
                        clienteLocal.Persona = new Persona
                        {
                            NroDocumento = userDTO.NroDocumento.ToString(),
                            Apellido = userDTO.Apellido,
                            Nombres = userDTO.Nombre,
                            FechaNacimiento = personaLoan != null ? Convert.ToDateTime(personaLoan.FechaNacimiento) : DateTime.Now,
                            Email = userDTO.Mail.Trim(),
                            NroTarjeta = userDTO.TarjetaEstancia.ToString().TrimStart('0')
                        };

                        if (userDTO.TarjetaEstancia != null)
                        {
                            var persona = GetPersonaloanByNroTarjeta(userDTO.TarjetaEstancia.ToString());
                            if (persona != null)
                                clienteLocal.Persona.NroDocumento = persona.NroDocumento;

                            if (personaLoan != null)
                                clienteLocal.Persona.FechaNacimiento = Convert.ToDateTime(personaLoan.FechaNacimiento);
                        }

                        clienteLocal.RegistroMobile = true;
                        CreateOrUpdateUser(user, clienteLocal, userDTO.Mail.Trim(), token, userDTO.Password);
                        await _userService.ChangePasswordAsync(user, "xahs567g", userDTO.Password);
                    }
                }
                else
                {
                    Clientes cliente = new Clientes();
                    cliente.Empresa = _context.Empresas.FirstOrDefault();
                    cliente.TipoCliente = _context.TiposClientes.Find(1);
                    cliente.Celular = userDTO.Telefono != null ? userDTO.Telefono : "";
                    cliente.Localidad = _context.Localidad.Find(24860);
                    cliente.Provincia = _context.Provincia.Find(6);

                    Persona personaLocal = _context.Personas.FirstOrDefault(x => x.Email == userDTO.Mail.Trim());

                    if (personaLocal != null)
                    {
                        cliente.Persona = personaLocal;
                        cliente.RegistroMobile = true;
                        user = CreateOrUpdateUser(user, cliente, userDTO.Mail.Trim(), token, userDTO.Password);
                        await _userService.ChangePasswordAsync(user, "xahs567g", userDTO.Password);
                    }
                    else
                    {
                        cliente.Persona = new Persona
                        {
                            NroDocumento = userDTO.NroDocumento.ToString(),
                            Apellido = userDTO.Apellido,
                            Nombres = userDTO.Nombre,
                            FechaNacimiento = personaLoan != null ? Convert.ToDateTime(personaLoan.FechaNacimiento) : DateTime.Now,
                            Email = userDTO.Mail.Trim(),
                            NroTarjeta = userDTO.TarjetaEstancia.ToString().TrimStart('0')
                        };

                        if (userDTO.TarjetaEstancia != null)
                        {
                            var persona = GetPersonaloanByNroTarjeta(userDTO.TarjetaEstancia.ToString());
                            if (persona != null)
                                cliente.Persona.NroDocumento = persona.NroDocumento;
                        }

                        cliente.RegistroMobile = true;
                        user = CreateOrUpdateUser(user, cliente, userDTO.Mail.Trim(), token, userDTO.Password);
                        await _userService.ChangePasswordAsync(user, "xahs567g", userDTO.Password);
                    }
                }

                user = await _userService.FindByEmailAsync(userDTO.Mail.ToString().Trim());
                if (user != null)
                {
                    user.EmailConfirmed = true;
                    user.Password = userDTO.Password;
                    var tokenReset = await _userService.GeneratePasswordResetTokenAsync(user);
                    await _userService.ResetPasswordAsync(user, tokenReset, userDTO.Password);
                    _context.Usuarios.Update(user);
                    _context.SaveChanges();

                    return Ok(new UsuarioEndpointResponseDTO { Success = true, Respuesta = true, Message = "Se creó correctamente el Usuario.", Data = MapUsuario(user) });
                }

                return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "Error al crear el Usuario." });
            }
            catch (Exception e)
            {
                return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = e.Message });
            }
        }

        [HttpPost("crear")]
        public async Task<IActionResult> Crear([FromBody] UsuarioEndpointDTO usuario)
        {
            try
            {
                Usuario nuevoUsuario = new Usuario
                {
                    UserName = usuario.Mail,
                    Email = usuario.Mail,
                    Mail = usuario.Mail,
                    Password = usuario.Password,
                    Administradores = usuario.Administrador
                };

                var result = await _userService.CreateAsync(nuevoUsuario, usuario.Password);
                if (!result.Succeeded)
                    return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "No se pudo crear el usuario.", Data = result.Errors });

                var persona = new Persona
                {
                    NroDocumento = usuario.Persona.NroDocumento,
                    Apellido = usuario.Persona.Apellido,
                    Nombres = usuario.Persona.Nombres,
                    Cuil = usuario.Persona.Cuil,
                    FechaNacimiento = usuario.Persona.TieneFechaNacimiento ? usuario.Persona.FechaNacimiento : (DateTime?)null,
                    NroTarjeta = usuario.TarjetaEstancia,
                    Email = usuario.Mail,
                    TipoDocumento = _context.TipoDocumento.Find(usuario.Persona.TipoDocumentoId),
                    Pais = _context.Paises.Find(usuario.Persona.PaisId)
                };

                await _context.Personas.AddAsync(persona);
                nuevoUsuario.Personas = persona;

                nuevoUsuario.Clientes = new Clientes();
                nuevoUsuario.Clientes.Empresa = _context.Empresas.FirstOrDefault();
                nuevoUsuario.Clientes.Persona = persona;
                nuevoUsuario.Clientes.RazonSocial = persona.Apellido + ", " + persona.Nombres;
                nuevoUsuario.Clientes.FechaIngreso = DateTime.Now;
                await _context.Clientes.AddAsync(nuevoUsuario.Clientes);

                _context.Usuarios.Update(nuevoUsuario);
                await _context.SaveChangesAsync();

                return Ok(new UsuarioEndpointResponseDTO { Success = true, Respuesta = true, Message = "Se creó correctamente el Usuario.", Data = MapUsuario(nuevoUsuario) });
            }
            catch (Exception e)
            {
                return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = e.Message });
            }
        }

        [HttpPost("buscar")]
        public IActionResult BuscarUsuario([FromBody] UsuarioBuscarRequestDTO request)
        {
            Usuario usuario = null;
            Persona persona = null;
            Clientes cliente = null;
            var valor = request != null ? request.Valor : "";
            var tipoDeBusqueda = request != null ? request.TipoDeBusqueda : "";

            switch (tipoDeBusqueda)
            {
                case "1":
                    persona = _context.Personas.FirstOrDefault(x => x.NroDocumento == valor);
                    if (persona != null)
                    {
                        usuario = _context.Usuarios.FirstOrDefault(x => x.Personas.Id == persona.Id);
                        if (usuario != null) cliente = _context.Clientes.FirstOrDefault(x => x.UsuarioId == usuario.Id);
                    }
                    break;
                case "2":
                    usuario = _context.Usuarios.FirstOrDefault(x => x.UserName == valor);
                    if (usuario != null)
                    {
                        persona = usuario.Personas;
                        cliente = _context.Clientes.FirstOrDefault(x => x.UsuarioId == usuario.Id);
                    }
                    break;
                case "3":
                    persona = _context.Personas.Where(x => x.NroTarjeta != null).FirstOrDefault(x => x.NroTarjeta.TrimStart('0') == valor.TrimStart('0'));
                    if (persona != null)
                    {
                        usuario = _context.Usuarios.FirstOrDefault(x => x.Personas.Id == persona.Id);
                        if (usuario != null) cliente = _context.Clientes.FirstOrDefault(x => x.UsuarioId == usuario.Id);
                    }
                    break;
            }

            if (usuario == null && persona == null && cliente == null)
                return Ok(new { success = false, message = "No se encontraron resultados." });

            var usuarioDto = new UsuarioBusquedaDTO();

            if (persona != null)
            {
                usuarioDto.NroDocumento = persona.NroDocumento;
                usuarioDto.Apellidos = persona.Apellido;
                usuarioDto.Nombres = persona.Nombres;
                usuarioDto.NroTarjeta = persona.NroTarjeta;
                usuarioDto.FechaNacimiento = persona.FechaNacimiento.HasValue ? persona.FechaNacimiento.Value.ToString("dd/MM/yyyy") : "";
                usuarioDto.Persona = 1;
                usuarioDto.Pagos = _context.PagoTarjeta
                    .Where(x => x.Persona.Id == persona.Id)
                    .Select(x => new UsuarioBusquedaComprobanteDTO
                    {
                        FechaComprobante = ((DateTime)x.FechaComprobante).ToString("dd/MM/yyyy"),
                        ComprobantePago = x.ComprobantePago
                    }).ToList();
            }
            else
            {
                usuarioDto.Persona = 0;
            }

            if (usuario != null)
            {
                usuarioDto.UserName = usuario.UserName;
                usuarioDto.DeviceId = usuario.DeviceId;
                usuarioDto.WonderPushDeviceId = usuario.UserIdNotification;
                usuarioDto.Usuario = 1;
            }
            else
            {
                usuarioDto.Usuario = 0;
            }

            if (cliente != null)
            {
                usuarioDto.Celular = cliente.Celular;
                usuarioDto.Cliente = 1;
            }
            else
            {
                usuarioDto.Cliente = 0;
            }

            return Ok(new { success = true, usuario = usuarioDto });
        }

        [HttpPost("borrar")]
        public IActionResult BorrarUsuario([FromBody] UsuarioBorrarRequestDTO request)
        {
            Usuario usuario = null;
            Persona persona = null;
            Clientes cliente = null;
            var valor = request != null ? request.Valor : "";
            var tipoDeBusqueda = request != null ? request.TipoDeBusqueda : "";

            try
            {
                switch (tipoDeBusqueda)
                {
                    case "1":
                        persona = _context.Personas.FirstOrDefault(x => x.NroDocumento == valor);
                        if (persona != null)
                        {
                            usuario = _context.Usuarios.FirstOrDefault(x => x.Personas.Id == persona.Id);
                            if (usuario != null) cliente = _context.Clientes.FirstOrDefault(x => x.UsuarioId == usuario.Id);
                        }
                        break;
                    case "2":
                        usuario = _context.Usuarios.FirstOrDefault(x => x.UserName == valor);
                        if (usuario != null)
                        {
                            persona = usuario.Personas;
                            cliente = _context.Clientes.FirstOrDefault(x => x.UsuarioId == usuario.Id);
                        }
                        break;
                    case "3":
                        persona = _context.Personas.Where(x => x.NroTarjeta != null).FirstOrDefault(x => x.NroTarjeta.TrimStart('0') == valor.TrimStart('0'));
                        if (persona != null)
                        {
                            usuario = _context.Usuarios.FirstOrDefault(x => x.Personas.Id == persona.Id);
                            if (usuario != null) cliente = _context.Clientes.FirstOrDefault(x => x.UsuarioId == usuario.Id);
                        }
                        break;
                }

                if (cliente != null)
                {
                    var uats = _context.UAT.Where(x => x.Cliente.Id == cliente.Id).ToList();
                    var notificaciones = _context.NotificacionesPersonas.Where(x => x.Cliente.Id == cliente.Id).ToList();
                    _context.UAT.RemoveRange(uats);
                    _context.NotificacionesPersonas.RemoveRange(notificaciones);
                    _context.Clientes.Remove(cliente);
                    _context.SaveChanges();
                }

                if (usuario != null)
                {
                    _context.Usuarios.Remove(usuario);
                    _context.SaveChanges();
                }

                if (persona != null)
                {
                    var pagos = _context.PagoTarjeta.Where(x => x.Persona.Id == persona.Id).ToList();
                    _context.PagoTarjeta.RemoveRange(pagos);
                    _context.Personas.Remove(persona);
                    _context.SaveChanges();
                }

                return Ok(new { success = true, respuesta = true });
            }
            catch
            {
                return Ok(new { success = true, respuesta = false });
            }
        }

        [HttpPut("actualizar")]
        public async Task<IActionResult> Actualizar([FromBody] UsuarioEndpointDTO usuario)
        {
            try
            {
                Persona persona = _context.Personas.FirstOrDefault(x => x.NroDocumento == usuario.Persona.NroDocumento);
                if (persona != null) usuario.Persona.Id = persona.Id;

                Persona personaFinal;
                if (usuario.Persona.Id != 0)
                {
                    Persona updatePersona = await _context.Personas.FindAsync(usuario.Persona.Id);
                    updatePersona.NroDocumento = usuario.Persona.NroDocumento;
                    updatePersona.NroTarjeta = usuario.TarjetaEstancia;
                    updatePersona.Nombres = usuario.Persona.Nombres;
                    updatePersona.Apellido = usuario.Persona.Apellido;
                    updatePersona.Cuil = usuario.Persona.Cuil;
                    updatePersona.FechaNacimiento = usuario.Persona.TieneFechaNacimiento ? usuario.Persona.FechaNacimiento : (DateTime?)null;
                    updatePersona.TipoDocumento = await _context.TipoDocumento.FindAsync(usuario.Persona.TipoDocumentoId);
                    updatePersona.Pais = await _context.Paises.FindAsync(usuario.Persona.PaisId);
                    _context.Personas.Update(updatePersona);
                    await _context.SaveChangesAsync();
                    personaFinal = updatePersona;
                }
                else
                {
                    Persona newPersona = new Persona();
                    newPersona.NroDocumento = usuario.Persona.NroDocumento;
                    newPersona.NroTarjeta = usuario.TarjetaEstancia;
                    newPersona.Nombres = usuario.Persona.Nombres;
                    newPersona.Apellido = usuario.Persona.Apellido;
                    newPersona.Cuil = usuario.Persona.Cuil;
                    newPersona.FechaNacimiento = usuario.Persona.TieneFechaNacimiento ? usuario.Persona.FechaNacimiento : (DateTime?)null;
                    newPersona.TipoDocumento = await _context.TipoDocumento.FindAsync(usuario.Persona.TipoDocumentoId);
                    newPersona.Pais = await _context.Paises.FindAsync(usuario.Persona.PaisId);
                    _context.Personas.Add(newPersona);
                    await _context.SaveChangesAsync();
                    personaFinal = newPersona;
                }

                var user = await _context.Users.FindAsync(usuario.UserId);
                if (user == null)
                    return NotFound(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "No se encontró el usuario." });

                user.Personas = personaFinal;
                user.UserName = usuario.Mail;
                user.NormalizedUserName = usuario.Mail.ToUpper();
                user.Email = usuario.Mail;
                user.NormalizedEmail = usuario.Mail.ToUpper();
                user.Mail = usuario.Mail;
                user.Administradores = usuario.Administrador;
                await _userManager.UpdateAsync(user);

                if (usuario.Administrador)
                {
                    user.Clientes = null;
                }
                else
                {
                    if (user.Clientes == null)
                    {
                        user.Clientes = new Clientes();
                        user.Clientes.Empresa = _context.Empresas.Find(1);
                        user.Clientes.Persona = personaFinal;
                        await _context.Clientes.AddAsync(user.Clientes);
                    }
                    else
                    {
                        user.Clientes.Empresa = _context.Empresas.Find(1);
                        user.Clientes.Persona = personaFinal;
                        await _context.Clientes.AddAsync(user.Clientes);
                    }
                }

                _context.Update(user);
                await _context.SaveChangesAsync();

                return Ok(new UsuarioEndpointResponseDTO { Success = true, Respuesta = true, Message = "Se actualizó correctamente el Usuario.", Data = MapUsuario(user) });
            }
            catch (Exception e)
            {
                return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = e.Message });
            }
        }

        [HttpPut("habilitar-admin/{id}")]
        public async Task<IActionResult> HabilitarAdmin(string id)
        {
            return await CambiarAdmin(id, true);
        }

        [HttpPut("deshabilitar-admin/{id}")]
        public async Task<IActionResult> DeshabilitarAdmin(string id)
        {
            return await CambiarAdmin(id, false);
        }

        [HttpPost("cambiar-password")]
        public async Task<IActionResult> CambiarPassword([FromBody] UsuarioPasswordRequestDTO request)
        {
            try
            {
                if (request.Password != request.RepeatPassword)
                    return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "Hubo un error al cambiar la contraseña del usuario." });

                var usuario = await _userManager.FindByIdAsync(request.UserId);
                if (usuario == null)
                    return NotFound(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "Hubo un error al cambiar la contraseña del usuario." });

                var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
                var result = await _userManager.ResetPasswordAsync(usuario, token, request.Password);
                if (!result.Succeeded)
                    return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "Hubo un error al cambiar la contraseña del usuario.", Data = result.Errors });

                usuario.Password = request.Password;
                _context.Usuarios.Update(usuario);
                _context.SaveChanges();

                return Ok(new UsuarioEndpointResponseDTO { Success = true, Respuesta = true, Message = "Se ha cambiado la contraseña del usuario exitosamente." });
            }
            catch (Exception e)
            {
                return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = e.Message });
            }
        }

        [HttpGet("categorias")]
        public IActionResult Categorias(string userId = "")
        {
            var usuario = _context.Usuarios.Include(u => u.UsuariosCategorias).FirstOrDefault(u => u.Id == userId);
            var categorias = _context.UsuariosCategorias
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .Select(x => new UsuarioCategoriaDTO
                {
                    Id = x.Id,
                    Nombre = x.Nombre,
                    Selected = usuario != null && usuario.UsuariosCategorias != null && usuario.UsuariosCategorias.Id == x.Id
                }).ToList();

            return Ok(categorias);
        }

        [HttpPost("cambiar-categoria")]
        public async Task<IActionResult> CambiarCategoria([FromBody] UsuarioCategoriaRequestDTO request)
        {
            try
            {
                var usuario = await _context.Usuarios
                    .Include(u => u.UsuariosCategorias)
                    .FirstOrDefaultAsync(u => u.Id == request.UserId);

                if (usuario == null)
                    return NotFound(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "No se encontró el usuario." });

                if (request.CategoryId > 0)
                {
                    var categoria = await _context.UsuariosCategorias.FindAsync(request.CategoryId);
                    usuario.UsuariosCategorias = categoria;
                }
                else
                {
                    usuario.UsuariosCategorias = null;
                }

                _context.Usuarios.Update(usuario);
                await _context.SaveChangesAsync();

                return Ok(new UsuarioEndpointResponseDTO { Success = true, Respuesta = true, Message = "Se actualizó la categoría del usuario " + usuario.UserName + " correctamente." });
            }
            catch (Exception e)
            {
                return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = e.Message });
            }
        }

        private async Task<IActionResult> CambiarAdmin(string id, bool administrador)
        {
            try
            {
                Usuario usuario = _context.Usuarios.FirstOrDefault(s => s.Id == id);
                if (usuario == null)
                    return NotFound(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = "No se encontró el usuario." });

                usuario.Administradores = administrador;
                _context.Usuarios.Update(usuario);
                await _context.SaveChangesAsync();

                return Ok(new UsuarioEndpointResponseDTO { Success = true, Respuesta = true, Message = "Se modificó correctamente el Usuario " + usuario.UserName + "." });
            }
            catch (Exception e)
            {
                return BadRequest(new UsuarioEndpointResponseDTO { Success = false, Respuesta = false, Message = e.Message });
            }
        }

        private UsuarioEndpointDTO MapUsuario(Usuario usuario)
        {
            var dto = new UsuarioEndpointDTO
            {
                UserId = usuario.Id,
                Mail = usuario.Email,
                Password = usuario.Password,
                Administrador = usuario.Administradores,
                TarjetaEstancia = usuario.Personas != null ? usuario.Personas.NroTarjeta : "",
                Persona = new UsuarioEndpointPersonaDTO()
            };

            if (usuario.Personas != null)
            {
                dto.Persona.Id = usuario.Personas.Id;
                dto.Persona.NroDocumento = usuario.Personas.NroDocumento;
                dto.Persona.Apellido = usuario.Personas.Apellido;
                dto.Persona.Nombres = usuario.Personas.Nombres;
                dto.Persona.Cuil = usuario.Personas.Cuil;
                dto.Persona.Email = usuario.Personas.Email;
                dto.Persona.NroTarjeta = usuario.Personas.NroTarjeta;
                dto.Persona.TieneFechaNacimiento = usuario.Personas.FechaNacimiento.HasValue;
                dto.Persona.FechaNacimiento = usuario.Personas.FechaNacimiento.HasValue ? usuario.Personas.FechaNacimiento.Value : DateTime.MinValue;
                dto.Persona.TipoDocumentoId = usuario.Personas.TipoDocumento != null ? usuario.Personas.TipoDocumento.Id : 0;
                dto.Persona.TipoDocumentoDescripcion = usuario.Personas.TipoDocumento != null ? usuario.Personas.TipoDocumento.Descripcion : "";
                dto.Persona.PaisId = usuario.Personas.Pais != null ? usuario.Personas.Pais.Id : 0;
                dto.Persona.PaisNombre = usuario.Personas.Pais != null ? usuario.Personas.Pais.Nombre : "";
            }

            return dto;
        }

        private PersonaLoan GetPersonaloanByNroTarjeta(string nroTarjeta)
        {
            BDExternaPersonalService consulta = new BDExternaPersonalService(_context);
            var persona = consulta.getPersonaloanByNroTarjeta(nroTarjeta) != null ? consulta.getPersonaloanByNroTarjeta(nroTarjeta).FirstOrDefault() : null;
            return persona;
        }

        private void UpdateUser(Usuario usuario, Clientes cliente)
        {
            cliente.Usuario = usuario;
            usuario.Clientes = cliente;
            usuario.Personas = cliente.Persona;
            _context.Update(usuario);
            _context.SaveChanges();
        }

        private bool CreateUser(string userName, string email, int token, string password = "xahs567g")
        {
            try
            {
                var user = new Usuario
                {
                    UserName = userName,
                    Email = email,
                    Token = token
                };
                var result = _userService.CreateAsync(user, password);
                return result.Result.Succeeded;
            }
            catch
            {
                return false;
            }
        }

        private Usuario CreateOrUpdateUser(Usuario usuario, Clientes clienteLocal = null, string mail = null, int token = 0, string password = "xahs567g")
        {
            try
            {
                if (usuario != null)
                {
                    Clientes cliente = _context.Clientes.FirstOrDefault(x => x.UsuarioId == usuario.Id);
                    if (cliente != null)
                    {
                        cliente.Persona = clienteLocal.Persona;
                        clienteLocal = cliente;
                    }
                    UpdateUser(usuario, clienteLocal);
                    return usuario;
                }

                if (CreateUser(mail, mail, token, password))
                {
                    Usuario usuarioLocal = _context.Usuarios.FirstOrDefault(x => x.UserName == mail);
                    UpdateUser(usuarioLocal, clienteLocal);
                    return usuarioLocal;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
