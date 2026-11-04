using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace EstanciasCore.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/empresas")]
    [ApiController]
    public class EmpresasEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;

        public EmpresasEndpointController(EstanciasContext context)
        {
            _context = context;
        }

        // GET: endpoint/empresas
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var empresas = await _context.Empresas
                .Select(e => new EmpresaDTO
                {
                    Id = e.Id,
                    CUIT = e.CUIT.ToString(),
                    RazonSocial = e.RazonSocial,
                    Grupo = e.Grupo != null ? e.Grupo.Nombre : null
                })
                .ToListAsync();

            return Ok(new
            {
                ok = true,
                data = empresas
            });
        }

        // GET: endpoint/empresas/grupos
        [HttpGet("grupos")]
        public async Task<IActionResult> GetGrupos()
        {
            var grupos = await _context.Grupos
                .Select(g => new EmpresaGrupoDTO
                {
                    Id = g.Id,
                    Nombre = g.Nombre
                })
                .ToListAsync();

            return Ok(new
            {
                ok = true,
                data = grupos
            });
        }

        // GET: endpoint/empresas/datos-bancarios
        [HttpGet("datos-bancarios")]
        public async Task<IActionResult> GetDatosBancarios()
        {
            var datos = await _context.DatosEstructura
                .Select(d => new EmpresaDatosBancariosDTO
                {
                    Telefono = d.Telefono,
                    CUIT = d.CUIT,
                    CBU = d.CBU,
                    Alias = d.Alias
                })
                .FirstOrDefaultAsync();

            if (datos == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontraron datos bancarios."
                });
            }

            return Ok(new
            {
                ok = true,
                data = datos
            });
        }

        // GET: endpoint/empresas/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var empresa = await _context.Empresas
                .Where(e => e.Id == id)
                .Select(e => new EmpresaDetalleDTO
                {
                    Id = e.Id,
                    CUIT = e.CUIT.ToString(),
                    GrupoId = e.Grupo != null ? (int?)e.Grupo.Id : null,
                    Grupo = e.Grupo != null ? e.Grupo.Nombre : null,
                    RazonSocial = e.RazonSocial,
                    Abreviatura = e.Abreviatura,
                    Domicilio = e.Domicilio,
                    Telefono = e.Telefono,
                    Mail = e.Mail,
                    ColorFontCarnet = e.ColorFontCarnet,
                    ColorCarnet = e.ColorCarnet,
                    Twitter = e.Twitter,
                    Facebook = e.Facebook,
                    Instagram = e.Instagram,
                    WhatsApp = e.WhatsApp,
                    ColorFondo = e.ColorFondo,
                    ColorBotones = e.ColorBotones,
                    ColorLogin = e.ColorLogin
                })
                .FirstOrDefaultAsync();

            if (empresa == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la empresa."
                });
            }

            return Ok(new
            {
                ok = true,
                data = empresa
            });
        }

        // POST: endpoint/empresas
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EmpresaCreateRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Los datos de la empresa son obligatorios."
                });
            }

            if (request.CUIT <= 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "El CUIT es obligatorio."
                });
            }

            if (string.IsNullOrWhiteSpace(request.RazonSocial))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "La razón social es obligatoria."
                });
            }

            if (!request.GrupoId.HasValue || request.GrupoId.Value <= 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Debe seleccionar un grupo."
                });
            }

            var grupo = await _context.Grupos.FindAsync(request.GrupoId.Value);

            if (grupo == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró el grupo seleccionado."
                });
            }

            var empresa = new Empresas
            {
                CUIT = request.CUIT,
                RazonSocial = request.RazonSocial,
                Abreviatura = request.Abreviatura,
                Domicilio = request.Domicilio,
                Telefono = request.Telefono,
                Mail = request.Mail,
                Grupo = grupo,
                ColorFontCarnet = request.ColorFontCarnet,
                ColorCarnet = request.ColorCarnet,
                Instagram = request.Instagram,
                Twitter = request.Twitter,
                Facebook = request.Facebook,
                WhatsApp = request.WhatsApp,
                ColorFondo = request.ColorFondo,
                ColorBotones = request.ColorBotones,
                ColorLogin = request.ColorLogin
            };

            await _context.Empresas.AddAsync(empresa);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Empresa creada correctamente.",
                data = new EmpresaDTO
                {
                    Id = empresa.Id,
                    CUIT = empresa.CUIT.ToString(),
                    RazonSocial = empresa.RazonSocial,
                    Grupo = grupo.Nombre
                }
            });
        }

        // PUT: endpoint/empresas/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] EmpresaUpdateRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Los datos de la empresa son obligatorios."
                });
            }

            if (request.CUIT <= 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "El CUIT es obligatorio."
                });
            }

            if (string.IsNullOrWhiteSpace(request.RazonSocial))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "La razón social es obligatoria."
                });
            }

            if (!request.GrupoId.HasValue || request.GrupoId.Value <= 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Debe seleccionar un grupo."
                });
            }

            var empresa = await _context.Empresas
                .Include(e => e.Grupo)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (empresa == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la empresa."
                });
            }

            var grupo = await _context.Grupos.FindAsync(request.GrupoId.Value);

            if (grupo == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró el grupo seleccionado."
                });
            }

            empresa.CUIT = request.CUIT;
            empresa.RazonSocial = request.RazonSocial;
            empresa.Abreviatura = request.Abreviatura;
            empresa.Domicilio = request.Domicilio;
            empresa.Telefono = request.Telefono;
            empresa.Mail = request.Mail;
            empresa.ColorFontCarnet = request.ColorFontCarnet;
            empresa.ColorCarnet = request.ColorCarnet;
            empresa.ColorFondo = request.ColorFondo;
            empresa.ColorBotones = request.ColorBotones;
            empresa.Instagram = request.Instagram;
            empresa.Twitter = request.Twitter;
            empresa.Facebook = request.Facebook;
            empresa.WhatsApp = request.WhatsApp;
            empresa.ColorLogin = request.ColorLogin;
            empresa.Grupo = grupo;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Empresa actualizada correctamente.",
                data = new EmpresaDTO
                {
                    Id = empresa.Id,
                    CUIT = empresa.CUIT.ToString(),
                    RazonSocial = empresa.RazonSocial,
                    Grupo = grupo.Nombre
                }
            });
        }

        // PUT: endpoint/empresas/datos-bancarios
        [HttpPut("datos-bancarios")]
        public async Task<IActionResult> UpdateDatosBancarios([FromBody] EmpresaDatosBancariosUpdateRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Los datos bancarios son obligatorios."
                });
            }

            var datosEstructuraUpdate = await _context.DatosEstructura.FirstOrDefaultAsync();

            if (datosEstructuraUpdate == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontraron datos bancarios para actualizar."
                });
            }

            datosEstructuraUpdate.Telefono = request.Telefono;
            datosEstructuraUpdate.CUIT = request.CUIT;
            datosEstructuraUpdate.CBU = request.CBU;
            datosEstructuraUpdate.Alias = request.Alias;

            _context.DatosEstructura.Update(datosEstructuraUpdate);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Datos bancarios actualizados correctamente.",
                data = new EmpresaDatosBancariosDTO
                {
                    Telefono = datosEstructuraUpdate.Telefono,
                    CUIT = datosEstructuraUpdate.CUIT,
                    CBU = datosEstructuraUpdate.CBU,
                    Alias = datosEstructuraUpdate.Alias
                }
            });
        }

        // POST: endpoint/empresas/5/imagenes
        [HttpPost("{id}/imagenes")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubirImagenes(
            int id,
            IFormFile FondoMobile,
            IFormFile GIFLogoMutual,
            IFormFile LogoMutual,
            IFormFile ImagenLogin)
        {
            try
            {
                var empresa = await _context.Empresas.FindAsync(id);

                if (empresa == null)
                {
                    return NotFound(new
                    {
                        ok = false,
                        message = "No se encontró la empresa."
                    });
                }

                if (FondoMobile != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await FondoMobile.CopyToAsync(memoryStream);
                        empresa.FondoMobile = memoryStream.ToArray();
                    }
                }

                if (GIFLogoMutual != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await GIFLogoMutual.CopyToAsync(memoryStream);
                        empresa.GIFLogoMutual = memoryStream.ToArray();
                    }
                }

                if (LogoMutual != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await LogoMutual.CopyToAsync(memoryStream);
                        empresa.LogoMutual = memoryStream.ToArray();
                    }
                }

                if (ImagenLogin != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await ImagenLogin.CopyToAsync(memoryStream);
                        empresa.ImagenLogin = memoryStream.ToArray();
                    }
                }

                _context.Empresas.Update(empresa);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    ok = true,
                    message = "Se cargaron correctamente las imágenes de la empresa.",
                    data = new { Id = empresa.Id }
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    message = "Hubo un error al cargar las imágenes de la empresa. Intentelo nuevamente más tarde."
                });
            }
        }

        // DELETE: endpoint/empresas/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var empresa = await _context.Empresas
                .FirstOrDefaultAsync(e => e.Id == id);

            if (empresa == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la empresa."
                });
            }

            try
            {
                _context.Empresas.Remove(empresa);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    ok = true,
                    message = "Empresa eliminada correctamente."
                });
            }
            catch (DbUpdateException)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "No se puede eliminar la empresa porque está relacionada con otros registros."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    message = "Ocurrió un error al eliminar la empresa."
                });
            }
        }
    }
}