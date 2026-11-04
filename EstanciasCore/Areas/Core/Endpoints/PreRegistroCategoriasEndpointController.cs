using DAL.Data;
using DAL.DTOs;
using DAL.Models;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Areas.Core.Controllers
{
    // Agregar junto al controlador MVC. Mantener los permisos del módulo
    // y, si se autentica con cookies, su protección antiforgery.
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/preregistro-categorias")]

    public class PreRegistroCategoriasEndpointController : ControllerBase
    {
        private readonly EstanciasContext _context;

        public PreRegistroCategoriasEndpointController(EstanciasContext context)
        {
            _context = context;
        }

        // GET /endpoint/preregistro-categorias
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var registros = await _context.PreRegistroCategorias.AsNoTracking()
                .Select(p => new PreRegistroCategoriaDTO
                {
                    Id = p.Id,
                    DNI = p.DNI,
                    NombreCompleto = p.NombreCompleto,
                    CategoriaId = p.CategoriaId
                }).ToListAsync();

            return Ok(registros);
        }

        // GET /endpoint/preregistro-categorias/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obtener(int id)
        {
            var registro = await _context.PreRegistroCategorias.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new PreRegistroCategoriaDTO
                {
                    Id = p.Id,
                    DNI = p.DNI,
                    NombreCompleto = p.NombreCompleto,
                    CategoriaId = p.CategoriaId
                }).FirstOrDefaultAsync();

            if (registro == null)
                return NotFound(new { mensaje = "Registro no encontrado." });

            return Ok(registro);
        }

        // POST /endpoint/preregistro-categorias
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] PreRegistroCategoriaDTO model)
        {
            if (model == null)
                return BadRequest(new { mensaje = "Enviá los datos del registro." });

            ModelState.Remove("Id");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(model.DNI))
                return BadRequest(new { mensaje = "El DNI es obligatorio." });

            if (await _context.PreRegistroCategorias.AnyAsync(p => p.DNI == model.DNI))
                return Conflict(new { mensaje = "El DNI ingresado ya existe." });

            var categoria = model.CategoriaId.HasValue
                ? await _context.UsuariosCategorias.FindAsync(model.CategoriaId.Value)
                : null;

            if (model.CategoriaId.HasValue && categoria == null)
                return BadRequest(new { mensaje = "La categoría indicada no existe." });

            var entity = new PreRegistroCategorias
            {
                DNI = model.DNI,
                NombreCompleto = model.NombreCompleto,
                CategoriaId = model.CategoriaId,
                Categoria = categoria
            };

            _context.PreRegistroCategorias.Add(entity);
            await ActualizarCategoriaUsuarioExistente(model.DNI, categoria);
            await _context.SaveChangesAsync();

            model.Id = entity.Id;
            return CreatedAtAction(nameof(Obtener), new { id = entity.Id }, model);
        }

        // PUT /endpoint/preregistro-categorias/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Editar(int id, [FromBody] PreRegistroCategoriaDTO model)
        {
            if (model == null)
                return BadRequest(new { mensaje = "Enviá los datos del registro." });

            // El Id de la URL identifica el registro; no hace falta enviarlo en el JSON.
            model.Id = id;
            ModelState.Remove("Id");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(model.DNI))
                return BadRequest(new { mensaje = "El DNI es obligatorio." });

            var entity = await _context.PreRegistroCategorias.FindAsync(id);
            if (entity == null)
                return NotFound(new { mensaje = "Registro no encontrado." });

            if (await _context.PreRegistroCategorias.AnyAsync(p => p.DNI == model.DNI && p.Id != id))
                return Conflict(new { mensaje = "El DNI está asignado a otro registro." });

            var categoria = model.CategoriaId.HasValue
                ? await _context.UsuariosCategorias.FindAsync(model.CategoriaId.Value)
                : null;

            if (model.CategoriaId.HasValue && categoria == null)
                return BadRequest(new { mensaje = "La categoría indicada no existe." });

            entity.DNI = model.DNI;
            entity.NombreCompleto = model.NombreCompleto;
            entity.CategoriaId = model.CategoriaId;
            entity.Categoria = categoria;

            await ActualizarCategoriaUsuarioExistente(model.DNI, categoria);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.PreRegistroCategorias.AnyAsync(p => p.Id == id))
                    return NotFound(new { mensaje = "Registro no encontrado." });
                throw;
            }

            return Ok(model);
        }

        // DELETE /endpoint/preregistro-categorias/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var entity = await _context.PreRegistroCategorias.FindAsync(id);
            if (entity == null)
                return NotFound(new { mensaje = "Registro no encontrado." });

            _context.PreRegistroCategorias.Remove(entity);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET /endpoint/preregistro-categorias/plantilla
        [HttpGet("plantilla")]
        public IActionResult DescargarPlantilla()
        {
            using (var package = new ExcelPackage())
            {
                var hoja = package.Workbook.Worksheets.Add("Plantilla");
                hoja.Cells[1, 1].Value = "DNI";
                hoja.Cells[1, 2].Value = "NombreCompleto";
                hoja.Cells[1, 3].Value = "CodigoCategoria";
                hoja.Cells[1, 1, 1, 3].Style.Font.Bold = true;

                return File(package.GetAsByteArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Plantilla_PreRegistroCategorias.xlsx");
            }
        }

        // POST /endpoint/preregistro-categorias/importar
        // multipart/form-data: campo archivo. Conserva Excel y CSV del MVC.
        [HttpPost("importar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Importar([FromForm] IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
                return BadRequest(new { mensaje = "Seleccioná un archivo válido." });

            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".csv")
                return BadRequest(new { mensaje = "Formato no soportado. Usá .xlsx o .csv." });

            // ProcessRecord guarda cada fila, como en el MVC original.
            // La transacción revierte todas si ocurre un error durante la importación.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                int procesados;
                try
                {
                    procesados = extension == ".xlsx"
                        ? await ImportarExcel(archivo)
                        : await ImportarCsv(archivo);
                }
                catch (InvalidDataException ex)
                {
                    return BadRequest(new { mensaje = ex.Message });
                }

                // Compatible con la versión de EF Core del proyecto.
                transaction.Commit();
                return Ok(new { mensaje = "Importación completada.", procesados });
            }
        }

        private async Task<int> ImportarExcel(IFormFile archivo)
        {
            int procesados = 0;
            using (var stream = new MemoryStream())
            {
                await archivo.CopyToAsync(stream);
                stream.Position = 0;
                using (var package = new ExcelPackage(stream))
                {
                    var hoja = package.Workbook.Worksheets.FirstOrDefault();
                    if (hoja == null)
                        throw new InvalidDataException("No se encontraron hojas en el Excel.");

                    int filas = hoja.Dimension?.Rows ?? 0;
                    for (int fila = 2; fila <= filas; fila++)
                    {
                        var dni = hoja.Cells[fila, 1].Value?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(dni)) continue;

                        var nombre = hoja.Cells[fila, 2].Value?.ToString()?.Trim();
                        var codigo = hoja.Cells[fila, 3].Value?.ToString()?.Trim();
                        await ProcessRecord(dni, nombre, codigo);
                        procesados++;
                    }
                }
            }
            return procesados;
        }

        private async Task<int> ImportarCsv(IFormFile archivo)
        {
            int procesados = 0;
            using (var reader = new StreamReader(archivo.OpenReadStream()))
            {
                bool encabezado = true;
                while (!reader.EndOfStream)
                {
                    var linea = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(linea)) continue;
                    if (encabezado)
                    {
                        encabezado = false;
                        continue;
                    }

                    // Mismo formato simple del MVC: sin campos entrecomillados
                    // que contengan separadores o saltos de línea.
                    char separador = linea.Contains(";") ? ';' : ',';
                    var valores = linea.Split(separador);
                    if (valores.Length < 3 || string.IsNullOrWhiteSpace(valores[0])) continue;

                    await ProcessRecord(valores[0].Trim(), valores[1].Trim(), valores[2].Trim());
                    procesados++;
                }
            }
            return procesados;
        }

        private async Task ProcessRecord(string dni, string nombreCompleto, string codigoCategoria)
        {
            UsuariosCategorias categoriaObj = null;

            if (!string.IsNullOrEmpty(codigoCategoria))
            {
                categoriaObj = await _context.UsuariosCategorias.FirstOrDefaultAsync(c => c.Codigo == codigoCategoria);
                if (categoriaObj == null)
                {
                    categoriaObj = await _context.UsuariosCategorias.FirstOrDefaultAsync(c => c.Codigo.ToLower() == codigoCategoria.ToLower());
                }
            }

            var existingRecord = await _context.PreRegistroCategorias.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.DNI == dni);

            if (existingRecord != null)
            {
                // Update
                existingRecord.NombreCompleto = nombreCompleto ?? existingRecord.NombreCompleto;
                if (categoriaObj != null)
                {
                    existingRecord.Categoria = categoriaObj;
                    existingRecord.CategoriaId = categoriaObj.Id;
                }
                _context.Update(existingRecord);
            }
            else
            {
                // Insert
                var newRecord = new PreRegistroCategorias
                {
                    DNI = dni,
                    NombreCompleto = nombreCompleto ?? "",
                    Categoria = categoriaObj,
                    CategoriaId = categoriaObj?.Id
                };
                _context.Add(newRecord);
            }

            if (categoriaObj != null)
            {
                await ActualizarCategoriaUsuarioExistente(dni, categoriaObj);
            }

            await _context.SaveChangesAsync();
        }

        private async Task ActualizarCategoriaUsuarioExistente(string dni, UsuariosCategorias categoriaObj)
        {
            if (string.IsNullOrWhiteSpace(dni) || categoriaObj == null)
            {
                return;
            }

            var dniTrimmed = dni.Trim();
            var usuariosExistentes = await _context.Usuarios
                .Include(u => u.Personas)
                .Include(u => u.UsuariosCategorias)
                .Where(u => u.Personas != null && u.Personas.NroDocumento != null && u.Personas.NroDocumento.Trim() == dniTrimmed)
                .ToListAsync();

            foreach (var usuario in usuariosExistentes)
            {
                usuario.UsuariosCategorias = categoriaObj;
                _context.Usuarios.Update(usuario);
            }
        }
    }
}
