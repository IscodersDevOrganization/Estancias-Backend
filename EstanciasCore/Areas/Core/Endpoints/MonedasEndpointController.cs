using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/monedas")]
    [ApiController]
    public class MonedasEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;

        public MonedasEndpointController(EstanciasContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var monedas = await _context.Monedas
                .Select(m => new MonedasDTO
                {
                    Id = m.Id,
                    Nombre = m.Nombre
                })
                .ToListAsync();

            return Ok(new
            {
                ok = true,
                data = monedas
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var moneda = await _context.Monedas
                .Where(m => m.Id == id)
                .Select(m => new MonedasDTO
                {
                    Id = m.Id,
                    Nombre = m.Nombre
                })
                .FirstOrDefaultAsync();

            if (moneda == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la moneda."
                });
            }

            return Ok(new
            {
                ok = true,
                data = moneda
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MonedaCreateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Nombre))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "El nombre de la moneda es obligatorio."
                });
            }

            var moneda = new Monedas
            {
                Nombre = request.Nombre
            };

            await _context.Monedas.AddAsync(moneda);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Moneda creada correctamente.",
                data = new MonedasDTO
                {
                    Id = moneda.Id,
                    Nombre = moneda.Nombre
                }
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] MonedaUpdateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Nombre))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "El nombre de la moneda es obligatorio."
                });
            }

            var moneda = await _context.Monedas
                .FirstOrDefaultAsync(m => m.Id == id);

            if (moneda == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la moneda."
                });
            }

            moneda.Nombre = request.Nombre;

            _context.Monedas.Update(moneda);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Moneda actualizada correctamente.",
                data = new MonedasDTO
                {
                    Id = moneda.Id,
                    Nombre = moneda.Nombre
                }
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var moneda = await _context.Monedas
                .FirstOrDefaultAsync(m => m.Id == id);

            if (moneda == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontró la moneda."
                });
            }

            _context.Monedas.Remove(moneda);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Moneda eliminada correctamente."
            });
        }
    }
}