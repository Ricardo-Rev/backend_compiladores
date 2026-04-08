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
public class RoverController : ControllerBase
{
    private readonly IMqttService            _mqtt;
    private readonly rover_db_context        _db;
    private readonly ILogger<RoverController> _logger;

    public RoverController(IMqttService mqtt, rover_db_context db, ILogger<RoverController> logger)
    {
        _mqtt   = mqtt;
        _db     = db;
        _logger = logger;
    }

    /// <summary>
    /// Envía las instrucciones de una compilación al rover vía MQTT.
    /// Requiere que la compilación exista y sea del usuario autenticado.
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(RoverExecuteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Execute([FromBody] RoverExecuteRequest request)
    {
        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(uid_str, out int usuario_id))
            return Unauthorized(new { error = "Token inválido." });

        // Verificar que la compilación existe y pertenece al usuario
        var compilacion = await _db.compilaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.id == request.compilacion_id && c.usuario_id == usuario_id);

        if (compilacion == null)
            return NotFound(new { error = "Compilación no encontrada." });

        if (compilacion.resultado != "exito")
            return BadRequest(new { error = "Solo se pueden enviar compilaciones exitosas al rover." });

        // Obtener sesión activa
        var sesion = await _db.sesiones
            .AsNoTracking()
            .Where(s => s.usuario_id == usuario_id && s.activa)
            .OrderByDescending(s => s.fecha_login)
            .Select(s => s.id)
            .FirstOrDefaultAsync();

        if (sesion == 0)
            return Unauthorized(new { error = "No hay sesión activa." });

        // Obtener instrucciones de la compilación
        var instrucciones = await _db.instrucciones_ejecutadas
            .AsNoTracking()
            .Where(i => i.compilacion_id == request.compilacion_id)
            .OrderBy(i => i.numero_orden)
            .ToListAsync();

        if (!instrucciones.Any())
            return BadRequest(new { error = "La compilación no tiene instrucciones para enviar." });

        // Convertir al formato que entiende el agente rover
        var payload = instrucciones.Select(i => new RoverInstruccionPayload
        {
            comando = MapearComando(i.instruccion_raw),
            params_ = ConstruirParams(i)
        }).ToList();

        // Publicar vía MQTT
        var enviado = await _mqtt.PublicarEjecucionAsync(request.compilacion_id, payload);

        // Registrar transmisión en BD
        var transmision = new transmision_rover_entity
        {
            compilacion_id   = request.compilacion_id,
            usuario_id       = usuario_id,
            lenguaje_destino = request.lenguaje_destino,
            estado_envio     = enviado ? "entregado" : "error",
            metodo_envio     = "inalambrico",
            mensaje_respuesta = enviado ? "Publicado en MQTT correctamente." : "Error al publicar en MQTT.",
            fecha_envio      = DateTime.Now,
            fecha_respuesta  = DateTime.Now
        };

        _db.transmisiones_rover.Add(transmision);

        // Bitácora
        _db.bitacora_acciones.Add(new bitacora_accion_entity
        {
            usuario_id  = usuario_id,
            sesion_id   = sesion,
            tipo_accion = "enviar_rover",
            descripcion = $"Compilación #{request.compilacion_id} enviada al rover. Estado: {transmision.estado_envio}",
            fecha_accion = DateTime.Now
        });

        await _db.SaveChangesAsync();

        if (!enviado)
            return StatusCode(503, new { error = "No se pudo publicar en MQTT. El rover puede estar desconectado." });

        return Ok(new RoverExecuteResponse
        {
            exitoso             = true,
            mensaje             = $"Instrucciones enviadas al rover correctamente.",
            transmision_id      = transmision.id,
            compilacion_id      = request.compilacion_id,
            total_instrucciones = instrucciones.Count
        });
    }

    /// <summary>
    /// Envía señal de parada de emergencia al rover.
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(typeof(RoverStopResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Stop()
    {
        var enviado = await _mqtt.PublicarStopAsync();

        return Ok(new RoverStopResponse
        {
            exitoso = enviado,
            mensaje = enviado ? "Señal STOP enviada al rover." : "Error al enviar STOP."
        });
    }

    /// <summary>
    /// Retorna si el broker MQTT está conectado.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Status()
    {
        return Ok(new
        {
            mqtt_conectado = _mqtt.EstaConectado,
            timestamp      = DateTime.UtcNow
        });
    }

    // ── Helpers de mapeo ────────────────────────────────────
    private static string MapearComando(string raw)
    {
        if (raw.StartsWith("avanzar_ctms")) return "AVANZAR_CTMS";
        if (raw.StartsWith("avanzar_vlts")) return "AVANZAR_VLTS";
        if (raw.StartsWith("avanzar_mts"))  return "AVANZAR_MTS";
        if (raw.StartsWith("girar"))        return "GIRAR";
        if (raw.StartsWith("circulo"))      return "CIRCULO";
        if (raw.StartsWith("cuadrado"))     return "CUADRADO";
        if (raw.StartsWith("rotar"))        return "ROTAR";
        if (raw.StartsWith("caminar"))      return "CAMINAR";
        if (raw.StartsWith("moonwalk"))     return "MOONWALK";
        return raw.ToUpper();
    }

    private static Dictionary<string, int> ConstruirParams(instruccion_ejecutada_entity i)
    {
        var p = new Dictionary<string, int>();
        if (i.parametro_n.HasValue) p["n"] = i.parametro_n.Value;
        if (i.parametro_r.HasValue) p["r"] = i.parametro_r.Value;
        if (i.parametro_l.HasValue) p["l"] = i.parametro_l.Value;
        return p;
    }
}