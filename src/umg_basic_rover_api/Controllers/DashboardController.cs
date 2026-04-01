using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "administrador")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly rover_db_context _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(rover_db_context db, ILogger<DashboardController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos los ingresos y salidas de la plataforma (solo administradores).
    /// Ordenados del más reciente al más antiguo.
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions([FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        try
        {
            var accesos = await _db.bitacora_accesos
                .AsNoTracking()
                .Include(b => b.usuario)
                .OrderByDescending(b => b.fecha_ingreso)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(b => new AccesoDto
                {
                    id_ingreso    = b.id,
                    nickname      = b.usuario.usuario,
                    avatar_url    = b.usuario.avatar_url,
                    avatar_base64 = b.usuario.avatar_base64,
                    metodo_login  = b.metodo_login,
                    ip_origen     = b.ip_origen,
                    fecha_ingreso = b.fecha_ingreso,
                    fecha_salida  = b.fecha_salida
                })
                .ToListAsync();

            var total = await _db.bitacora_accesos.CountAsync();
            return Ok(new { page, size, total, data = accesos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al obtener sesiones.");
            return StatusCode(500, new { error = "Error al obtener sesiones." });
        }
    }

    /// <summary>
    /// Lista todos los aspirantes conductores (solo administradores).
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        try
        {
            var aspirantes = await _db.usuarios
                .AsNoTracking()
                .Where(u => u.rol == "conductor")
                .OrderByDescending(u => u.fecha_creacion)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(u => new AspiranteDto
                {
                    id_elegido          = u.id,
                    nickname            = u.usuario,
                    avatar_url          = u.avatar_url,
                    avatar_base64       = u.avatar_base64,
                    email               = u.email,
                    activo              = u.activo,
                    fecha_creacion      = u.fecha_creacion,
                    total_compilaciones = _db.compilaciones.Count(c => c.usuario_id == u.id)
                })
                .ToListAsync();

            var total = await _db.usuarios.CountAsync(u => u.rol == "conductor");
            return Ok(new { page, size, total, data = aspirantes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al obtener usuarios.");
            return StatusCode(500, new { error = "Error al obtener usuarios." });
        }
    }
}
