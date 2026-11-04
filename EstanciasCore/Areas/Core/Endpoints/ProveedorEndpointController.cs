using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using DAL.Models.Core;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EstanciasCore.Areas.Core.Endpoints
{
    [Area("Core")]
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [ApiController]
    [Route("endpoint/proveedor")]
    public class ProveedorEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;

        public ProveedorEndpointController(EstanciasContext context)
        {
            _context = context;
        }

        // GET: endpoint/proveedor
        [HttpGet]
        public IActionResult GetAll(string buscar = "", int page = 1, int pageSize = 10)
        {
            try
            {
                if (page < 1)
                {
                    page = 1;
                }

                if (pageSize < 1)
                {
                    pageSize = 10;
                }

                var query = _context.Proveedores
                    .Where(x => x.Activo)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    string texto = buscar.Trim().ToLower();

                    query = query.Where(p =>
                        (p.Nombre != null && p.Nombre.ToLower().Contains(texto)) ||
                        p.CUIT.ToString().Contains(texto) ||
                        (p.RazonSocial != null && p.RazonSocial.ToLower().Contains(texto)) ||
                        (p.Empresa != null && p.Empresa.RazonSocial != null && p.Empresa.RazonSocial.ToLower().Contains(texto))
                    );
                }

                int total = query.Count();

                List<ListProveedorDTO> proveedores = query
                    .OrderBy(p => p.Nombre)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new ListProveedorDTO
                    {
                        Id = p.Id,
                        NombreCompleto = p.Nombre != null ? p.Nombre.ToUpper() : "",
                        CUIT = p.CUIT.ToString(),
                        RazonSocial = p.RazonSocial,
                        Empresa = p.Empresa != null ? p.Empresa.RazonSocial : ""
                    })
                    .ToList();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Listado de proveedores obtenido correctamente.",
                    total = total,
                    page = page,
                    pageSize = pageSize,
                    data = proveedores
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al obtener el listado de proveedores."
                });
            }
        }

        // GET: endpoint/proveedor/create-data
        [HttpGet("create-data")]
        public IActionResult GetCreateData()
        {
            try
            {
                ProveedorDTO modelo = new ProveedorDTO();
                modelo = CreateProveedorDTO(modelo);

                return Ok(new
                {
                    status = 200,
                    mensaje = "Datos para crear proveedor obtenidos correctamente.",
                    data = modelo
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al obtener los datos para crear el proveedor."
                });
            }
        }

        // GET: endpoint/proveedor/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                Proveedor proveedor = await _context.Proveedores
                    .Include(p => p.Empresa)
                    .Include(p => p.Rubros)
                        .ThenInclude(r => r.Rubro)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                ProveedorDTO modelo = new ProveedorDTO();
                modelo = CreateProveedorDTO(modelo, proveedor);

                return Ok(new
                {
                    status = 200,
                    mensaje = "Proveedor obtenido correctamente.",
                    data = modelo
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al obtener el proveedor."
                });
            }
        }

        // POST: endpoint/proveedor
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProveedorDTO nuevoProveedor)
        {
            try
            {
                List<string> errores = ValidarProveedor(nuevoProveedor);

                if (errores.Count > 0)
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Hay errores de validación.",
                        errores = errores,
                        data = CreateProveedorDTO(nuevoProveedor)
                    });
                }

                Empresas empresa = await _context.Empresas.FindAsync(nuevoProveedor.EmpresaId);

                if (empresa == null)
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Debe seleccionar una Empresa válida."
                    });
                }

                Proveedor proveedor = new Proveedor
                {
                    Nombre = nuevoProveedor.Nombre,
                    CUIT = nuevoProveedor.CUIT,
                    RazonSocial = nuevoProveedor.RazonSocial,
                    Domicilio = nuevoProveedor.Domicilio,
                    Empresa = empresa,
                    Activo = true
                };

                await _context.Proveedores.AddAsync(proveedor);

                foreach (int r in nuevoProveedor.RubrosSeleccionados)
                {
                    Rubro rubro = await _context.Rubros.FindAsync(r);

                    if (rubro != null)
                    {
                        ProveedorRubro proveedorRubro = new ProveedorRubro
                        {
                            Proveedor = proveedor,
                            Rubro = rubro
                        };

                        await _context.ProveedorRubros.AddAsync(proveedorRubro);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Se cargó correctamente el Proveedor " + nuevoProveedor.Nombre + ".",
                    id = proveedor.Id
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al cargar el Proveedor. Intentelo nuevamente mas tarde."
                });
            }
        }

        // PUT: endpoint/proveedor/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProveedorDTO editProveedor)
        {
            try
            {
                editProveedor.ProveedorId = id;

                List<string> errores = ValidarProveedor(editProveedor);

                if (errores.Count > 0)
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Hay errores de validación.",
                        errores = errores,
                        data = CreateProveedorDTO(editProveedor)
                    });
                }

                Proveedor proveedor = await _context.Proveedores
                    .Include(p => p.Empresa)
                    .Include(p => p.Rubros)
                        .ThenInclude(r => r.Rubro)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                Empresas empresa = await _context.Empresas.FindAsync(editProveedor.EmpresaId);

                if (empresa == null)
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Debe seleccionar una Empresa válida."
                    });
                }

                List<ProveedorRubro> rubrosAEliminar = proveedor.Rubros
                    .Where(r => !editProveedor.RubrosSeleccionados.Contains(r.Rubro.Id))
                    .ToList();

                _context.ProveedorRubros.RemoveRange(rubrosAEliminar);

                List<int> rubrosActuales = proveedor.Rubros
                    .Select(r => r.Rubro.Id)
                    .ToList();

                foreach (int r in editProveedor.RubrosSeleccionados.Where(x => !rubrosActuales.Contains(x)))
                {
                    Rubro rubro = await _context.Rubros.FindAsync(r);

                    if (rubro != null)
                    {
                        ProveedorRubro proveedorRubro = new ProveedorRubro
                        {
                            Proveedor = proveedor,
                            Rubro = rubro
                        };

                        await _context.ProveedorRubros.AddAsync(proveedorRubro);
                    }
                }

                proveedor.Nombre = editProveedor.Nombre;
                proveedor.CUIT = editProveedor.CUIT;
                proveedor.RazonSocial = editProveedor.RazonSocial;
                proveedor.Domicilio = editProveedor.Domicilio;
                proveedor.Empresa = empresa;
                proveedor.Activo = true;

                _context.Proveedores.Update(proveedor);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Se modificó correctamente el Proveedor."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al modificar el Proveedor. Intentelo nuevamente mas tarde."
                });
            }
        }

        // DELETE: endpoint/proveedor/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                Proveedor proveedor = await _context.Proveedores.FindAsync(id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                proveedor.Activo = false;

                _context.Proveedores.Update(proveedor);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Se eliminó correctamente el Proveedor " + proveedor.Nombre + "."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al eliminar el Proveedor. Intentelo nuevamente mas tarde."
                });
            }
        }

        // GET: endpoint/proveedor/5/detalle
        [HttpGet("{id}/detalle")]
        public async Task<IActionResult> GetDetalleProveedor(int id)
        {
            try
            {
                Proveedor proveedor = await _context.Proveedores
                    .Include(p => p.Empresa)
                    .Include(p => p.Rubros)
                        .ThenInclude(r => r.Rubro)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                List<ProveedorProductoDTO> productos = ObtenerProductosProveedor(id);

                ProveedorDetalleDTO data = new ProveedorDetalleDTO
                {
                    Id = proveedor.Id,
                    Nombre = proveedor.Nombre,
                    CUIT = proveedor.CUIT.ToString(),
                    RazonSocial = proveedor.RazonSocial,
                    Domicilio = proveedor.Domicilio,
                    Empresa = proveedor.Empresa != null ? proveedor.Empresa.RazonSocial : "",
                    Rubros = proveedor.Rubros != null
                        ? proveedor.Rubros.Select(x => x.Rubro.Nombre).ToList()
                        : new List<string>(),
                    Productos = productos
                };

                return Ok(new
                {
                    status = 200,
                    mensaje = "Detalle del proveedor obtenido correctamente.",
                    data = data
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al acceder a la lista de Productos del Proveedor. Intentelo nuevamente mas tarde."
                });
            }
        }

        // GET: endpoint/proveedor/5/productos
        [HttpGet("{id}/productos")]
        public IActionResult GetProductosProveedor(int id, string buscar = "", int page = 1, int pageSize = 10)
        {
            try
            {
                if (page < 1)
                {
                    page = 1;
                }

                if (pageSize < 1)
                {
                    pageSize = 10;
                }

                Proveedor proveedor = _context.Proveedores.Find(id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                List<ProveedorProductoDTO> productos = ObtenerProductosProveedor(id);

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    string texto = buscar.Trim().ToLower();

                    productos = productos
                        .Where(p =>
                            (p.Producto != null && p.Producto.ToLower().Contains(texto)) ||
                            (p.Rubro != null && p.Rubro.ToLower().Contains(texto)) ||
                            (p.Detalle != null && p.Detalle.ToLower().Contains(texto)) ||
                            p.Precio.ToString().Contains(texto) ||
                            p.PrecioOferta.ToString().Contains(texto)
                        )
                        .ToList();
                }

                int total = productos.Count;

                productos = productos
                    .OrderBy(p => p.Producto)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Productos del proveedor obtenidos correctamente.",
                    total = total,
                    page = page,
                    pageSize = pageSize,
                    data = productos
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al obtener los productos del proveedor."
                });
            }
        }

        // GET: endpoint/proveedor/5/productos/create-data
        [HttpGet("{id}/productos/create-data")]
        public IActionResult GetCreateProductoData(int id)
        {
            try
            {
                Proveedor proveedor = _context.Proveedores
                    .Include(p => p.Rubros)
                        .ThenInclude(r => r.Rubro)
                    .FirstOrDefault(p => p.Id == id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                List<SelectListItem> rubros = proveedor.Rubros != null && proveedor.Rubros.Count > 0
                    ? proveedor.Rubros
                        .Where(x => x.Rubro != null && x.Rubro.Activo)
                        .Select(x => new SelectListItem
                        {
                            Text = x.Rubro.Nombre,
                            Value = x.Rubro.Id.ToString()
                        })
                        .ToList()
                    : _context.Rubros
                        .Where(x => x.Activo)
                        .Select(x => new SelectListItem
                        {
                            Text = x.Nombre,
                            Value = x.Id.ToString()
                        })
                        .ToList();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Datos para crear producto obtenidos correctamente.",
                    data = new
                    {
                        proveedorId = proveedor.Id,
                        proveedor = proveedor.Nombre,
                        rubros = rubros
                    }
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al obtener los datos para crear el producto."
                });
            }
        }

        // POST: endpoint/proveedor/5/productos
        [HttpPost("{id}/productos")]
        public async Task<IActionResult> CreateProductoProveedor(int id, [FromBody] ProveedorProductoCreateDTO nuevoProducto)
        {
            try
            {
                Proveedor proveedor = await _context.Proveedores.FindAsync(id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                List<string> errores = ValidarProducto(nuevoProducto);

                if (errores.Count > 0)
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Hay errores de validación.",
                        errores = errores
                    });
                }

                Rubro rubro = await _context.Rubros.FindAsync(nuevoProducto.RubroId);

                if (rubro == null)
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Debe seleccionar un Rubro válido."
                    });
                }

                Producto producto = new Producto
                {
                    Proveedor = proveedor,
                    Rubro = rubro,
                    Precio = nuevoProducto.Precio,
                    Financiable = nuevoProducto.Financiable,
                    Activo = true
                };

                SetStringProperty(producto, nuevoProducto.Producto, "Nombre", "Producto", "Descripcion", "DescripcionProducto", "Titulo");
                SetStringProperty(producto, nuevoProducto.Detalle, "Detalle", "Observacion", "Observaciones");
                SetDecimalProperty(producto, nuevoProducto.PrecioOferta, "PrecioOferta", "PrecioPromocion", "PrecioPromocional", "Oferta");

                await _context.Productos.AddAsync(producto);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Se cargó correctamente el Producto.",
                    id = producto.Id
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al cargar el Producto. Intentelo nuevamente mas tarde."
                });
            }
        }

        // GET: endpoint/proveedor/5/productos/10
        [HttpGet("{id}/productos/{productoId}")]
        public IActionResult GetProductoProveedorById(int id, int productoId)
        {
            try
            {
                Producto producto = _context.Productos
                    .Include(p => p.Rubro)
                    .Include(p => p.Proveedor)
                    .FirstOrDefault(p => p.Id == productoId && p.Proveedor.Id == id);

                if (producto == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el producto solicitado."
                    });
                }

                ProveedorProductoCreateDTO data = new ProveedorProductoCreateDTO
                {
                    ProductoId = producto.Id,
                    Producto = GetStringProperty(producto, "Nombre", "Producto", "Descripcion", "DescripcionProducto", "Titulo"),
                    Detalle = GetStringProperty(producto, "Detalle", "Observacion", "Observaciones"),
                    Precio = producto.Precio,
                    PrecioOferta = GetDecimalProperty(producto, "PrecioOferta", "PrecioPromocion", "PrecioPromocional", "Oferta"),
                    Financiable = producto.Financiable,
                    RubroId = producto.Rubro != null ? producto.Rubro.Id : 0
                };

                return Ok(new
                {
                    status = 200,
                    mensaje = "Producto obtenido correctamente.",
                    data = data
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al obtener el producto."
                });
            }
        }

        // PUT: endpoint/proveedor/5/productos/10
        [HttpPut("{id}/productos/{productoId}")]
        public async Task<IActionResult> UpdateProductoProveedor(int id, int productoId, [FromBody] ProveedorProductoCreateDTO editProducto)
        {
            try
            {
                Proveedor proveedor = await _context.Proveedores.FindAsync(id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                Producto producto = await _context.Productos
                    .Include(p => p.Proveedor)
                    .FirstOrDefaultAsync(p => p.Id == productoId && p.Proveedor.Id == id);

                if (producto == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el producto solicitado."
                    });
                }

                List<string> errores = ValidarProducto(editProducto);

                if (errores.Count > 0)
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Hay errores de validación.",
                        errores = errores
                    });
                }

                Rubro rubro = await _context.Rubros.FindAsync(editProducto.RubroId);

                if (rubro == null)
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Debe seleccionar un Rubro válido."
                    });
                }

                producto.Proveedor = proveedor;
                producto.Rubro = rubro;
                producto.Precio = editProducto.Precio;
                producto.Financiable = editProducto.Financiable;
                producto.Activo = true;

                SetStringProperty(producto, editProducto.Producto, "Nombre", "Producto", "Descripcion", "DescripcionProducto", "Titulo");
                SetStringProperty(producto, editProducto.Detalle, "Detalle", "Observacion", "Observaciones");
                SetDecimalProperty(producto, editProducto.PrecioOferta, "PrecioOferta", "PrecioPromocion", "PrecioPromocional", "Oferta");

                _context.Productos.Update(producto);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Se modificó correctamente el Producto."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al modificar el Producto. Intentelo nuevamente mas tarde."
                });
            }
        }

        // DELETE: endpoint/proveedor/5/productos/10
        [HttpDelete("{id}/productos/{productoId}")]
        public async Task<IActionResult> DeleteProductoProveedor(int id, int productoId)
        {
            try
            {
                Producto producto = await _context.Productos
                    .Include(p => p.Proveedor)
                    .FirstOrDefaultAsync(p => p.Id == productoId && p.Proveedor.Id == id);

                if (producto == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el producto solicitado."
                    });
                }

                producto.Activo = false;

                _context.Productos.Update(producto);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Se eliminó correctamente el Producto."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al eliminar el Producto. Intentelo nuevamente mas tarde."
                });
            }
        }

        // GET: endpoint/proveedor/5/productos/export-excel
        [HttpGet("{id}/productos/export-excel")]
        public IActionResult ExportProductosProveedorExcel(int id, string buscar = "")
        {
            try
            {
                Proveedor proveedor = _context.Proveedores.Find(id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                List<ProveedorProductoDTO> productos = ObtenerProductosProveedor(id);

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    string texto = buscar.Trim().ToLower();

                    productos = productos
                        .Where(p =>
                            (p.Producto != null && p.Producto.ToLower().Contains(texto)) ||
                            (p.Rubro != null && p.Rubro.ToLower().Contains(texto)) ||
                            (p.Detalle != null && p.Detalle.ToLower().Contains(texto)) ||
                            p.Precio.ToString().Contains(texto) ||
                            p.PrecioOferta.ToString().Contains(texto)
                        )
                        .ToList();
                }

                StringBuilder sb = new StringBuilder();

                sb.AppendLine("Producto;Rubro;Detalle;Precio;Precio Oferta;Financiable");

                foreach (ProveedorProductoDTO producto in productos.OrderBy(p => p.Producto))
                {
                    sb.Append(EscapeCsv(producto.Producto));
                    sb.Append(";");
                    sb.Append(EscapeCsv(producto.Rubro));
                    sb.Append(";");
                    sb.Append(EscapeCsv(producto.Detalle));
                    sb.Append(";");
                    sb.Append(producto.Precio);
                    sb.Append(";");
                    sb.Append(producto.PrecioOferta);
                    sb.Append(";");
                    sb.Append(producto.Financiable ? "Si" : "No");
                    sb.AppendLine();
                }

                byte[] bytes = Encoding.UTF8.GetPreamble()
                    .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                    .ToArray();

                string fileName = "productos-proveedor-" + id + ".csv";

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al exportar los productos del proveedor."
                });
            }
        }

        // GET: endpoint/proveedor/5/image
        [HttpGet("{id}/image")]
        public async Task<IActionResult> GetImage(int id)
        {
            try
            {
                Proveedor proveedor = await _context.Proveedores.FindAsync(id);

                if (proveedor == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                string fotoBase64 = "";

                if (proveedor.Foto != null)
                {
                    fotoBase64 = Convert.ToBase64String(proveedor.Foto);
                }

                return Ok(new
                {
                    status = 200,
                    mensaje = "Imagen del proveedor obtenida correctamente.",
                    data = new
                    {
                        Id = proveedor.Id,
                        Foto = fotoBase64
                    }
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al obtener la imagen del proveedor."
                });
            }
        }

        // POST: endpoint/proveedor/5/image
        [HttpPost("{id}/image")]
        public async Task<IActionResult> UploadImage(int id, [FromForm] IFormFile FotoProveedor)
        {
            try
            {
                Proveedor proveedorEdit = await _context.Proveedores.FindAsync(id);

                if (proveedorEdit == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        mensaje = "No se encontró el proveedor solicitado."
                    });
                }

                if (FotoProveedor != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await FotoProveedor.CopyToAsync(memoryStream);
                        proveedorEdit.Foto = memoryStream.ToArray();
                    }
                }
                else
                {
                    return BadRequest(new
                    {
                        status = 400,
                        mensaje = "Debe seleccionar una imagen."
                    });
                }

                _context.Proveedores.Update(proveedorEdit);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Se cargó correctamente la Foto del Proveedor."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    status = 500,
                    mensaje = "Hubo un error al cargar la Foto del Proveedor. Intentelo nuevamente mas tarde."
                });
            }
        }

        // POST: endpoint/proveedor/5/productos/10/imagenes
        [HttpPost("{id}/productos/{productoId}/imagenes")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubirImagenesProducto(
            int id, int productoId, [FromForm] List<IFormFile> FotoProducto)
        {
            try
            {
                var producto = await _context.Productos
                    .FirstOrDefaultAsync(p => p.Id == productoId && p.Proveedor.Id == id);

                if (producto == null)
                    return NotFound(new { status = 404, mensaje = "No se encontró el producto del proveedor solicitado." });

                foreach (var archivo in FotoProducto ?? new List<IFormFile>())
                {
                    if (archivo != null)
                    {
                        var adjunto = new ProductoAdjuntos
                        {
                            Nombre = Path.GetFileNameWithoutExtension(archivo.FileName),
                            Extension = Path.GetExtension(archivo.FileName)
                        };

                        using (var memoryStream = new MemoryStream())
                        {
                            await archivo.CopyToAsync(memoryStream);
                            adjunto.Producto = producto;
                            adjunto.Foto = memoryStream.ToArray();
                        }

                        _context.ProductoAdjuntos.Add(adjunto);
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new { status = 200, mensaje = "Se cargó correctamente la Foto del Producto." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { status = 500, mensaje = "Hubo un error al cargar la Foto del Producto. Intentelo nuevamente mas tarde." });
            }
        }

        // GET: endpoint/proveedor/5/productos/10/financiacion
        [HttpGet("{id}/productos/{productoId}/financiacion")]
        public async Task<IActionResult> GetFinanciacionProducto(int id, int productoId)
        {
            try
            {
                var producto = await _context.Productos
                    .Include(p => p.FinanciacionProducto)
                    .FirstOrDefaultAsync(p => p.Id == productoId && p.Proveedor.Id == id);

                if (producto == null)
                    return NotFound(new { status = 404, mensaje = "No se encontró el producto del proveedor solicitado." });

                return Ok(new
                {
                    status = 200,
                    mensaje = "Financiación del producto obtenida correctamente.",
                    data = MapFinanciacionProducto(producto)
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { status = 500, mensaje = "Hubo un error al obtener la financiación del Producto." });
            }
        }

        // POST: endpoint/proveedor/5/productos/10/financiacion
        [HttpPost("{id}/productos/{productoId}/financiacion")]
        public async Task<IActionResult> CrearFinanciacionProducto(
            int id, int productoId, [FromBody] ProveedorProductoFinanciacionCreateDTO request)
        {
            if (request == null)
                return BadRequest(new { status = 400, mensaje = "Los datos de la financiación son obligatorios." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var producto = await _context.Productos
                    .FirstOrDefaultAsync(p => p.Id == productoId && p.Proveedor.Id == id);

                if (producto == null)
                    return NotFound(new { status = 404, mensaje = "No se encontró el producto del proveedor solicitado." });

                await _context.FinanciacionProductos.AddAsync(new Financiacion
                {
                    CantidadCuotas = Convert.ToInt32(request.CantidadCuotasAdd),
                    InteresesPorCuota = Convert.ToDecimal(request.InteresCoutasAdd),
                    Producto = producto
                });
                await _context.SaveChangesAsync();

                await _context.Entry(producto).Collection(p => p.FinanciacionProducto).LoadAsync();

                return Ok(new
                {
                    status = 200,
                    mensaje = "Se cargó correctamente la financiación del Producto.",
                    data = MapFinanciacionProducto(producto)
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { status = 500, mensaje = "Hubo un error al Cargar la Finaciación del Producto. Intentelo nuevamente mas tarde." });
            }
        }

        // DELETE: endpoint/proveedor/productos/financiacion/15
        [HttpDelete("productos/financiacion/{id}")]
        public async Task<IActionResult> EliminarFinanciacionProducto(int id)
        {
            try
            {
                var financiacion = await _context.FinanciacionProductos.FindAsync(id);
                if (financiacion == null)
                    return NotFound(new { status = 404, mensaje = "No se encontró la financiación solicitada." });

                _context.FinanciacionProductos.Remove(financiacion);
                await _context.SaveChangesAsync();

                return Ok(new { status = 200, mensaje = "Se eliminó correctamente la financiación del Producto." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { status = 500, mensaje = "Hubo un error al eliminar la financiación del Producto." });
            }
        }

        private static ProveedorProductoFinanciacionResponseDTO MapFinanciacionProducto(Producto producto)
        {
            var data = new ProveedorProductoFinanciacionResponseDTO
            {
                ProductoId = producto.Id,
                ProductoNombre = producto.Descripcion,
                PrecioProducto = producto.Precio
            };

            foreach (var financiacion in producto.FinanciacionProducto)
            {
                data.ListaFinanciacion.Add(new FinanciacionDTO
                {
                    FinaciacionId = financiacion.Id,
                    CantidadCuotas = financiacion.CantidadCuotas,
                    InteresCuota = financiacion.InteresesPorCuota,
                    ValorCuota = decimal.Round(
                        (producto.Precio / financiacion.CantidadCuotas) *
                        (1 + (financiacion.InteresesPorCuota / 100)), 2)
                });
            }

            return data;
        }

        private List<ProveedorProductoDTO> ObtenerProductosProveedor(int proveedorId)
        {
            List<Producto> productosDb = _context.Productos
                .Include(p => p.Rubro)
                .Include(p => p.Proveedor)
                .Where(p => p.Activo && p.Proveedor.Id == proveedorId)
                .ToList();

            List<ProveedorProductoDTO> productos = productosDb
                .Select(p => new ProveedorProductoDTO
                {
                    Id = p.Id,
                    Producto = GetStringProperty(p, "Nombre", "Producto", "Descripcion", "DescripcionProducto", "Titulo"),
                    Rubro = p.Rubro != null ? p.Rubro.Nombre : "",
                    Detalle = GetStringProperty(p, "Detalle", "Observacion", "Observaciones"),
                    Precio = p.Precio,
                    PrecioOferta = GetDecimalProperty(p, "PrecioOferta", "PrecioPromocion", "PrecioPromocional", "Oferta"),
                    Financiable = p.Financiable
                })
                .ToList();

            return productos;
        }

        private ProveedorDTO CreateProveedorDTO(ProveedorDTO proveedorDto, Proveedor proveedor = null)
        {
            proveedorDto.Rubros = _context.Rubros
                .Where(x => x.Activo)
                .Select(g => new SelectListItem
                {
                    Text = g.Nombre,
                    Value = g.Id.ToString()
                })
                .ToList();

            proveedorDto.Empresas = _context.Empresas
                .Select(g => new SelectListItem
                {
                    Text = g.RazonSocial,
                    Value = g.Id.ToString()
                })
                .ToList();

            if (proveedorDto.RubrosSeleccionados == null)
            {
                proveedorDto.RubrosSeleccionados = new List<int>();
            }

            if (proveedor != null)
            {
                proveedorDto.ProveedorId = proveedor.Id;
                proveedorDto.Nombre = proveedor.Nombre;
                proveedorDto.CUIT = proveedor.CUIT;
                proveedorDto.RazonSocial = proveedor.RazonSocial;
                proveedorDto.Domicilio = proveedor.Domicilio;
                proveedorDto.EmpresaId = proveedor.Empresa != null ? proveedor.Empresa.Id : 0;
                proveedorDto.RubrosSeleccionados = proveedor.Rubros != null
                    ? proveedor.Rubros.Select(x => x.Rubro.Id).ToList()
                    : new List<int>();
            }

            return proveedorDto;
        }

        private List<string> ValidarProveedor(ProveedorDTO proveedor)
        {
            List<string> errores = new List<string>();

            if (proveedor.EmpresaId == 0)
            {
                errores.Add("Debe seleccionar una Empresa.");
            }

            if (proveedor.RubrosSeleccionados == null || proveedor.RubrosSeleccionados.Count < 1)
            {
                errores.Add("Debe seleccionar un Rubro.");
            }

            if (string.IsNullOrWhiteSpace(proveedor.Nombre))
            {
                errores.Add("Debe ingresar el Nombre del Proveedor.");
            }

            if (proveedor.CUIT == 0)
            {
                errores.Add("Debe ingresar el CUIT del Proveedor.");
            }

            if (string.IsNullOrWhiteSpace(proveedor.RazonSocial))
            {
                errores.Add("Debe ingresar la Razón Social del Proveedor.");
            }

            return errores;
        }

        private List<string> ValidarProducto(ProveedorProductoCreateDTO producto)
        {
            List<string> errores = new List<string>();

            if (string.IsNullOrWhiteSpace(producto.Producto))
            {
                errores.Add("Debe ingresar la Descripción del Producto.");
            }

            if (producto.Precio < 0)
            {
                errores.Add("El Precio del Producto no puede ser menor a 0.");
            }

            if (producto.PrecioOferta < 0)
            {
                errores.Add("El Precio de Oferta del Producto no puede ser menor a 0.");
            }

            if (producto.RubroId == 0)
            {
                errores.Add("Debe seleccionar un Rubro.");
            }

            return errores;
        }

        private string GetStringProperty(object obj, params string[] propertyNames)
        {
            if (obj == null)
            {
                return "";
            }

            foreach (string propertyName in propertyNames)
            {
                var property = obj.GetType().GetProperty(propertyName);

                if (property != null)
                {
                    object value = property.GetValue(obj, null);

                    if (value != null)
                    {
                        return value.ToString();
                    }
                }
            }

            return "";
        }

        private decimal GetDecimalProperty(object obj, params string[] propertyNames)
        {
            if (obj == null)
            {
                return 0;
            }

            foreach (string propertyName in propertyNames)
            {
                var property = obj.GetType().GetProperty(propertyName);

                if (property != null)
                {
                    object value = property.GetValue(obj, null);

                    if (value != null)
                    {
                        decimal result;

                        if (decimal.TryParse(value.ToString(), out result))
                        {
                            return result;
                        }
                    }
                }
            }

            return 0;
        }

        private void SetStringProperty(object obj, string value, params string[] propertyNames)
        {
            if (obj == null)
            {
                return;
            }

            foreach (string propertyName in propertyNames)
            {
                var property = obj.GetType().GetProperty(propertyName);

                if (property != null && property.CanWrite && property.PropertyType == typeof(string))
                {
                    property.SetValue(obj, value);
                    return;
                }
            }
        }

        private void SetDecimalProperty(object obj, decimal value, params string[] propertyNames)
        {
            if (obj == null)
            {
                return;
            }

            foreach (string propertyName in propertyNames)
            {
                var property = obj.GetType().GetProperty(propertyName);

                if (property != null && property.CanWrite)
                {
                    if (property.PropertyType == typeof(decimal))
                    {
                        property.SetValue(obj, value);
                        return;
                    }

                    if (property.PropertyType == typeof(decimal?))
                    {
                        property.SetValue(obj, value);
                        return;
                    }
                }
            }
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            value = value.Replace("\"", "\"\"");

            if (value.Contains(";") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                value = "\"" + value + "\"";
            }

            return value;
        }
    }
}