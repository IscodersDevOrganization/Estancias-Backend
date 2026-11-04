// Agregar como Areas/Core/Controllers/MorososEndpointController.cs.
// Convive con MorososController (MVC): no reemplazar el controlador de las vistas.
// Requiere EstanciasContext, los modelos DAL y la misma versión EPPlus del proyecto.
// Registrar attribute routing (endpoints.MapControllers() en Startup, si falta).
// [Authorize] usa la autenticación por defecto: aplicar la política/roles del módulo.
// Con autenticación por cookies, configurar antiforgery para las escrituras,
// como en el MVC original, y enviar el token desde el cliente.
// No se ha compilado: sólo se proporcionó el controlador MVC, sin .csproj/modelos.

using DAL.Data;
using DAL.Models;
using EstanciasCore.API.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EstanciasCore.Areas.Core.Controllers
{
    // Aplicar además la política/roles de acceso de Morosos de la aplicación.
    [Authorize]
    [ApiController]
    [TypeFilter(typeof(EndpointUatAuthorizeAttribute))]
    [Route("endpoint/morosos")]
    public class MorososEndpointController : ControllerBase
    {
        private const string ExcelContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly EstanciasContext _context;

        public MorososEndpointController(EstanciasContext context)
        {
            _context = context;
        }

        // GET /endpoint/morosos?busqueda=123&pagina=1&tamanioPagina=20
        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] string busqueda = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 20)
        {
            if (pagina < 1 || tamanioPagina < 1 || tamanioPagina > 100)
                return BadRequest(new { mensaje = "Página mínima: 1. Tamaño de página: de 1 a 100." });

            var desplazamiento = ((long)pagina - 1) * tamanioPagina;
            if (desplazamiento > int.MaxValue)
                return BadRequest(new { mensaje = "La página solicitada excede el límite permitido." });

            var query = _context.Morosos.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                busqueda = busqueda.Trim();
                query = query.Where(m => m.DNI.Contains(busqueda) ||
                    (m.NombreCompleto != null && m.NombreCompleto.Contains(busqueda)));
            }

            var total = await query.CountAsync();
            var datos = await query.OrderByDescending(m => m.FechaCarga)
                .ThenByDescending(m => m.Id)
                .Skip((int)desplazamiento).Take(tamanioPagina)
                .Select(m => new { m.Id, m.DNI, m.NombreCompleto, m.FechaCarga })
                .ToListAsync();

            return Ok(new
            {
                datos,
                total,
                pagina,
                tamanioPagina,
                totalPaginas = (int)Math.Ceiling(total / (double)tamanioPagina)
            });
        }

        // GET /endpoint/morosos/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obtener(int id)
        {
            var registro = await _context.Morosos.AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new { m.Id, m.DNI, m.NombreCompleto, m.FechaCarga })
                .FirstOrDefaultAsync();

            if (registro == null)
                return NotFound(new { mensaje = "No se encontró el moroso." });

            return Ok(registro);
        }

        // POST /endpoint/morosos
        // Mantiene _Create: si el DNI ya existe, actualiza ese registro.
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] MorosoGuardarRequest model)
        {
            var dni = model.DNI.Trim();
            var entity = await _context.Morosos.FirstOrDefaultAsync(m => m.DNI == dni);
            var creado = entity == null;

            if (creado)
            {
                entity = new Morosos { DNI = dni };
                _context.Morosos.Add(entity);
            }

            entity.NombreCompleto = model.NombreCompleto.Trim();
            entity.FechaCarga = DateTime.Now;
            var usuariosActualizados = await MarcarUsuariosIncobrable(dni, true);
            await _context.SaveChangesAsync();

            var respuesta = new
            {
                mensaje = creado ? "Registro creado correctamente." : "Registro actualizado correctamente.",
                usuariosActualizados,
                datos = new { entity.Id, entity.DNI, entity.NombreCompleto, entity.FechaCarga }
            };

            if (creado)
                return CreatedAtAction(nameof(Obtener), new { id = entity.Id }, respuesta);

            return Ok(respuesta);
        }

        // PUT /endpoint/morosos/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Editar(int id, [FromBody] MorosoGuardarRequest model)
        {
            var entity = await _context.Morosos.FindAsync(id);
            if (entity == null)
                return NotFound(new { mensaje = "No se encontró el moroso." });

            var dni = model.DNI.Trim();
            if (await _context.Morosos.AnyAsync(m => m.DNI == dni && m.Id != id))
                return Conflict(new { mensaje = "Ya existe otro moroso con ese DNI." });

            var oldDni = entity.DNI;
            entity.DNI = dni;
            entity.NombreCompleto = model.NombreCompleto.Trim();
            entity.FechaCarga = DateTime.Now;

            if (oldDni != dni &&
                !await _context.Morosos.AnyAsync(m => m.DNI == oldDni && m.Id != id))
            {
                await MarcarUsuariosIncobrable(oldDni, false);
            }

            await MarcarUsuariosIncobrable(dni, true);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Morosos.AsNoTracking().AnyAsync(m => m.Id == id))
                    return NotFound(new { mensaje = "El registro fue eliminado por otro proceso." });
                return Conflict(new { mensaje = "El registro cambió. Volvé a consultarlo antes de guardar." });
            }

            return Ok(new
            {
                mensaje = "Registro actualizado correctamente.",
                datos = new { entity.Id, entity.DNI, entity.NombreCompleto, entity.FechaCarga }
            });
        }

        // DELETE /endpoint/morosos/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var entity = await _context.Morosos.FindAsync(id);
            if (entity == null)
                return NotFound(new { mensaje = "No se encontró el moroso." });

            _context.Morosos.Remove(entity);
            if (!await _context.Morosos.AnyAsync(m => m.DNI == entity.DNI && m.Id != id))
                await MarcarUsuariosIncobrable(entity.DNI, false);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST /endpoint/morosos/importar (multipart/form-data, campo: archivo)
        // Reemplaza el listado vigente; NO agrega solamente los registros del archivo.
        [HttpPost("importar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Importar([FromForm] IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
                return BadRequest(new { mensaje = "Seleccioná un archivo .xlsx o .csv válido." });

            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".csv")
                return BadRequest(new { mensaje = "Formato no soportado. Usá .xlsx o .csv." });

            List<(string DNI, string NombreCompleto)> registros;
            List<string> errores;
            int total;
            int fallidos;
            int omitidos;

            try
            {
                if (extension == ".xlsx")
                    (registros, errores, total, fallidos, omitidos) = await LeerExcel(archivo);
                else
                    (registros, errores, total, fallidos, omitidos) = await LeerCsv(archivo);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException ||
                                       ex is System.Xml.XmlException)
            {
                return BadRequest(new { mensaje = "No se pudo interpretar el archivo. Revisá su formato." });
            }

            // No sincronizar un listado parcial: borraría DNI de las filas rechazadas.
            if (fallidos > 0 || registros.Count == 0)
                return BadRequest(new
                {
                    mensaje = "No se importó el archivo. Debe contener registros válidos y ninguna fila fallida.",
                    registrosTotales = total,
                    fallidos,
                    omitidos,
                    errores
                });

            // La sincronización y el historial se guardan juntos o se revierten juntos.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                var resultado = await SincronizarMorosos(registros);
                var identidad = User.Identity?.Name;
                var usuario = string.IsNullOrWhiteSpace(identidad) ? null :
                    await _context.Usuarios.FirstOrDefaultAsync(u =>
                        u.UserName == identidad || u.Email == identidad);

                // El código original usa Name; si JWT usa otro claim, adaptar esta búsqueda.
                if (usuario == null)
                    return Conflict(new { mensaje = "No se pudo identificar al usuario para registrar la importación." });

                var observacion = "Archivo: '" + Path.GetFileName(archivo.FileName) + "'";
                if (errores.Any()) observacion += " Detalle: " + string.Join(" | ", errores);

                _context.HistoricoDeCargaMorosos.Add(new HistoricoDeCargaMorosos
                {
                    RegistrosTotales = total,
                    Cargados = registros.Count,
                    Fallidos = fallidos,
                    Omitidos = omitidos,
                    Observacion = observacion,
                    Usuario = usuario,
                    FechaCarga = DateTime.Now
                });

                await _context.SaveChangesAsync();
                transaction.Commit();

                return Ok(new
                {
                    mensaje = "Sincronización completada.",
                    registrosTotales = total,
                    cargados = registros.Count,
                    fallidos,
                    omitidos,
                    errores,
                    usuariosMarcados = resultado.Marcados,
                    usuariosDesmarcados = resultado.Desmarcados,
                    registrosEliminados = resultado.Eliminados
                });
            }
        }

        // GET /endpoint/morosos/historial
        [HttpGet("historial")]
        public async Task<IActionResult> Historial()
        {
            // Proyección explícita: no serializar la entidad Usuario completa.
            var historial = await _context.HistoricoDeCargaMorosos.AsNoTracking()
                .OrderByDescending(h => h.FechaCarga)
                .Select(h => new
                {
                    h.RegistrosTotales,
                    h.Cargados,
                    h.Fallidos,
                    h.Omitidos,
                    h.Observacion,
                    h.FechaCarga,
                    usuario = h.Usuario != null ? h.Usuario.UserName : null
                }).ToListAsync();
            return Ok(historial);
        }

        // GET: Core/Morosos/DescargarPlantilla
        [HttpGet("plantilla")]
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
                return File(stream, ExcelContentType, "Plantilla_Morosos.xlsx");
            }
        }

        // GET: Core/Morosos/DescargarListadoVigente
        [HttpGet("listado-vigente")]
        public async Task<IActionResult> DescargarListadoVigente()
        {
            var listado = await _context.Morosos.AsNoTracking().OrderBy(m => m.NombreCompleto).ToListAsync();

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
                return File(stream, ExcelContentType, $"Listado_Morosos_Vigentes_{DateTime.Now:yyyyMMdd}.xlsx");
            }
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
                stream.Position = 0;
                using (var package = new ExcelPackage(stream))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null) throw new InvalidDataException("No se encontraron hojas en el archivo Excel.");
                    if (!EsEncabezadoValido(worksheet.Cells[1, 1].Text, worksheet.Cells[1, 2].Text))
                        throw new InvalidDataException("El encabezado debe ser DNI y NombreCompleto, en ese orden.");

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
                int row = 0;
                char separator = ',';
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    row++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (isFirstRow)
                    {
                        separator = line.IndexOf(';') >= 0 ? ';' : ',';
                        var encabezado = ParsearFilaCsv(line, separator);
                        if (encabezado.Count != 2 || !EsEncabezadoValido(encabezado[0], encabezado[1]))
                            throw new InvalidDataException("El encabezado debe ser DNI y NombreCompleto, en ese orden.");
                        isFirstRow = false;
                        continue;
                    }

                    registrosTotales++;
                    List<string> values;
                    try
                    {
                        values = ParsearFilaCsv(line, separator);
                    }
                    catch (InvalidDataException)
                    {
                        fallidos++;
                        errores.Add($"Fila {row}: CSV inválido. No se admiten campos con saltos de línea.");
                        continue;
                    }

                    if (values.Count == 2)
                    {
                        var dni = values[0]?.Trim();
                        var nombreCompleto = values.Count >= 2 ? values[1]?.Trim() : "";

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

        private static bool EsEncabezadoValido(string dni, string nombre)
        {
            return string.Equals(dni?.Trim(), "DNI", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(nombre?.Trim(), "NombreCompleto", StringComparison.OrdinalIgnoreCase);
        }

        // CSV de dos columnas: admite separadores dentro de comillas y comillas escapadas.
        // Los campos multilínea se rechazan para no sincronizar una lectura parcial.
        private static List<string> ParsearFilaCsv(string line, char separator)
        {
            var campos = new List<string>();
            var actual = new System.Text.StringBuilder();
            var entreComillas = false;
            var comillasCerradas = false;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (entreComillas)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            actual.Append('"');
                            i++;
                        }
                        else
                        {
                            entreComillas = false;
                            comillasCerradas = true;
                        }
                    }
                    else actual.Append(c);
                }
                else if (c == separator)
                {
                    campos.Add(actual.ToString().Trim());
                    actual.Clear();
                    comillasCerradas = false;
                }
                else if (c == '"')
                {
                    if (actual.ToString().Trim().Length > 0 || comillasCerradas)
                        throw new InvalidDataException("Comillas inválidas en CSV.");
                    actual.Clear();
                    entreComillas = true;
                }
                else
                {
                    if (comillasCerradas && !char.IsWhiteSpace(c))
                        throw new InvalidDataException("Texto después del cierre de comillas.");
                    actual.Append(c);
                }
            }
            if (entreComillas) throw new InvalidDataException("Campo CSV sin cierre de comillas.");
            campos.Add(actual.ToString().Trim());
            return campos;
        }

        private async Task<(int Marcados, int Desmarcados, int Eliminados)> SincronizarMorosos(
            List<(string DNI, string NombreCompleto)> registros)
        {
            var dnis = registros.Select(r => r.DNI).ToHashSet();
            var existentes = await _context.Morosos.ToListAsync();
            // Agrupar tolera duplicados heredados del controlador MVC.
            var porDni = existentes.GroupBy(m => m.DNI).ToDictionary(g => g.Key, g => g.ToList());
            var fecha = DateTime.Now;

            foreach (var registro in registros)
            {
                if (porDni.TryGetValue(registro.DNI, out var coincidencias))
                {
                    foreach (var entity in coincidencias)
                    {
                        if (!string.IsNullOrWhiteSpace(registro.NombreCompleto))
                            entity.NombreCompleto = registro.NombreCompleto;
                        entity.FechaCarga = fecha;
                    }
                }
                else
                {
                    _context.Morosos.Add(new Morosos
                    {
                        DNI = registro.DNI,
                        NombreCompleto = registro.NombreCompleto,
                        FechaCarga = fecha
                    });
                }
            }

            var eliminados = existentes.Where(m => !dnis.Contains(m.DNI)).ToList();
            _context.Morosos.RemoveRange(eliminados);

            // Mantiene el criterio original: documento de Persona y, si es null, UserName.
            var usuarios = await _context.Usuarios.Include(u => u.Personas)
                .Where(u => u.Incobrable ||
                    (u.Personas != null && dnis.Contains(u.Personas.NroDocumento)) ||
                    dnis.Contains(u.UserName))
                .ToListAsync();

            int marcados = 0, desmarcados = 0;
            foreach (var usuario in usuarios)
            {
                var dni = usuario.Personas?.NroDocumento ?? usuario.UserName;
                var incobrable = !string.IsNullOrEmpty(dni) && dnis.Contains(dni);
                if (usuario.Incobrable == incobrable) continue;
                usuario.Incobrable = incobrable;
                if (incobrable) marcados++; else desmarcados++;
            }

            // El llamador guarda morosos, usuarios e historial en una única transacción.
            return (marcados, desmarcados, eliminados.Count);
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

    // El Id viene en la URL y FechaCarga la asigna el servidor.
    public class MorosoGuardarRequest
    {
        [Required(AllowEmptyStrings = false)]
        public string DNI { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string NombreCompleto { get; set; }
    }
}
