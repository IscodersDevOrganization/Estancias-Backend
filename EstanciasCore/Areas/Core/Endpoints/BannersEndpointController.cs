using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Areas.Core.Endpoints
{
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [ApiController]
    [Route("endpoint/banners")]
    public class BannersEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;
        private readonly IHostingEnvironment _env;

        public BannersEndpointController(EstanciasContext context, IHostingEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: endpoint/banners/listar?pagina=1&cantidad=10&buscar=promo
        [HttpGet("listar")]
        public async Task<IActionResult> Listar(
            [FromQuery] int pagina = 1,
            [FromQuery] int cantidad = 10,
            [FromQuery] string buscar = "")
        {
            try
            {
                if (pagina < 1)
                    pagina = 1;

                if (cantidad < 1)
                    cantidad = 10;

                buscar = buscar ?? "";

                var email = User.Identity?.Name;

                var usuario = await _context.Usuarios
                    .Include(x => x.Clientes)
                        .ThenInclude(x => x.Empresa)
                    .FirstOrDefaultAsync(x => x.Email == email);

                var query = _context.Banners
                    .Include(x => x.Marcas)
                    .AsQueryable();

                if (usuario == null || usuario.Clientes?.Empresa == null)
                {
                    query = query.Where(x => x.Empresa == null);
                }
                else
                {
                    int empresaId = usuario.Clientes.Empresa.Id;
                    query = query.Where(x => x.Empresa.Id == empresaId);
                }

                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(x =>
                        (x.Titulo ?? "").Contains(buscar) ||
                        (x.Texto ?? "").Contains(buscar));
                }

                var totalRegistros = await query.CountAsync();

                var banners = await query
                    .OrderBy(x => x.Orden)
                    .ThenBy(x => x.Marcas != null ? x.Marcas.Nombre : "")
                    .Skip((pagina - 1) * cantidad)
                    .Take(cantidad)
                    .Select(x => new BannerListadoDTO
                    {
                        Id = x.Id,
                        Titulo = x.Titulo ?? "",
                        Subtitulo = x.Subtitulo ?? "",
                        Texto = x.Texto ?? "",
                        TextoBoton = x.TextoBoton ?? "",
                        Fecha = x.Fecha,
                        FechaDesde = x.FechaDesde,
                        FechaHasta = x.FechaHasta,
                        Publico = x.Publico,
                        Link = x.Link ?? "",
                        LinkExterno = x.LinkExterno,
                        EsCarrousel = x.EsCarrousel,
                        BannerFijo = x.BannerFijo,
                        Vencimiento = x.Vencimiento,
                        EsVideo = x.EsVideo,
                        Video = x.Video ?? "",
                        Foto = x.Foto ?? "",
                        Orden = x.Orden,
                        Marca = x.Marcas == null ? null : new MarcaBannerDTO
                        {
                            Id = x.Marcas.Id,
                            Nombre = x.Marcas.Nombre ?? ""
                        }
                    })
                    .ToListAsync();

                var totalPaginas = totalRegistros == 0
                    ? 1
                    : (int)Math.Ceiling(totalRegistros / (decimal)cantidad);

                return Ok(new BannerListadoResponseDTO
                {
                    Status = 200,
                    Mensaje = "Listado de Banners obtenido correctamente.",
                    Data = new BannerListadoDataDTO
                    {
                        TotalRegistros = totalRegistros,
                        PaginaActual = pagina,
                        CantidadPorPagina = cantidad,
                        TotalPaginas = totalPaginas,
                        Banners = banners
                    }
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "Hubo un error al obtener el listado de Banners. Intentelo nuevamente mas tarde."
                });
            }
        }

        // GET: endpoint/banners/obtener/5
        [HttpGet("obtener/{id}")]
        public async Task<IActionResult> Obtener(int id)
        {
            try
            {
                var banner = await _context.Banners
                    .Include(x => x.Marcas)
                    .Where(x => x.Id == id)
                    .Select(x => new BannerListadoDTO
                    {
                        Id = x.Id,
                        Titulo = x.Titulo ?? "",
                        Subtitulo = x.Subtitulo ?? "",
                        Texto = x.Texto ?? "",
                        TextoBoton = x.TextoBoton ?? "",
                        Fecha = x.Fecha,
                        FechaDesde = x.FechaDesde,
                        FechaHasta = x.FechaHasta,
                        Publico = x.Publico,
                        Link = x.Link ?? "",
                        LinkExterno = x.LinkExterno,
                        EsCarrousel = x.EsCarrousel,
                        BannerFijo = x.BannerFijo,
                        Vencimiento = x.Vencimiento,
                        EsVideo = x.EsVideo,
                        Video = x.Video ?? "",
                        Foto = x.Foto ?? "",
                        Orden = x.Orden,
                        Marca = x.Marcas == null ? null : new MarcaBannerDTO
                        {
                            Id = x.Marcas.Id,
                            Nombre = x.Marcas.Nombre ?? ""
                        }
                    })
                    .FirstOrDefaultAsync();

                if (banner == null)
                {
                    return NotFound(new BannerResponseDTO
                    {
                        Status = 404,
                        Mensaje = "No se encontró el Banner solicitado."
                    });
                }

                return Ok(new BannerItemResponseDTO
                {
                    Status = 200,
                    Mensaje = "Banner obtenido correctamente.",
                    Banner = banner
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "Hubo un error al obtener el Banner. Intentelo nuevamente mas tarde."
                });
            }
        }

        // GET: endpoint/banners/marcas
        [HttpGet("marcas")]
        public async Task<IActionResult> Marcas()
        {
            try
            {
                var marcas = await _context.Marcas
                    .Where(x => x.Activo)
                    .OrderBy(x => x.Orden)
                    .Select(x => new MarcaBannerDTO
                    {
                        Id = x.Id,
                        Nombre = x.Nombre ?? ""
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = 200,
                    Mensaje = "Listado de marcas obtenido correctamente.",
                    Data = marcas
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "Hubo un error al obtener las Marcas. Intentelo nuevamente mas tarde."
                });
            }
        }

        // POST: endpoint/banners/crear
        [HttpPost("crear")]
        public async Task<IActionResult> Crear([FromBody] BannerCrearDTO dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new BannerResponseDTO
                    {
                        Status = 400,
                        Mensaje = "Los datos del Banner son obligatorios."
                    });
                }

                var email = User.Identity?.Name;

                var usuario = await _context.Usuarios
                    .Include(x => x.Clientes)
                        .ThenInclude(x => x.Empresa)
                    .FirstOrDefaultAsync(x => x.Email == email);

                var banner = new Banners();

                banner.Titulo = dto.Titulo;
                banner.Subtitulo = dto.Subtitulo;
                banner.Texto = dto.Texto;
                banner.TextoBoton = dto.TextoBoton;

                banner.Fecha = dto.Fecha.HasValue ? dto.Fecha.Value : DateTime.Now;
                banner.Publico = dto.Publico;
                banner.Link = dto.Link;
                banner.FechaDesde = dto.FechaDesde.HasValue ? dto.FechaDesde.Value : DateTime.Now;
                banner.FechaHasta = dto.FechaHasta;
                banner.LinkExterno = dto.LinkExterno;
                banner.EsCarrousel = dto.EsCarrousel;

                /*
                 * El original NO setea Orden en Create explícitamente.
                 * Si querés respetar 100%, no lo seteamos acá.
                 * El orden se modifica con ModificarOrden.
                 */

                if (usuario != null && usuario.Clientes != null)
                {
                    banner.Empresa = usuario.Clientes.Empresa;
                }

                banner.Vencimiento = false;

                if (dto.BannersFijo == 1)
                {
                    banner.BannerFijo = true;
                }
                else
                {
                    banner.BannerFijo = false;
                }

                if (dto.TieneFechaVencimiento == "on")
                {
                    banner.Vencimiento = true;
                }

                if (dto.MarcasId.HasValue && dto.MarcasId.Value > 0)
                {
                    banner.Marcas = await _context.Marcas.FindAsync(dto.MarcasId.Value);
                }

                banner.EsVideo = false;

                _context.Banners.Add(banner);
                _context.SaveChanges();

                return Ok(new BannerResponseDTO
                {
                    Status = 200,
                    Mensaje = "Se registró correctamente el Banner."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "Hubo un error al registrar el Banner."
                });
            }
        }

        // POST: endpoint/banners/editar/5
        [HttpPost("editar/{id}")]
        public async Task<IActionResult> Editar(int id, [FromBody] BannerEditarDTO dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new BannerResponseDTO
                    {
                        Status = 400,
                        Mensaje = "Los datos del Banner son obligatorios."
                    });
                }

                if (id != dto.Id)
                {
                    return BadRequest(new BannerResponseDTO
                    {
                        Status = 400,
                        Mensaje = "El Id enviado por ruta no coincide con el Id del Banner."
                    });
                }

                Banners d = await _context.Banners
                    .Include(x => x.Marcas)
                    .Where(s => s.Id == dto.Id)
                    .FirstOrDefaultAsync();

                if (d == null)
                {
                    return NotFound(new BannerResponseDTO
                    {
                        Status = 404,
                        Mensaje = "No se encontró el Banner solicitado."
                    });
                }

                /*
                 * Respeta la lógica original del Update:
                 * pisa campos directamente y no modifica Orden.
                 */
                d.Titulo = dto.Titulo;
                d.Subtitulo = dto.Subtitulo == null ? " " : dto.Subtitulo;
                d.Texto = dto.Texto == null ? " " : dto.Texto;
                d.TextoBoton = dto.TextoBoton == null ? " " : dto.TextoBoton;

                d.Fecha = dto.Fecha.HasValue ? dto.Fecha.Value : d.Fecha;
                d.Publico = dto.Publico;
                d.Link = dto.Link;
                d.FechaDesde = dto.FechaDesde.HasValue ? dto.FechaDesde.Value : d.FechaDesde;
                d.LinkExterno = dto.LinkExterno;
                d.EsCarrousel = dto.EsCarrousel;

                if (dto.BannerFijo == 1)
                {
                    d.BannerFijo = true;
                }
                else
                {
                    d.BannerFijo = false;
                }

                if (dto.TieneFechaVencimiento == "on")
                {
                    d.FechaHasta = dto.FechaHasta;
                    d.Vencimiento = true;
                }
                else
                {
                    d.FechaHasta = null;
                    d.Vencimiento = false;
                }

                if (dto.MarcasId.HasValue && dto.MarcasId.Value > 0)
                {
                    d.Marcas = await _context.Marcas.FindAsync(dto.MarcasId.Value);
                }
                else
                {
                    d.Marcas = null;
                }

                await _context.SaveChangesAsync();

                return Ok(new BannerResponseDTO
                {
                    Status = 200,
                    Mensaje = "Se modificó correctamente el Banner."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "Hubo un error al modificar el Banner."
                });
            }
        }

        // DELETE: endpoint/banners/borrar/5
        [HttpDelete("borrar/{id}")]
        public async Task<IActionResult> Borrar(int id)
        {
            try
            {
                Banners banner = await _context.Banners
                    .Where(s => s.Id == id)
                    .FirstOrDefaultAsync();

                if (banner == null)
                {
                    return NotFound(new BannerResponseDTO
                    {
                        Status = 404,
                        Mensaje = "No se encontró el Banner solicitado."
                    });
                }

                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();

                return Ok(new BannerResponseDTO
                {
                    Status = 200,
                    Mensaje = "Se eliminó correctamente el Banner."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "Hubo un error al eliminar el Banner."
                });
            }
        }

        // POST: endpoint/banners/cambiar-imagen/5
        [HttpPost("cambiar-imagen/{id}")]
        public async Task<IActionResult> CambiarImagen(int id, IFormFile file)
        {
            try
            {
                var banner = await _context.Banners.FindAsync(id);

                /*
                 * En el original esto está después de usar banner.EsVideo,
                 * pero acá se valida antes para evitar NullReference.
                 */
                if (banner == null)
                {
                    return NotFound(new BannerResponseDTO
                    {
                        Status = 404,
                        Mensaje = "No se encontró el Banner solicitado."
                    });
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new BannerResponseDTO
                    {
                        Status = 400,
                        Mensaje = "Debe enviar una imagen."
                    });
                }

                if (banner.EsVideo && banner.Video != null)
                {
                    Uri uri = new Uri(banner.Video);
                    string nombreArchivo = Path.GetFileName(uri.LocalPath);

                    try
                    {
                        string uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
                        string rutaArchivo = Path.Combine(uploadsPath, nombreArchivo);
                        System.IO.File.Delete(rutaArchivo);
                    }
                    catch (Exception)
                    {
                    }
                }

                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);

                    banner.EsVideo = false;
                    banner.Video = null;
                    banner.Foto = Convert.ToBase64String(memoryStream.ToArray());

                    await _context.SaveChangesAsync();
                }

                return Ok(new BannerResponseDTO
                {
                    Status = 200,
                    Mensaje = "Se cambió correctamente la imagen del Banner."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "Hubo un error al cambiar la imagen del Banner."
                });
            }
        }

        // POST: endpoint/banners/notificacion/5
        [HttpPost("notificacion/{id}")]
        public IActionResult Notificacion(int id)
        {
            try
            {

                var usuario = _context.Usuarios
                    .Include(x => x.Clientes)
                        .ThenInclude(x => x.Empresa)
                    .FirstOrDefault();

                if (usuario != null && usuario.Clientes != null && usuario.Clientes.Empresa != null)
                {
                    var listaPush = _context.Clientes
                        .Where(x => x.Empresa.Id == usuario.Clientes.Empresa.Id);

                    var promocion = _context.Banners.Find(id);

                    return Ok(new BannerResponseDTO
                    {
                        Status = 200,
                        Mensaje = listaPush.Count().ToString() + " Notificaciones Enviadas!"
                    });
                }

                return BadRequest(new BannerResponseDTO
                {
                    Status = 400,
                    Mensaje = "No se pudieron enviar las notificaciones."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "No se pudieron enviar las notificaciones."
                });
            }
        }

        // POST: endpoint/banners/modificar-orden
        [HttpPost("modificar-orden")]
        public async Task<IActionResult> ModificarOrden([FromBody] BannerOrdenDTO dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new BannerResponseDTO
                    {
                        Status = 400,
                        Mensaje = "Los datos son obligatorios."
                    });
                }

                Banners banner = await _context.Banners
                    .Where(x => x.Id == dto.Id)
                    .FirstOrDefaultAsync();

                /*
                 * El original no valida null y caería al catch.
                 * Acá respondemos claro sin romper.
                 */
                if (banner == null)
                {
                    return NotFound(new BannerResponseDTO
                    {
                        Status = 404,
                        Mensaje = "No se encontró el Banner solicitado."
                    });
                }

                banner.Orden = dto.Orden;

                _context.Banners.Update(banner);
                await _context.SaveChangesAsync();

                return Ok(new BannerResponseDTO
                {
                    Status = 200,
                    Mensaje = "Se modificó el orden del Banner."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "No se pudieron modificar el Banner."
                });
            }
        }

        // POST: endpoint/banners/cambiar-video/5
        [HttpPost("cambiar-video/{id}")]
        public async Task<IActionResult> CambiarVideo(int id, IFormFile file)
        {
            try
            {
                var banner = _context.Banners.Find(id);

                if (banner == null)
                {
                    return NotFound(new BannerResponseDTO
                    {
                        Status = 404,
                        Mensaje = "No se encontró el Banner solicitado."
                    });
                }

                if (banner.EsVideo && banner.Video != null)
                {
                    Uri uri = new Uri(banner.Video);
                    string nombreArchivo = Path.GetFileName(uri.LocalPath);

                    try
                    {
                        string uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
                        string rutaArchivo = Path.Combine(uploadsPath, nombreArchivo);
                        System.IO.File.Delete(rutaArchivo);
                    }
                    catch (Exception)
                    {
                    }
                }

                if (file != null && file.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    Directory.CreateDirectory(uploadsFolder);

                    string cadenaSinEspacios = file.FileName.Replace(" ", "_");
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + cadenaSinEspacios;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    var urlBase = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";

                    banner.Video = Url.Content(urlBase + "/uploads/" + uniqueFileName);
                    banner.Foto = null;
                    banner.EsVideo = true;

                    _context.Update(banner);
                    await _context.SaveChangesAsync();
                }

                return Ok(new BannerResponseDTO
                {
                    Status = 200,
                    Mensaje = "Se cargó correctamente el video."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new BannerResponseDTO
                {
                    Status = 500,
                    Mensaje = "Hubo un error al cargar el video."
                });
            }
        }
    }
}
