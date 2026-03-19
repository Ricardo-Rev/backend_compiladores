using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "conductor,administrador")]
[Produces("application/json")]
public class CompilerController : ControllerBase
{
    private readonly ICompilerService _compiler;
    private readonly rover_db_context _db;
    private readonly ILogger<CompilerController> _logger;

    public CompilerController(ICompilerService compiler, rover_db_context db, ILogger<CompilerController> logger)
    {
        _compiler = compiler;
        _db       = db;
        _logger   = logger;
    }

    /// <summary>
    /// Compila código UMG++. Ejecuta Léxico → Sintáctico → Semántico → Transpilador.
    /// </summary>
    /// <remarks>
    /// Ejemplo de código UMG++ válido:
    ///
    ///     PROGRAM mi_ruta
    ///     BEGIN
    ///       avanzar_mts(5);
    ///       girar(1);
    ///       circulo(50);
    ///     END.
    ///
    /// Modos disponibles: solo_compilar | compilar_simular | compilar_ejecutar
    /// Lenguajes destino: python | csharp
    /// </remarks>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(CompileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Analyze([FromBody] CompileRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { error = "Datos inválidos.", detalles = errors });
        }

        var modos = new[] { "solo_compilar", "compilar_simular", "compilar_ejecutar" };
        if (!modos.Contains(request.modo))
            return BadRequest(new { error = $"Modo inválido. Use: {string.Join(", ", modos)}" });

        var lenguajes = new[] { "python", "csharp" };
        if (!lenguajes.Contains(request.lenguaje_destino))
            return BadRequest(new { error = $"Lenguaje inválido. Use: {string.Join(", ", lenguajes)}" });

        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(uid_str, out int usuario_id))
            return Unauthorized(new { error = "Token inválido." });

        var sesion = await _db.sesiones
            .AsNoTracking()
            .Where(s => s.usuario_id == usuario_id && s.activa)
            .OrderByDescending(s => s.fecha_login)
            .FirstOrDefaultAsync();

        if (sesion == null)
            return Unauthorized(new { error = "No hay sesión activa." });

        try
        {
            // Registrar acción en bitácora
            _db.bitacora_acciones.Add(new bitacora_accion_entity
            {
                usuario_id   = usuario_id,
                sesion_id    = sesion.id,
                tipo_accion  = request.modo == "solo_compilar" ? "compilar" : request.modo,
                descripcion  = $"Compilación iniciada. Modo: {request.modo}",
                fecha_accion = DateTime.Now
            });
            await _db.SaveChangesAsync();

            var resultado = await _compiler.CompileAsync(request, usuario_id, sesion.id);

            if (!resultado.exitoso)
            {
                _db.bitacora_acciones.Add(new bitacora_accion_entity
                {
                    usuario_id   = usuario_id,
                    sesion_id    = sesion.id,
                    tipo_accion  = "error_compilacion",
                    descripcion  = $"Error: {resultado.resultado}. Total errores: {resultado.errores.Count}",
                    fecha_accion = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[COMPILER] Error inesperado al compilar.");
            return StatusCode(500, new { error = "Error interno del compilador." });
        }
    }

    /// <summary>
    /// Retorna el historial de compilaciones del usuario autenticado.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(uid_str, out int usuario_id))
            return Unauthorized();

        var historial = await _db.compilaciones
            .AsNoTracking()
            .Where(c => c.usuario_id == usuario_id)
            .OrderByDescending(c => c.fecha_compilacion)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(c => new
            {
                c.id,
                c.modo_compilacion,
                c.resultado,
                c.tiempo_compilacion_ms,
                c.fecha_compilacion,
                total_tokens  = c.tokens.Count,
                total_errores = c.errores.Count
            })
            .ToListAsync();

        var total = await _db.compilaciones.CountAsync(c => c.usuario_id == usuario_id);
        return Ok(new { page, size, total, data = historial });
    }
}
