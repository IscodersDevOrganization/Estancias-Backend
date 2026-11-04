using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/relacion-puntos")]
    [ApiController]
    public class RelacionPuntosEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;

        public RelacionPuntosEndpointController(EstanciasContext context)
        {
            _context = context;
        }

        // GET: endpoint/relacion-puntos/listado?page=1&pageSize=10&buscar=1500
        [HttpGet("listado")]
        public async Task<IActionResult> GetAll(
            int page = 1,
            int pageSize = 10,
            string buscar = null)
        {
            try
            {
                if (page < 1)
                    page = 1;

                if (pageSize < 1)
                    pageSize = 10;

                var query = _context.RelacionPuntos.AsQueryable();

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    var texto = buscar.Trim();

                    decimal montoBuscado;
                    long puntosBuscados;

                    bool esMonto = decimal.TryParse(texto, out montoBuscado);
                    bool esPuntos = long.TryParse(texto, out puntosBuscados);

                    query = query.Where(r =>
                        (esMonto && r.Monto == montoBuscado) ||
                        (esPuntos && r.Puntos == puntosBuscados)
                    );
                }

                var totalRegistros = await query.CountAsync();

                if (totalRegistros < 1)
                    totalRegistros = 0;

                var registros = await query
                    .OrderByDescending(r => r.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new
                    {
                        r.Id,
                        r.Monto,
                        r.Puntos,
                        r.Fecha,
                        r.Activo,
                        r.Imagen
                    })
                    .ToListAsync();

                // La conversión se hace en memoria para no depender de su traducción SQL.
                var data = registros.Select(r => new RelacionPuntosDTO
                {
                    Id = r.Id,
                    Monto = r.Monto,
                    Puntos = r.Puntos,
                    Fecha = r.Fecha,
                    Activo = r.Activo,
                    Imagen = r.Imagen != null ? Convert.ToBase64String(r.Imagen) : null
                }).ToList();

                var totalPaginas = (int)Math.Ceiling(totalRegistros / (double)pageSize);

                var response = new RelacionPuntosListadoDTO
                {
                    TotalRegistros = totalRegistros,
                    PaginaActual = page,
                    CantidadPorPagina = pageSize,
                    TotalPaginas = totalPaginas,
                    Registros = data
                };

                return Ok(response);
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    mensaje = "Hubo un error al obtener el listado de Relación Puntos."
                });
            }
        }

        // GET: endpoint/relacion-puntos/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var registro = await _context.RelacionPuntos
                    .Where(r => r.Id == id)
                    .Select(r => new
                    {
                        r.Id,
                        r.Monto,
                        r.Puntos,
                        r.Fecha,
                        r.Activo,
                        r.Imagen
                    })
                    .FirstOrDefaultAsync();

                if (registro == null)
                {
                    return NotFound(new
                    {
                        mensaje = "No se encontró el Registro."
                    });
                }

                var relacion = new RelacionPuntosDTO
                {
                    Id = registro.Id,
                    Monto = registro.Monto,
                    Puntos = registro.Puntos,
                    Fecha = registro.Fecha,
                    Activo = registro.Activo,
                    Imagen = registro.Imagen != null ? Convert.ToBase64String(registro.Imagen) : null
                };

                return Ok(relacion);
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    mensaje = "Hubo un error al obtener el Registro."
                });
            }
        }

        // POST: endpoint/relacion-puntos/create
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] RelacionPuntosDTO dto)
        {
            if (dto == null)
            {
                return BadRequest(new
                {
                    mensaje = "Los datos enviados son inválidos."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var relacion = new RelacionPuntos
                {
                    Monto = dto.Monto,
                    Puntos = dto.Puntos,
                    Fecha = dto.Fecha,
                    Activo = dto.Activo
                };

                await _context.RelacionPuntos.AddAsync(relacion);
                await _context.SaveChangesAsync();

                dto.Id = relacion.Id;

                return Ok(new
                {
                    mensaje = "Se creó correctamente el Registro.",
                    data = dto
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    mensaje = "Hubo un error al crear el registro. Intentelo nuevamente mas tarde."
                });
            }
        }

        // PUT: endpoint/relacion-puntos/update/5
        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RelacionPuntosDTO dto)
        {
            if (dto == null)
            {
                return BadRequest(new
                {
                    mensaje = "Los datos enviados son inválidos."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var relacion = await _context.RelacionPuntos.FindAsync(id);

                if (relacion == null)
                {
                    return NotFound(new
                    {
                        mensaje = "No se encontró el Registro."
                    });
                }

                relacion.Monto = dto.Monto;
                relacion.Puntos = dto.Puntos;
                relacion.Fecha = dto.Fecha;
                relacion.Activo = dto.Activo;

                _context.RelacionPuntos.Update(relacion);
                await _context.SaveChangesAsync();

                dto.Id = relacion.Id;

                return Ok(new
                {
                    mensaje = "Se editó correctamente el Registro.",
                    data = dto
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    mensaje = "Hubo un error al editar el Registro. Intentelo nuevamente mas tarde."
                });
            }
        }

        // POST: endpoint/relacion-puntos/5/imagen
        [HttpPost("{id}/imagen")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubirImagen(int id, IFormFile FotoRelacion)
        {
            try
            {
                var relacionEdit = await _context.RelacionPuntos.FindAsync(id);

                if (relacionEdit == null)
                {
                    return NotFound(new
                    {
                        mensaje = "No se encontró el registro para cargar la imagen."
                    });
                }

                if (FotoRelacion != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await FotoRelacion.CopyToAsync(memoryStream);
                        relacionEdit.Imagen = memoryStream.ToArray();
                    }
                }

                _context.RelacionPuntos.Update(relacionEdit);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Se cargó correctamente la Imagen."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    mensaje = "Hubo un error al cargar la Imagen. Inténtelo nuevamente más tarde."
                });
            }
        }

        // DELETE: endpoint/relacion-puntos/delete/5
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var relacion = await _context.RelacionPuntos
                    .Where(r => r.Id == id)
                    .FirstOrDefaultAsync();

                if (relacion == null)
                {
                    return NotFound(new
                    {
                        mensaje = "No se encontró el Registro."
                    });
                }

                _context.RelacionPuntos.Remove(relacion);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Se eliminó correctamente el Registro."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    mensaje = "Hubo un error al eliminar el Registro."
                });
            }
        }
    }
}
