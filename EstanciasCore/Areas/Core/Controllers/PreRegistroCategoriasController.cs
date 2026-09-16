using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DAL.Data;
using DAL.Models;
using DAL.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace EstanciasCore.Areas.Core.Controllers
{
    [Area("Core")]
    public class PreRegistroCategoriasController : Controller
    {
        private readonly EstanciasContext _context;

        public PreRegistroCategoriasController(EstanciasContext context)
        {
            _context = context;
        }

        private async Task CargarCategoriasViewBag(int? selectedValue = null)
        {
            var list = await _context.UsuariosCategorias
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .Select(x => new SelectListItem
                {
                    Text = string.IsNullOrEmpty(x.Codigo) ? x.Nombre : $"{x.Nombre} ({x.Codigo})",
                    Value = x.Id.ToString(),
                    Selected = selectedValue.HasValue && x.Id == selectedValue.Value
                }).ToListAsync();

            ViewBag.Categorias = list;
        }

        // GET: Administracion/PreRegistroCategorias
        public async Task<IActionResult> Index()
        {
            return View(await _context.PreRegistroCategorias.Include(p => p.Categoria).ToListAsync());
        }

        // GET: Administracion/PreRegistroCategorias/_Create
        public async Task<IActionResult> _Create()
        {
            await CargarCategoriasViewBag();
            return PartialView();
        }

        // POST: Administracion/PreRegistroCategorias/_Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Create(PreRegistroCategoriaDTO model)
        {
            ModelState.Remove("Id");
            if (ModelState.IsValid)
            {
                // Verify if DNI already exists to avoid duplicates
                if (await _context.PreRegistroCategorias.AnyAsync(p => p.DNI == model.DNI))
                {
                    ModelState.AddModelError("DNI", "El DNI ingresado ya existe.");
                    await CargarCategoriasViewBag(model.CategoriaId);
                    return PartialView(model);
                }

                var categoriaObj = model.CategoriaId.HasValue ? await _context.UsuariosCategorias.FindAsync(model.CategoriaId.Value) : null;

                var entity = new PreRegistroCategorias
                {
                    DNI = model.DNI,
                    NombreCompleto = model.NombreCompleto,
                    CategoriaId = model.CategoriaId,
                    Categoria = categoriaObj
                };

                _context.Add(entity);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await CargarCategoriasViewBag(model.CategoriaId);
            return PartialView(model);
        }

        // GET: Administracion/PreRegistroCategorias/_Edit/5
        public async Task<IActionResult> _Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entity = await _context.PreRegistroCategorias.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            var model = new PreRegistroCategoriaDTO
            {
                Id = entity.Id,
                DNI = entity.DNI,
                NombreCompleto = entity.NombreCompleto,
                CategoriaId = entity.CategoriaId ?? entity.Categoria?.Id
            };

            await CargarCategoriasViewBag(model.CategoriaId);
            return PartialView(model);
        }

        // POST: Administracion/PreRegistroCategorias/_Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Edit(int id, PreRegistroCategoriaDTO model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var entity = await _context.PreRegistroCategorias.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);
                if (entity == null)
                {
                    return NotFound();
                }

                // Verify if another record has the same DNI
                if (await _context.PreRegistroCategorias.AnyAsync(p => p.DNI == model.DNI && p.Id != id))
                {
                    ModelState.AddModelError("DNI", "El DNI ingresado ya está asignado a otro registro.");
                    await CargarCategoriasViewBag(model.CategoriaId);
                    return PartialView(model);
                }

                var categoriaObj = model.CategoriaId.HasValue ? await _context.UsuariosCategorias.FindAsync(model.CategoriaId.Value) : null;

                entity.DNI = model.DNI;
                entity.NombreCompleto = model.NombreCompleto;
                entity.CategoriaId = model.CategoriaId;
                entity.Categoria = categoriaObj;

                try
                {
                    _context.Update(entity);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PreRegistroCategoriasExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            await CargarCategoriasViewBag(model.CategoriaId);
            return PartialView(model);
        }

        // GET: Administracion/PreRegistroCategorias/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.PreRegistroCategorias.FindAsync(id);
            if (entity != null)
            {
                _context.PreRegistroCategorias.Remove(entity);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Registro eliminado correctamente.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PreRegistroCategoriasExists(int id)
        {
            return _context.PreRegistroCategorias.Any(e => e.Id == id);
        }

        // GET: Administracion/PreRegistroCategorias/_Importar
        public IActionResult _Importar()
        {
            return PartialView();
        }

        // GET: Administracion/PreRegistroCategorias/DescargarPlantilla
        public IActionResult DescargarPlantilla()
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Plantilla");
                worksheet.Cells[1, 1].Value = "DNI";
                worksheet.Cells[1, 2].Value = "NombreCompleto";
                worksheet.Cells[1, 3].Value = "CodigoCategoria";

                // Formateo de la cabecera
                using (var range = worksheet.Cells[1, 1, 1, 3])
                {
                    range.Style.Font.Bold = true;
                }

                var stream = new MemoryStream(package.GetAsByteArray());
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Plantilla_PreRegistroCategorias.xlsx");
            }
        }

        // POST: Administracion/PreRegistroCategorias/Importar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar(IFormFile archivo)
        {
            if (archivo == null || archivo.Length <= 0)
            {
                TempData["Error"] = "Por favor, seleccione un archivo válido.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(archivo.FileName).ToLower();

            if (extension == ".xlsx")
            {
                await ImportarExcel(archivo);
            }
            else if (extension == ".csv")
            {
                await ImportarCsv(archivo);
            }
            else
            {
                TempData["Error"] = "Formato de archivo no soportado. Use .xlsx o .csv";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task ImportarExcel(IFormFile archivo)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    await archivo.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets.FirstOrDefault(); // Get first sheet
                        if (worksheet == null) throw new Exception("No se encontraron hojas en el archivo Excel.");
                        
                        var rowCount = worksheet.Dimension?.Rows ?? 0;

                        // Start from row 2 assuming row 1 is header
                        for (int row = 2; row <= rowCount; row++)
                        {
                            var dni = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            var nombreCompleto = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                            var codigoCategoria = worksheet.Cells[row, 3].Value?.ToString()?.Trim();

                            if (!string.IsNullOrEmpty(dni))
                            {
                                await ProcessRecord(dni, nombreCompleto, codigoCategoria);
                            }
                        }
                    }
                }
                TempData["Success"] = "Importación de Excel completada.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al procesar el Excel: {ex.Message}";
            }
        }

        private async Task ImportarCsv(IFormFile archivo)
        {
            try
            {
                using (var reader = new StreamReader(archivo.OpenReadStream()))
                {
                    var isFirstRow = true;
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (isFirstRow)
                        {
                            isFirstRow = false; // Skip header
                            continue;
                        }

                        // Handle both comma and semicolon separators
                        char separator = line.Contains(";") ? ';' : ',';
                        var values = line.Split(separator);

                        if (values.Length >= 3)
                        {
                            var dni = values[0]?.Trim();
                            var nombreCompleto = values[1]?.Trim();
                            var codigoCategoria = values[2]?.Trim();

                            if (!string.IsNullOrEmpty(dni))
                            {
                                await ProcessRecord(dni, nombreCompleto, codigoCategoria);
                            }
                        }
                    }
                }
                TempData["Success"] = "Importación de CSV completada.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al procesar el CSV: {ex.Message}";
            }
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

            await _context.SaveChangesAsync();
        }
    }
}
