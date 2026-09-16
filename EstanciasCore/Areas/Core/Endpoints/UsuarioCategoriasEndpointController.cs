using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/usuarios-categorias")]
    [ApiController]
    public class UsuariosCategoriasEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;

        public UsuariosCategoriasEndpointController(EstanciasContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string q)
        {
            var query = _context.UsuariosCategorias.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(c => c.Nombre.Contains(q) || (c.Codigo != null && c.Codigo.Contains(q)));
            }

            var categorias = await query
                .OrderBy(c => c.Orden)
                .Select(c => new UsuariosCategoriasDTO
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Codigo = c.Codigo,
                    Color = c.Color,
                    CodigoColor = c.CodigoColor,
                    Orden = c.Orden,
                    Activo = c.Activo,
                    TieneImagenTarjeta = c.ImagenTarjeta != null
                })
                .ToListAsync();

            return Ok(new
            {
                ok = true,
                data = categorias
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var categoria = await _context.UsuariosCategorias
                .Where(c => c.Id == id)
                .Select(c => new UsuariosCategoriasDTO
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Codigo = c.Codigo,
                    Color = c.Color,
                    CodigoColor = c.CodigoColor,
                    Orden = c.Orden,
                    Activo = c.Activo,
                    TieneImagenTarjeta = c.ImagenTarjeta != null
                })
                .FirstOrDefaultAsync();

            if (categoria == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la categoría de usuario."
                });
            }

            return Ok(new
            {
                ok = true,
                data = categoria
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UsuarioCategoriaCreateRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Los datos de la categoría son obligatorios."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "El nombre de la categoría es obligatorio."
                });
            }

            var categoria = new UsuariosCategorias
            {
                Nombre = request.Nombre,
                Codigo = request.Codigo,
                Color = request.Color,
                CodigoColor = request.CodigoColor,
                Orden = request.Orden,
                Activo = request.Activo
            };

            await _context.UsuariosCategorias.AddAsync(categoria);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Categoría de usuario creada correctamente.",
                data = new UsuariosCategoriasDTO
                {
                    Id = categoria.Id,
                    Nombre = categoria.Nombre,
                    Codigo = categoria.Codigo,
                    Color = categoria.Color,
                    CodigoColor = categoria.CodigoColor,
                    Orden = categoria.Orden,
                    Activo = categoria.Activo,
                    TieneImagenTarjeta = categoria.ImagenTarjeta != null
                }
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UsuarioCategoriaUpdateRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Los datos de la categoría son obligatorios."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "El nombre de la categoría es obligatorio."
                });
            }

            var categoria = await _context.UsuariosCategorias
                .FirstOrDefaultAsync(c => c.Id == id);

            if (categoria == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la categoría de usuario."
                });
            }

            categoria.Nombre = request.Nombre;
            categoria.Codigo = request.Codigo;
            categoria.Color = request.Color;
            categoria.CodigoColor = request.CodigoColor;
            categoria.Orden = request.Orden;
            categoria.Activo = request.Activo;

            _context.UsuariosCategorias.Update(categoria);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Categoría de usuario actualizada correctamente.",
                data = new UsuariosCategoriasDTO
                {
                    Id = categoria.Id,
                    Nombre = categoria.Nombre,
                    Codigo = categoria.Codigo,
                    Color = categoria.Color,
                    CodigoColor = categoria.CodigoColor,
                    Orden = categoria.Orden,
                    Activo = categoria.Activo,
                    TieneImagenTarjeta = categoria.ImagenTarjeta != null
                }
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var categoria = await _context.UsuariosCategorias
                .FirstOrDefaultAsync(c => c.Id == id);

            if (categoria == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la categoría de usuario."
                });
            }

            _context.UsuariosCategorias.Remove(categoria);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Categoría de usuario eliminada correctamente."
            });
        }

        [HttpPatch("{id}/orden")]
        public async Task<IActionResult> ModificarOrden(int id, [FromBody] UsuarioCategoriaOrdenRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Debe enviar el nuevo orden."
                });
            }

            var categoria = await _context.UsuariosCategorias
                .FirstOrDefaultAsync(c => c.Id == id);

            if (categoria == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la categoría de usuario."
                });
            }

            categoria.Orden = request.Orden;

            _context.UsuariosCategorias.Update(categoria);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Orden actualizado correctamente.",
                data = new UsuariosCategoriasDTO
                {
                    Id = categoria.Id,
                    Nombre = categoria.Nombre,
                    Color = categoria.Color,
                    CodigoColor = categoria.CodigoColor,
                    Orden = categoria.Orden,
                    Activo = categoria.Activo,
                    TieneImagenTarjeta = categoria.ImagenTarjeta != null
                }
            });
        }

        [HttpPost("{id}/imagen")]
        public async Task<IActionResult> CambiarImagen(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Debe enviar una imagen."
                });
            }

            var categoria = await _context.UsuariosCategorias.FindAsync(id);

            if (categoria == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la categoría de usuario."
                });
            }

            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using (var img = Image.FromStream(memoryStream))
                {
                    int width = img.Width;
                    int height = img.Height;

                    if (width > 1080 || height > 1080)
                    {
                        if (width > height)
                        {
                            height = (int)(height * (1080.0 / width));
                            width = 1080;
                        }
                        else
                        {
                            width = (int)(width * (1080.0 / height));
                            height = 1080;
                        }

                        using (var newImg = new Bitmap(width, height))
                        using (var g = Graphics.FromImage(newImg))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.CompositingQuality = CompositingQuality.HighQuality;

                            g.DrawImage(img, 0, 0, width, height);

                            using (var outputStream = new MemoryStream())
                            {
                                newImg.Save(outputStream, ImageFormat.Png);
                                categoria.ImagenTarjeta = outputStream.ToArray();
                            }
                        }
                    }
                    else
                    {
                        categoria.ImagenTarjeta = memoryStream.ToArray();
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Imagen actualizada correctamente.",
                data = new UsuariosCategoriasDTO
                {
                    Id = categoria.Id,
                    Nombre = categoria.Nombre,
                    Color = categoria.Color,
                    CodigoColor = categoria.CodigoColor,
                    Orden = categoria.Orden,
                    Activo = categoria.Activo,
                    TieneImagenTarjeta = categoria.ImagenTarjeta != null
                }
            });
        }
    }
}