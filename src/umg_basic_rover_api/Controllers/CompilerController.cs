using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_api.Controllers;

// ============================================================
//  CompilerController.cs — UMG Basic Rover 2.0
//
//  Endpoints del compilador UMG++:
//
//  POST /api/compiler/analyze  → Ejecuta el pipeline completo
//  GET  /api/compiler/history  → Historial de compilaciones
//
//  Todos los endpoints requieren JWT Bearer Token.
//  El pipeline sigue el orden:
//  Léxico → Sintáctico → Semántico → Transpilador → Simulación
// ============================================================

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CompilerController : ControllerBase
{
    private readonly ICompilerService _compiler;
    private readonly rover_db_context _db;
    private readonly ILogger<CompilerController> _logger;

    // Modos y lenguajes válidos
    private static readonly string[] MODOS_VALIDOS    = { "solo_compilar", "compilar_simular", "compilar_ejecutar" };
    private static readonly string[] LENGUAJES_VALIDOS = { "python", "csharp", "java", "cpp" };

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
    /// Modos disponibles   : solo_compilar | compilar_simular | compilar_ejecutar
    /// Lenguajes destino   : python | csharp | java | cpp
    /// </remarks>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(CompileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Analyze([FromBody] CompileRequest request)
    {
        // Validar modelo
        if (!ModelState.IsValid)
        {
            var errores = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { error = "Datos inválidos.", detalles = errores });
        }

        // Validar modo
        if (!MODOS_VALIDOS.Contains(request.modo))
            return BadRequest(new
            {
                error     = $"Modo '{request.modo}' no válido.",
                validos   = MODOS_VALIDOS,
                ejemplo   = "solo_compilar"
            });

        // Validar lenguaje destino
        var lenguaje = request.lenguaje_destino?.ToLower() ?? "python";
        if (!LENGUAJES_VALIDOS.Contains(lenguaje))
            return BadRequest(new
            {
                error   = $"Lenguaje '{lenguaje}' no soportado.",
                validos = LENGUAJES_VALIDOS,
                ejemplo = "python"
            });

        // Obtener usuario del JWT
        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(uid_str, out int usuario_id))
            return Unauthorized(new { error = "Token JWT inválido o expirado." });

        // Verificar sesión activa
        var sesion = await _db.sesiones
            .AsNoTracking()
            .Where(s => s.usuario_id == usuario_id && s.activa)
            .OrderByDescending(s => s.fecha_login)
            .FirstOrDefaultAsync();

        if (sesion == null)
            return Unauthorized(new { error = "No hay sesión activa. Inicie sesión nuevamente." });

        try
        {
            // Registrar inicio en bitácora
            _db.bitacora_acciones.Add(new bitacora_accion_entity
            {
                usuario_id   = usuario_id,
                sesion_id    = sesion.id,
                tipo_accion  = "compilacion_iniciada",
                descripcion  = $"Compilación iniciada. Modo: {request.modo} | Lenguaje: {lenguaje}",
                fecha_accion = DateTime.Now
            });
            await _db.SaveChangesAsync();

            // Ejecutar pipeline del compilador
            var resultado = await _compiler.CompileAsync(request, usuario_id, sesion.id);

            // Registrar resultado en bitácora
            if (!resultado.exitoso)
            {
                _db.bitacora_acciones.Add(new bitacora_accion_entity
                {
                    usuario_id   = usuario_id,
                    sesion_id    = sesion.id,
                    tipo_accion  = "compilacion_error",
                    descripcion  = $"Error: {resultado.resultado} | Total errores: {resultado.errores.Count}",
                    fecha_accion = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }
            else
            {
                _db.bitacora_acciones.Add(new bitacora_accion_entity
                {
                    usuario_id   = usuario_id,
                    sesion_id    = sesion.id,
                    tipo_accion  = "compilacion_exitosa",
                    descripcion  = $"Compilación exitosa en {resultado.tiempo_ms}ms | Instrucciones: {resultado.instrucciones.Count}",
                    fecha_accion = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[COMPILER] Error inesperado al compilar. Usuario: {u}", usuario_id);
            return StatusCode(500, new { error = "Error interno del compilador. Intente nuevamente." });
        }
    }

    /// <summary>
    /// Retorna el historial de compilaciones del usuario autenticado.
    /// Soporta paginación con los parámetros page y size.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(uid_str, out int usuario_id))
            return Unauthorized(new { error = "Token JWT inválido." });

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
                total_tokens       = c.tokens.Count,
                total_errores      = c.errores.Count,
                total_instrucciones = _db.instrucciones_ejecutadas.Count(i => i.compilacion_id == c.id)
            })
            .ToListAsync();

        var total = await _db.compilaciones.CountAsync(c => c.usuario_id == usuario_id);

        return Ok(new
        {
            page,
            size,
            total,
            total_pages = (int)Math.Ceiling((double)total / size),
            data        = historial
        });
    }
}