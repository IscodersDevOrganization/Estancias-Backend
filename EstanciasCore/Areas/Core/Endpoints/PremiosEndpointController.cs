using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/premios")]
    [ApiController]
    public class PremiosEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;

        public PremiosEndpointController(EstanciasContext context)
        {
            _context = context;
        }

        // GET: endpoint/premios?buscar=texto
        [HttpGet]
        public IActionResult GetAll([FromQuery] string buscar = null)
        {
            try
            {
                var query = _context.Premios.AsQueryable();

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    var texto = buscar.Trim().ToLower();

                    query = query.Where(p =>
                        ((p.Nombre ?? "").ToLower().Contains(texto)) ||
                        ((p.Descripcion ?? "").ToLower().Contains(texto)) ||
                        ((p.TerminosCondiciones ?? "").ToLower().Contains(texto)) ||
                        (p.Categoria != null && (p.Categoria.Nombre ?? "").ToLower().Contains(texto)) ||
                        (p.Marcas != null && (p.Marcas.Nombre ?? "").ToLower().Contains(texto))
                    );
                }

                var premios = query
                    .OrderByDescending(p => p.Id)
                    .Select(p => new PremioDTO
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        TerminosCondiciones = p.TerminosCondiciones,
                        Stock = p.Stock,
                        StockActual = p.StockActual,
                        Puntos = p.Puntos,
                        Fecha = p.Fecha,
                        Activo = p.Activo,

                        CategoriaId = p.Categoria != null ? p.Categoria.Id : 0,
                        CategoriaNombre = p.Categoria != null ? p.Categoria.Nombre : null,

                        FechaVencimiento = p.FechaVencimiento,
                        DiasDeVencimiento = p.DiasDeVencimiento,

                        Marca = p.Marcas
                    })
                    .ToList();

                return Ok(new PremiosResponseDTO
                {
                    Success = true,
                    Mensaje = "Listado de premios obtenido correctamente.",
                    Premios = premios
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PremiosResponseDTO
                {
                    Success = false,
                    Mensaje = "Hubo un error al obtener el listado de premios: " + ex.Message,
                    Premios = new List<PremioDTO>()
                });
            }
        }

        // GET: endpoint/premios/5
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            try
            {
                var premio = _context.Premios
                    .Where(p => p.Id == id)
                    .Select(p => new PremioDTO
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        TerminosCondiciones = p.TerminosCondiciones,
                        Stock = p.Stock,
                        StockActual = p.StockActual,
                        Puntos = p.Puntos,
                        Fecha = p.Fecha,
                        Activo = p.Activo,

                        CategoriaId = p.Categoria != null ? p.Categoria.Id : 0,
                        CategoriaNombre = p.Categoria != null ? p.Categoria.Nombre : null,

                        FechaVencimiento = p.FechaVencimiento,
                        DiasDeVencimiento = p.DiasDeVencimiento,

                        Marca = p.Marcas
                    })
                    .FirstOrDefault();

                if (premio == null)
                {
                    return NotFound(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "El premio no fue encontrado.",
                        Premio = null
                    });
                }

                return Ok(new PremioResponseDTO
                {
                    Success = true,
                    Mensaje = "Premio obtenido correctamente.",
                    Premio = premio
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PremioResponseDTO
                {
                    Success = false,
                    Mensaje = "Hubo un error al obtener el premio: " + ex.Message,
                    Premio = null
                });
            }
        }

        // POST: endpoint/premios
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PremioCreateDTO dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "Los datos del premio son obligatorios.",
                        Premio = null
                    });
                }

                var categoria = _context.Categorias
                    .Where(x => x.Id == dto.CategoriaId)
                    .FirstOrDefault();

                if (categoria == null)
                {
                    return BadRequest(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "La categoría indicada no existe.",
                        Premio = null
                    });
                }

                var marca = _context.Marcas
                    .Where(x => x.Id == dto.MarcaId)
                    .FirstOrDefault();

                if (marca == null)
                {
                    return BadRequest(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "La marca indicada no existe.",
                        Premio = null
                    });
                }

                var premio = new Premios
                {
                    Nombre = dto.Nombre,
                    Descripcion = dto.Descripcion,
                    TerminosCondiciones = dto.TerminosCondiciones,
                    Stock = dto.Stock,
                    StockActual = dto.Stock,
                    Puntos = dto.Puntos,
                    Categoria = categoria,
                    Marcas = marca,
                    FechaVencimiento = dto.FechaVencimiento,
                    DiasDeVencimiento = dto.DiasDeVencimiento,
                    Fecha = DateTime.Now,
                    Activo = true
                };

                await _context.Premios.AddAsync(premio);
                await _context.SaveChangesAsync();

                var response = new PremioDTO
                {
                    Id = premio.Id,
                    Nombre = premio.Nombre,
                    Descripcion = premio.Descripcion,
                    TerminosCondiciones = premio.TerminosCondiciones,
                    Stock = premio.Stock,
                    StockActual = premio.StockActual,
                    Puntos = premio.Puntos,
                    Fecha = premio.Fecha,
                    Activo = premio.Activo,

                    CategoriaId = categoria.Id,
                    CategoriaNombre = categoria.Nombre,

                    FechaVencimiento = premio.FechaVencimiento,
                    DiasDeVencimiento = premio.DiasDeVencimiento,

                    Marca = marca
                };

                return Ok(new PremioResponseDTO
                {
                    Success = true,
                    Mensaje = "Se creó correctamente el Premio " + premio.Nombre + ".",
                    Premio = response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PremioResponseDTO
                {
                    Success = false,
                    Mensaje = "Hubo un error al crear el Premio: " + ex.Message,
                    Premio = null
                });
            }
        }

        // PUT: endpoint/premios/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PremioUpdateDTO dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "Los datos del premio son obligatorios.",
                        Premio = null
                    });
                }

                var premioDB = _context.Premios
                    .Where(x => x.Id == id)
                    .FirstOrDefault();

                if (premioDB == null)
                {
                    return NotFound(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "El premio no fue encontrado.",
                        Premio = null
                    });
                }

                var categoria = _context.Categorias
                    .Where(x => x.Id == dto.CategoriaId)
                    .FirstOrDefault();

                if (categoria == null)
                {
                    return BadRequest(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "La categoría indicada no existe.",
                        Premio = null
                    });
                }

                var marca = _context.Marcas
                    .Where(x => x.Id == dto.MarcaId)
                    .FirstOrDefault();

                if (marca == null)
                {
                    return BadRequest(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "La marca indicada no existe.",
                        Premio = null
                    });
                }

                premioDB.Categoria = categoria;
                premioDB.Marcas = marca;
                premioDB.Nombre = dto.Nombre;
                premioDB.Descripcion = dto.Descripcion;
                premioDB.Stock = dto.Stock;
                premioDB.StockActual = dto.StockActual == 0 ? dto.Stock : dto.StockActual;
                premioDB.Puntos = dto.Puntos;
                premioDB.TerminosCondiciones = dto.TerminosCondiciones;
                premioDB.FechaVencimiento = dto.FechaVencimiento;
                premioDB.DiasDeVencimiento = dto.DiasDeVencimiento;

                _context.Premios.Update(premioDB);
                await _context.SaveChangesAsync();

                var response = new PremioDTO
                {
                    Id = premioDB.Id,
                    Nombre = premioDB.Nombre,
                    Descripcion = premioDB.Descripcion,
                    TerminosCondiciones = premioDB.TerminosCondiciones,
                    Stock = premioDB.Stock,
                    StockActual = premioDB.StockActual,
                    Puntos = premioDB.Puntos,
                    Fecha = premioDB.Fecha,
                    Activo = premioDB.Activo,

                    CategoriaId = categoria.Id,
                    CategoriaNombre = categoria.Nombre,

                    FechaVencimiento = premioDB.FechaVencimiento,
                    DiasDeVencimiento = premioDB.DiasDeVencimiento,

                    Marca = marca
                };

                return Ok(new PremioResponseDTO
                {
                    Success = true,
                    Mensaje = "Se editó correctamente el Premio " + premioDB.Nombre + ".",
                    Premio = response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PremioResponseDTO
                {
                    Success = false,
                    Mensaje = "Hubo un error al editar el Premio: " + ex.Message,
                    Premio = null
                });
            }
        }

        // DELETE: endpoint/premios/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (_context.HistorialCanje.Any(x => x.Premio.Id == id))
                {
                    return BadRequest(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "No se puede eliminar un Cupón Canjeado.",
                        Premio = null
                    });
                }

                var premio = _context.Premios
                    .Where(s => s.Id == id)
                    .FirstOrDefault();

                if (premio == null)
                {
                    return NotFound(new PremioResponseDTO
                    {
                        Success = false,
                        Mensaje = "El premio no fue encontrado.",
                        Premio = null
                    });
                }

                var fotos = _context.FotosPremios
                    .Where(x => x.Premio.Id == id)
                    .ToList();

                _context.FotosPremios.RemoveRange(fotos);
                _context.Premios.Remove(premio);

                await _context.SaveChangesAsync();

                return Ok(new PremioResponseDTO
                {
                    Success = true,
                    Mensaje = "Se eliminó correctamente el Cupón.",
                    Premio = null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PremioResponseDTO
                {
                    Success = false,
                    Mensaje = "Hubo un error al eliminar el Cupón: " + ex.Message,
                    Premio = null
                });
            }
        }

        // GET: endpoint/premios/5/imagenes
        [HttpGet("{id}/imagenes")]
        public IActionResult GetImagenes(int id)
        {
            try
            {
                var premio = _context.Premios
                    .Where(p => p.Id == id)
                    .FirstOrDefault();

                if (premio == null)
                {
                    return NotFound(new PremioImagenResponseDTO
                    {
                        Success = false,
                        Mensaje = "El premio no fue encontrado.",
                        Imagenes = null
                    });
                }

                var imagenes = _context.FotosPremios
                    .Where(fp => fp.Premio.Id == id)
                    .OrderBy(fp => fp.Orden)
                    .Select(fp => fp.Foto)
                    .ToList();

                return Ok(new PremioImagenResponseDTO
                {
                    Success = true,
                    Mensaje = "Imágenes obtenidas correctamente.",
                    Imagenes = new PremiosImagenDTO
                    {
                        Id = id,
                        Imagenes = imagenes
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PremioImagenResponseDTO
                {
                    Success = false,
                    Mensaje = "Hubo un error al obtener las imágenes del premio: " + ex.Message,
                    Imagenes = null
                });
            }
        }

        // POST: endpoint/premios/5/imagen
        [HttpPost("{id}/imagen")]
        public async Task<IActionResult> CambiarImagen(int id, IFormFile file)
        {
            try
            {
                var premio = await _context.Premios.FindAsync(id);

                if (premio == null)
                {
                    return NotFound(new PremioImagenResponseDTO
                    {
                        Success = false,
                        Mensaje = "El premio no fue encontrado.",
                        Imagenes = null
                    });
                }

                if (file == null || file.Length <= 0)
                {
                    return BadRequest(new PremioImagenResponseDTO
                    {
                        Success = false,
                        Mensaje = "Debe enviar una imagen.",
                        Imagenes = null
                    });
                }

                var fotoExistente = _context.FotosPremios
                    .FirstOrDefault(fp => fp.Premio.Id == id);

                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);

                    string base64Foto = Convert.ToBase64String(memoryStream.ToArray());

                    if (fotoExistente != null)
                    {
                        fotoExistente.Foto = base64Foto;
                        fotoExistente.Fecha = DateTime.Now;

                        _context.FotosPremios.Update(fotoExistente);
                    }
                    else
                    {
                        FotosPremios nuevaFoto = new FotosPremios
                        {
                            Premio = premio,
                            Foto = base64Foto,
                            Orden = 1,
                            Fecha = DateTime.Now
                        };

                        _context.FotosPremios.Add(nuevaFoto);
                    }

                    await _context.SaveChangesAsync();
                }

                var imagenes = _context.FotosPremios
                    .Where(fp => fp.Premio.Id == id)
                    .OrderBy(fp => fp.Orden)
                    .Select(fp => fp.Foto)
                    .ToList();

                return Ok(new PremioImagenResponseDTO
                {
                    Success = true,
                    Mensaje = "Se actualizó la imagen correctamente.",
                    Imagenes = new PremiosImagenDTO
                    {
                        Id = id,
                        Imagenes = imagenes
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PremioImagenResponseDTO
                {
                    Success = false,
                    Mensaje = "Hubo un error al cargar la imagen: " + ex.Message,
                    Imagenes = null
                });
            }
        }
    }
}