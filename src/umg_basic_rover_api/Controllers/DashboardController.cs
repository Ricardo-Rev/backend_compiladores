using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "administrador")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly rover_db_context            _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(rover_db_context db, ILogger<DashboardController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════
    //  GET /api/Dashboard/sessions
    //  (original intacto + nombre_completo)
    // ════════════════════════════════════════════════════════
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
                    id_ingreso      = b.id,
                    nickname        = b.usuario.usuario,
                    nombre_completo = b.usuario.nombre_completo,
                    avatar_url      = b.usuario.avatar_url,
                    avatar_base64   = b.usuario.avatar_base64,
                    metodo_login    = b.metodo_login,
                    ip_origen       = b.ip_origen,
                    fecha_ingreso   = b.fecha_ingreso,
                    fecha_salida    = b.fecha_salida
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

    // ════════════════════════════════════════════════════════
    //  GET /api/Dashboard/users
    //  (original intacto + filtro, nombre_completo, email_confirmado)
    // ════════════════════════════════════════════════════════
    [HttpGet("users")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int    page   = 1,
        [FromQuery] int    size   = 50,
        [FromQuery] string filtro = "todos")
    {
        try
        {
            var query = _db.usuarios
                .AsNoTracking()
                .Where(u => u.rol == "conductor");

            if (filtro == "activos")   query = query.Where(u => u.activo);
            if (filtro == "inactivos") query = query.Where(u => !u.activo);

            var total = await query.CountAsync();

            var aspirantes = await query
                .OrderByDescending(u => u.fecha_creacion)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(u => new AspiranteDto
                {
                    id_elegido          = u.id,
                    nickname            = u.usuario,
                    nombre_completo     = u.nombre_completo,
                    avatar_url          = u.avatar_url,
                    avatar_base64       = u.avatar_base64,
                    email               = u.email,
                    activo              = u.activo,
                    email_confirmado    = u.email_confirmado,
                    fecha_creacion      = u.fecha_creacion,
                    total_compilaciones = _db.compilaciones.Count(c => c.usuario_id == u.id)
                })
                .ToListAsync();

            return Ok(new { page, size, total, filtro, data = aspirantes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al obtener usuarios.");
            return StatusCode(500, new { error = "Error al obtener usuarios." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  PUT /api/Dashboard/users/{id}/toggle
    //  Dar de baja o reactivar un conductor
    // ════════════════════════════════════════════════════════
    [HttpPut("users/{id}/toggle")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleUserStatus(int id)
    {
        try
        {
            var usuario = await _db.usuarios.FindAsync(id);

            if (usuario == null)
                return NotFound(new { error = "Conductor no encontrado." });

            if (usuario.rol == "administrador")
                return BadRequest(new { error = "No se puede modificar el estado de un administrador." });

            usuario.activo = !usuario.activo;

            if (!usuario.activo)
            {
                var sesiones_activas = await _db.sesiones
                    .Where(s => s.usuario_id == id && s.activa)
                    .ToListAsync();

                foreach (var s in sesiones_activas)
                    s.activa = false;

                _logger.LogInformation(
                    "[DASHBOARD] Admin dio de baja al usuario {id}. Sesiones revocadas: {n}",
                    id, sesiones_activas.Count);
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                mensaje      = usuario.activo ? "Conductor reactivado exitosamente." : "Conductor dado de baja exitosamente.",
                usuario_id   = id,
                nuevo_estado = usuario.activo ? "activo" : "inactivo"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al cambiar estado del usuario {id}.", id);
            return StatusCode(500, new { error = "Error al cambiar el estado del conductor." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  GET /api/Dashboard/stats
    //  Estadísticas generales
    // ════════════════════════════════════════════════════════
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var hoy         = DateTime.Today;
            var hace_7_dias = hoy.AddDays(-7);

            var total_conductores      = await _db.usuarios.CountAsync(u => u.rol == "conductor");
            var conductores_activos    = await _db.usuarios.CountAsync(u => u.rol == "conductor" && u.activo);
            var conductores_nuevos_7d  = await _db.usuarios.CountAsync(u => u.rol == "conductor" && u.fecha_creacion >= hace_7_dias);

            var total_compilaciones    = await _db.compilaciones.CountAsync();
            var compilaciones_hoy      = await _db.compilaciones.CountAsync(c => c.fecha_compilacion >= hoy);
            var compilaciones_exitosas = await _db.compilaciones.CountAsync(c => c.resultado == "exito");

            var sesiones_activas       = await _db.sesiones.CountAsync(s => s.activa);
            var total_accesos          = await _db.bitacora_accesos.CountAsync();
            var accesos_hoy            = await _db.bitacora_accesos.CountAsync(b => b.fecha_ingreso >= hoy);

            var total_coreografias     = await _db.coreografias.CountAsync(c => c.activa);
            var total_transmisiones    = await _db.transmisiones_rover.CountAsync();
            var transmisiones_exitosas = await _db.transmisiones_rover.CountAsync(t => t.estado_envio == "entregado");

            var compilaciones_por_dia = await _db.compilaciones
                .Where(c => c.fecha_compilacion >= hace_7_dias)
                .GroupBy(c => c.fecha_compilacion.Date)
                .Select(g => new { fecha = g.Key, total = g.Count() })
                .OrderBy(x => x.fecha)
                .ToListAsync();

            return Ok(new
            {
                conductores = new
                {
                    total        = total_conductores,
                    activos      = conductores_activos,
                    inactivos    = total_conductores - conductores_activos,
                    nuevos_7dias = conductores_nuevos_7d
                },
                compilaciones = new
                {
                    total      = total_compilaciones,
                    hoy        = compilaciones_hoy,
                    exitosas   = compilaciones_exitosas,
                    tasa_exito = total_compilaciones > 0
                        ? Math.Round((double)compilaciones_exitosas / total_compilaciones * 100, 1)
                        : 0.0
                },
                sesiones = new
                {
                    activas_ahora = sesiones_activas,
                    total_accesos,
                    accesos_hoy
                },
                rover = new
                {
                    total_envios         = total_transmisiones,
                    envios_exitosos      = transmisiones_exitosas,
                    coreografias_activas = total_coreografias
                },
                grafica_compilaciones = compilaciones_por_dia,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al obtener stats.");
            return StatusCode(500, new { error = "Error al obtener estadísticas." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  GET /api/Dashboard/compilations
    //  Compilaciones de todos los usuarios
    // ════════════════════════════════════════════════════════
    [HttpGet("compilations")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompilations(
        [FromQuery] int    page   = 1,
        [FromQuery] int    size   = 30,
        [FromQuery] string filtro = "todos")
    {
        try
        {
            var query = _db.compilaciones
                .AsNoTracking()
                .Include(c => c.usuario)
                .AsQueryable();

            if (filtro == "exito") query = query.Where(c => c.resultado == "exito");
            if (filtro == "error") query = query.Where(c => c.resultado != "exito");

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(c => c.fecha_compilacion)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(c => new
                {
                    c.id,
                    conductor           = c.usuario.usuario,
                    c.modo_compilacion,
                    c.resultado,
                    c.tiempo_compilacion_ms,
                    c.fecha_compilacion,
                    total_tokens        = c.tokens.Count,
                    total_errores       = c.errores.Count,
                    total_instrucciones = c.instrucciones.Count
                })
                .ToListAsync();

            return Ok(new { page, size, total, filtro, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al obtener compilaciones.");
            return StatusCode(500, new { error = "Error al obtener compilaciones." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  GET /api/Dashboard/choreographies
    //  Lista completa de coreografías con stats
    // ════════════════════════════════════════════════════════
    [HttpGet("choreographies")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChoreographies()
    {
        try
        {
            var data = await _db.coreografias
                .AsNoTracking()
                .OrderBy(c => c.nombre)
                .Select(c => new
                {
                    c.id,
                    c.nombre,
                    c.descripcion,
                    c.codigo_fuente,
                    c.cancion_url,
                    c.cancion_nombre,
                    duracion_min    = c.duracion_min_seg / 60,
                    duracion_seg    = c.duracion_min_seg % 60,
                    c.activa,
                    c.fecha_creacion,
                    veces_ejecutada = _db.coreografias_ejecutadas.Count(e => e.coreografia_id == c.id)
                })
                .ToListAsync();

            return Ok(new { total = data.Count, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al obtener coreografías.");
            return StatusCode(500, new { error = "Error al obtener coreografías." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  POST /api/Dashboard/choreographies
    //  Crear nueva coreografía (solo admin)
    // ════════════════════════════════════════════════════════
    [HttpPost("choreographies")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateChoreography([FromBody] CoreografiaCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Datos inválidos.", detalles = ModelState });

        // Validaciones básicas
        if (string.IsNullOrWhiteSpace(dto.nombre))
            return BadRequest(new { error = "El nombre es requerido." });

        if (string.IsNullOrWhiteSpace(dto.codigo_fuente))
            return BadRequest(new { error = "El código fuente es requerido." });

        if (dto.duracion_min_seg < 180)
            return BadRequest(new { error = "La duración mínima es 180 segundos (3 minutos)." });

        // Verificar nombre único
        var existe = await _db.coreografias.AnyAsync(c => c.nombre == dto.nombre);
        if (existe)
            return BadRequest(new { error = $"Ya existe una coreografía con el nombre '{dto.nombre}'." });

        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(uid_str, out int admin_id);

        try
        {
            var coreografia = new coreografia_entity
            {
                nombre          = dto.nombre.Trim(),
                descripcion     = dto.descripcion?.Trim(),
                codigo_fuente   = dto.codigo_fuente.Trim(),
                cancion_url     = dto.cancion_url?.Trim(),
                cancion_nombre  = dto.cancion_nombre?.Trim(),
                duracion_min_seg = dto.duracion_min_seg,
                creado_por      = admin_id > 0 ? admin_id : null,
                activa          = true,
                fecha_creacion  = DateTime.Now
            };

            _db.coreografias.Add(coreografia);
            await _db.SaveChangesAsync();

            _logger.LogInformation("[DASHBOARD] Coreografía '{nombre}' creada por admin {id}.", dto.nombre, admin_id);

            return StatusCode(201, new
            {
                mensaje         = "Coreografía creada exitosamente.",
                coreografia_id  = coreografia.id,
                nombre          = coreografia.nombre
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al crear coreografía.");
            return StatusCode(500, new { error = "Error al crear la coreografía." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  PUT /api/Dashboard/choreographies/{id}
    //  Editar coreografía existente (solo admin)
    // ════════════════════════════════════════════════════════
    [HttpPut("choreographies/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateChoreography(int id, [FromBody] CoreografiaUpdateDto dto)
    {
        try
        {
            var coreografia = await _db.coreografias.FindAsync(id);

            if (coreografia == null)
                return NotFound(new { error = "Coreografía no encontrada." });

            // Verificar nombre único si se está cambiando
            if (!string.IsNullOrWhiteSpace(dto.nombre) && dto.nombre != coreografia.nombre)
            {
                var nombre_duplicado = await _db.coreografias
                    .AnyAsync(c => c.nombre == dto.nombre && c.id != id);

                if (nombre_duplicado)
                    return BadRequest(new { error = $"Ya existe una coreografía con el nombre '{dto.nombre}'." });

                coreografia.nombre = dto.nombre.Trim();
            }

            // Validar duración si se cambia
            if (dto.duracion_min_seg.HasValue)
            {
                if (dto.duracion_min_seg.Value < 180)
                    return BadRequest(new { error = "La duración mínima es 180 segundos (3 minutos)." });

                coreografia.duracion_min_seg = dto.duracion_min_seg.Value;
            }

            // Actualizar solo los campos que vienen en el DTO
            if (dto.descripcion   != null) coreografia.descripcion   = dto.descripcion.Trim();
            if (dto.codigo_fuente != null) coreografia.codigo_fuente = dto.codigo_fuente.Trim();
            if (dto.cancion_url   != null) coreografia.cancion_url   = dto.cancion_url.Trim();
            if (dto.cancion_nombre != null) coreografia.cancion_nombre = dto.cancion_nombre.Trim();

            await _db.SaveChangesAsync();

            _logger.LogInformation("[DASHBOARD] Coreografía {id} '{nombre}' actualizada.", id, coreografia.nombre);

            return Ok(new
            {
                mensaje        = "Coreografía actualizada exitosamente.",
                coreografia_id = id,
                nombre         = coreografia.nombre
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al actualizar coreografía {id}.", id);
            return StatusCode(500, new { error = "Error al actualizar la coreografía." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  PUT /api/Dashboard/choreographies/{id}/toggle
    //  Activar o desactivar una coreografía
    // ════════════════════════════════════════════════════════
    [HttpPut("choreographies/{id}/toggle")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleChoreography(int id)
    {
        try
        {
            var coreografia = await _db.coreografias.FindAsync(id);

            if (coreografia == null)
                return NotFound(new { error = "Coreografía no encontrada." });

            coreografia.activa = !coreografia.activa;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[DASHBOARD] Coreografía {id} '{nombre}' → {estado}.",
                id, coreografia.nombre, coreografia.activa ? "activa" : "inactiva");

            return Ok(new
            {
                mensaje        = coreografia.activa ? "Coreografía activada." : "Coreografía desactivada.",
                coreografia_id = id,
                nuevo_estado   = coreografia.activa ? "activa" : "inactiva"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al cambiar estado de coreografía {id}.", id);
            return StatusCode(500, new { error = "Error al cambiar el estado de la coreografía." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  GET /api/Dashboard/notifications
    //  Historial de notificaciones enviadas
    // ════════════════════════════════════════════════════════
    [HttpGet("notifications")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int size = 30)
    {
        try
        {
            var total = await _db.historial_notificaciones.CountAsync();

            var data = await _db.historial_notificaciones
                .AsNoTracking()
                .Include(n => n.usuario)
                .OrderByDescending(n => n.fecha_envio)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(n => new
                {
                    n.id,
                    conductor = n.usuario.usuario,
                    n.tipo,
                    n.canal,
                    n.asunto,
                    n.estado,
                    n.fecha_envio
                })
                .ToListAsync();

            return Ok(new { page, size, total, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DASHBOARD] Error al obtener notificaciones.");
            return StatusCode(500, new { error = "Error al obtener notificaciones." });
        }
    }
}