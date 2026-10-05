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
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace EstanciasCore.Areas.Core.Controllers
{
    [Area("Core")]
    public class MorososController : Controller
    {
        private readonly EstanciasContext _context;

        public MorososController(EstanciasContext context)
        {
            _context = context;
        }

        // GET: Core/Morosos
        public async Task<IActionResult> Index()
        {
            return View(await _context.Morosos.OrderByDescending(m => m.FechaCarga).ToListAsync());
        }

        // GET: Core/Morosos/_Create
        public IActionResult _Create()
        {
            return PartialView();
        }

        // POST: Core/Morosos/_Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Create(MorososDTO model)
        {
            ModelState.Remove("Id");
            if (ModelState.IsValid)
            {
                var existingRecord = await _context.Morosos.FirstOrDefaultAsync(p => p.DNI == model.DNI);
                if (existingRecord != null)
                {
                    existingRecord.NombreCompleto = model.NombreCompleto;
                    existingRecord.FechaCarga = DateTime.Now;
                    _context.Update(existingRecord);
                }
                else
                {
                    var entity = new Morosos
                    {
                        DNI = model.DNI,
                        NombreCompleto = model.NombreCompleto,
                        FechaCarga = DateTime.Now
                    };
                    _context.Add(entity);
                }

                int usuariosActualizados = await MarcarUsuariosIncobrable(model.DNI, true);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Registro guardado correctamente. ({usuariosActualizados} usuario(s) marcados como Incobrables)";
                return RedirectToAction(nameof(Index));
            }
            return PartialView(model);
        }

        // GET: Core/Morosos/_Edit/5
        public async Task<IActionResult> _Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entity = await _context.Morosos.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            var model = new MorososDTO
            {
                Id = entity.Id,
                DNI = entity.DNI,
                NombreCompleto = entity.NombreCompleto,
                FechaCarga = entity.FechaCarga
            };

            return PartialView(model);
        }

        // POST: Core/Morosos/_Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Edit(int id, MorososDTO model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var entity = await _context.Morosos.FindAsync(id);
                if (entity == null)
                {
                    return NotFound();
                }

                string oldDni = entity.DNI;
                entity.DNI = model.DNI;
                entity.NombreCompleto = model.NombreCompleto;
                entity.FechaCarga = DateTime.Now;

                try
                {
                    _context.Update(entity);

                    // Si el DNI cambió, desmarcar el anterior y marcar el nuevo
                    if (oldDni != model.DNI)
                    {
                        if (!_context.Morosos.Any(m => m.DNI == oldDni && m.Id != id))
                        {
                            await MarcarUsuariosIncobrable(oldDni, false);
                        }
                    }

                    await MarcarUsuariosIncobrable(model.DNI, true);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MorososExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["Success"] = "Registro actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            return PartialView(model);
        }

        // GET: Core/Morosos/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.Morosos.FindAsync(id);
            if (entity != null)
            {
                string dni = entity.DNI;
                _context.Morosos.Remove(entity);

                // Si no quedan otros registros con ese DNI, desmarcar usuario como Incobrable
                if (!_context.Morosos.Any(m => m.DNI == dni && m.Id != id))
                {
                    await MarcarUsuariosIncobrable(dni, false);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Registro eliminado correctamente y estado Incobrable actualizado.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool MorososExists(int id)
        {
            return _context.Morosos.Any(e => e.Id == id);
        }

        // GET: Core/Morosos/_Importar
        public IActionResult _Importar()
        {
            return PartialView();
        }

        // GET: Core/Morosos/_VerHistorial
        public async Task<IActionResult> _VerHistorial()
        {
            var historial = await _context.HistoricoDeCargaMorosos
                .Include(h => h.Usuario)
                .OrderByDescending(h => h.FechaCarga)
                .ToListAsync();

            return PartialView("_VerHistorial", historial);
        }

        // GET: Core/Morosos/DescargarPlantilla
        public IActionResult DescargarPlantilla()
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Plantilla_Morosos");
                worksheet.Cells[1, 1].Value = "DNI";
                worksheet.Cells[1, 2].Value = "NombreCompleto";

                using (var range = worksheet.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                }

                var stream = new MemoryStream(package.GetAsByteArray());
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Plantilla_Morosos.xlsx");
            }
        }

        // GET: Core/Morosos/DescargarListadoVigente
        public async Task<IActionResult> DescargarListadoVigente()
        {
            var listado = await _context.Morosos.OrderBy(m => m.NombreCompleto).ToListAsync();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Morosos_Vigentes");
                worksheet.Cells[1, 1].Value = "DNI";
                worksheet.Cells[1, 2].Value = "NombreCompleto";
                worksheet.Cells[1, 3].Value = "FechaCarga";

                using (var range = worksheet.Cells[1, 1, 1, 3])
                {
                    range.Style.Font.Bold = true;
                }

                int row = 2;
                foreach (var item in listado)
                {
                    worksheet.Cells[row, 1].Value = item.DNI;
                    worksheet.Cells[row, 2].Value = item.NombreCompleto;
                    worksheet.Cells[row, 3].Value = item.FechaCarga.ToString("dd/MM/yyyy HH:mm");
                    row++;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream(package.GetAsByteArray());
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Listado_Morosos_Vigentes_{DateTime.Now:yyyyMMdd}.xlsx");
            }
        }

        // POST: Core/Morosos/Importar
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

            List<(string DNI, string NombreCompleto)> registrosArchivo = new List<(string DNI, string NombreCompleto)>();
            List<string> erroresLectura = new List<string>();
            int registrosTotales = 0;
            int fallidos = 0;
            int omitidos = 0;

            if (extension == ".xlsx")
            {
                (registrosArchivo, erroresLectura, registrosTotales, fallidos, omitidos) = await LeerExcel(archivo);
            }
            else if (extension == ".csv")
            {
                (registrosArchivo, erroresLectura, registrosTotales, fallidos, omitidos) = await LeerCsv(archivo);
            }
            else
            {
                TempData["Error"] = "Formato de archivo no soportado. Use .xlsx o .csv";
                return RedirectToAction(nameof(Index));
            }

            if (erroresLectura.Any())
            {
                TempData["ErroresImportacion"] = Newtonsoft.Json.JsonConvert.SerializeObject(erroresLectura);
            }

            await ProcesarSincronizacionMorosos(registrosArchivo);

            // Guardar registro en HistoricoDeCargaMorosos
            try
            {
                var usuarioActual = _context.Usuarios.FirstOrDefault(x => x.UserName == User.Identity.Name || x.Email == User.Identity.Name);
                int cargados = registrosArchivo.Count;

                string observacion = $"Archivo: '{archivo.FileName}'";
                if (erroresLectura.Any())
                {
                    observacion += " Detalle: " + string.Join(" | ", erroresLectura);
                }

                var historico = new HistoricoDeCargaMorosos
                {
                    RegistrosTotales = registrosTotales,
                    Cargados = cargados,
                    Fallidos = fallidos,
                    Omitidos = omitidos,
                    Observacion = observacion,
                    Usuario = usuarioActual,
                    FechaCarga = DateTime.Now
                };

                _context.HistoricoDeCargaMorosos.Add(historico);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"Error al guardar HistoricoDeCargaMorosos: {ex.Message}");
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<(List<(string DNI, string NombreCompleto)> Registros, List<string> Errores, int RegistrosTotales, int Fallidos, int Omitidos)> LeerExcel(IFormFile archivo)
        {
            var registros = new List<(string DNI, string NombreCompleto)>();
            var errores = new List<string>();
            var dnisVistos = new HashSet<string>();
            int registrosTotales = 0;
            int fallidos = 0;
            int omitidos = 0;

            using (var stream = new MemoryStream())
            {
                await archivo.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null) throw new Exception("No se encontraron hojas en el archivo Excel.");

                    var rowCount = worksheet.Dimension?.Rows ?? 0;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        registrosTotales++;
                        var dni = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                        var nombreCompleto = worksheet.Cells[row, 2].Value?.ToString()?.Trim();

                        if (string.IsNullOrWhiteSpace(dni))
                        {
                            fallidos++;
                            errores.Add($"Fila {row}: DNI vacío");
                            continue;
                        }

                        if (dnisVistos.Contains(dni))
                        {
                            omitidos++;
                            errores.Add($"Fila {row}: DNI {dni} duplicado en el archivo");
                            continue;
                        }

                        dnisVistos.Add(dni);
                        registros.Add((dni, nombreCompleto ?? ""));
                    }
                }
            }
            return (registros, errores, registrosTotales, fallidos, omitidos);
        }

        private async Task<(List<(string DNI, string NombreCompleto)> Registros, List<string> Errores, int RegistrosTotales, int Fallidos, int Omitidos)> LeerCsv(IFormFile archivo)
        {
            var registros = new List<(string DNI, string NombreCompleto)>();
            var errores = new List<string>();
            var dnisVistos = new HashSet<string>();
            int registrosTotales = 0;
            int fallidos = 0;
            int omitidos = 0;

            using (var reader = new StreamReader(archivo.OpenReadStream()))
            {
                var isFirstRow = true;
                int row = 1;
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    row++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (isFirstRow)
                    {
                        isFirstRow = false;
                        continue;
                    }

                    registrosTotales++;
                    char separator = line.Contains(";") ? ';' : ',';
                    var values = line.Split(separator);

                    if (values.Length >= 1)
                    {
                        var dni = values[0]?.Trim();
                        var nombreCompleto = values.Length >= 2 ? values[1]?.Trim() : "";

                        if (string.IsNullOrWhiteSpace(dni))
                        {
                            fallidos++;
                            errores.Add($"Fila {row}: DNI vacío");
                            continue;
                        }

                        if (dnisVistos.Contains(dni))
                        {
                            omitidos++;
                            errores.Add($"Fila {row}: DNI {dni} duplicado en el archivo");
                            continue;
                        }

                        dnisVistos.Add(dni);
                        registros.Add((dni, nombreCompleto ?? ""));
                    }
                    else
                    {
                        fallidos++;
                        errores.Add($"Fila {row}: Formato de fila incorrecto");
                    }
                }
            }
            return (registros, errores, registrosTotales, fallidos, omitidos);
        }

        private async Task ProcesarSincronizacionMorosos(List<(string DNI, string NombreCompleto)> registrosArchivo)
        {
            try
            {
                var dnisEnArchivo = registrosArchivo.Select(r => r.DNI).Distinct().ToHashSet();

                // 1. Actualizar / Insertar registros en la tabla Morosos
                var morososExistentes = await _context.Morosos.ToListAsync();
                var morososExistentesDict = morososExistentes.ToDictionary(m => m.DNI, m => m);

                DateTime fechaCargaActual = DateTime.Now;

                foreach (var item in registrosArchivo.GroupBy(x => x.DNI).Select(g => g.First()))
                {
                    if (morososExistentesDict.TryGetValue(item.DNI, out var existente))
                    {
                        existente.NombreCompleto = string.IsNullOrWhiteSpace(item.NombreCompleto) ? existente.NombreCompleto : item.NombreCompleto;
                        existente.FechaCarga = fechaCargaActual;
                        _context.Update(existente);
                    }
                    else
                    {
                        var nuevoMoroso = new Morosos
                        {
                            DNI = item.DNI,
                            NombreCompleto = item.NombreCompleto,
                            FechaCarga = fechaCargaActual
                        };
                        _context.Add(nuevoMoroso);
                    }
                }

                // 2. Eliminar registros de la tabla Morosos que YA NO estén en el archivo
                var morososAEliminar = morososExistentes.Where(m => !dnisEnArchivo.Contains(m.DNI)).ToList();
                if (morososAEliminar.Any())
                {
                    _context.Morosos.RemoveRange(morososAEliminar);
                }

                // 3. Sincronizar campo Incobrable en Usuarios
                // Cargar usuarios cuyo DNI está en el archivo O que actualmente son Incobrables
                var todosLosUsuarios = await _context.Usuarios
                    .Include(u => u.Personas)
                    .Where(u => u.Incobrable || (u.Personas != null && dnisEnArchivo.Contains(u.Personas.NroDocumento)) || dnisEnArchivo.Contains(u.UserName))
                    .ToListAsync();

                int usuariosIncobrablesMarcados = 0;
                int usuariosDesmarcados = 0;

                foreach (var usuario in todosLosUsuarios)
                {
                    string dniUsuario = usuario.Personas?.NroDocumento ?? usuario.UserName;
                    bool figuraEnArchivo = !string.IsNullOrEmpty(dniUsuario) && dnisEnArchivo.Contains(dniUsuario);

                    if (figuraEnArchivo)
                    {
                        if (!usuario.Incobrable)
                        {
                            usuario.Incobrable = true;
                            _context.Update(usuario);
                            usuariosIncobrablesMarcados++;
                        }
                    }
                    else
                    {
                        if (usuario.Incobrable)
                        {
                            usuario.Incobrable = false;
                            _context.Update(usuario);
                            usuariosDesmarcados++;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Sincronización completada. ({dnisEnArchivo.Count} morosos procesados). " +
                                      $"{usuariosIncobrablesMarcados} usuario(s) marcados como Incobrables. " +
                                      $"{usuariosDesmarcados} usuario(s) desmarcados por no figurar en el listado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al procesar la importación: {ex.Message}";
            }
        }

        private async Task<int> MarcarUsuariosIncobrable(string dni, bool esIncobrable)
        {
            if (string.IsNullOrWhiteSpace(dni)) return 0;

            var usuariosCoincidentes = await _context.Usuarios
                .Include(u => u.Personas)
                .Where(u => (u.Personas != null && u.Personas.NroDocumento == dni) || u.UserName == dni)
                .ToListAsync();

            int cantidad = 0;
            foreach (var u in usuariosCoincidentes)
            {
                if (u.Incobrable != esIncobrable)
                {
                    u.Incobrable = esIncobrable;
                    _context.Update(u);
                    cantidad++;
                }
            }

            return cantidad;
        }
    }
}
