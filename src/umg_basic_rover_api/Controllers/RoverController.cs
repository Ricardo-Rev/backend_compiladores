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
    /// CORRECCIÓN: Las instrucciones combinadas (girar+avanzar) ahora se
    /// expanden correctamente en comandos individuales antes de publicar.
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

        var compilacion = await _db.compilaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.id == request.compilacion_id && c.usuario_id == usuario_id);

        if (compilacion == null)
            return NotFound(new { error = "Compilación no encontrada." });

        if (compilacion.resultado != "exito")
            return BadRequest(new { error = "Solo se pueden enviar compilaciones exitosas al rover." });

        var sesion = await _db.sesiones
            .AsNoTracking()
            .Where(s => s.usuario_id == usuario_id && s.activa)
            .OrderByDescending(s => s.fecha_login)
            .Select(s => s.id)
            .FirstOrDefaultAsync();

        if (sesion == 0)
            return Unauthorized(new { error = "No hay sesión activa." });

        var instrucciones = await _db.instrucciones_ejecutadas
            .AsNoTracking()
            .Where(i => i.compilacion_id == request.compilacion_id)
            .OrderBy(i => i.numero_orden)
            .ToListAsync();

        if (!instrucciones.Any())
            return BadRequest(new { error = "La compilación no tiene instrucciones para enviar." });

        // ── CORRECCIÓN CRÍTICA: expandir combinadas ──────────────────────────
        // ANTES: MapearComando("girar(1) + avanzar_ctms(30)") → solo "GIRAR"
        //        ConstruirParams para combinadas → diccionario vacío
        // AHORA: cada parte se convierte en un payload individual con sus params
        // ─────────────────────────────────────────────────────────────────────
        var payload = new List<RoverInstruccionPayload>();

        foreach (var inst in instrucciones)
        {
            var raw = inst.instruccion_raw?.Trim() ?? "";

            if (raw.Contains('+'))
            {
                var expandidas = ExpandirCombinada(raw);
                _logger.LogInformation("[ROVER] Combinada '{raw}' → {n} cmds", raw, expandidas.Count);
                payload.AddRange(expandidas);
            }
            else
            {
                payload.Add(new RoverInstruccionPayload
                {
                    comando = MapearComando(raw),
                    params_ = ConstruirParams(inst)
                });
            }
        }

        _logger.LogInformation(
            "[ROVER] Compilación #{id}: {orig} filas BD → {exp} comandos MQTT",
            request.compilacion_id, instrucciones.Count, payload.Count);

        var enviado = await _mqtt.PublicarEjecucionAsync(request.compilacion_id, payload);

        var transmision = new transmision_rover_entity
        {
            compilacion_id    = request.compilacion_id,
            usuario_id        = usuario_id,
            lenguaje_destino  = request.lenguaje_destino,
            estado_envio      = enviado ? "entregado" : "error",
            metodo_envio      = "inalambrico",
            mensaje_respuesta = enviado
                ? $"Publicado en MQTT: {payload.Count} comandos."
                : "Error al publicar en MQTT.",
            fecha_envio     = DateTime.Now,
            fecha_respuesta = DateTime.Now
        };

        _db.transmisiones_rover.Add(transmision);

        _db.bitacora_acciones.Add(new bitacora_accion_entity
        {
            usuario_id   = usuario_id,
            sesion_id    = sesion,
            tipo_accion  = "enviar_rover",
            descripcion  = $"Compilación #{request.compilacion_id} enviada. " +
                           $"Comandos: {payload.Count}. Estado: {transmision.estado_envio}",
            fecha_accion = DateTime.Now
        });

        await _db.SaveChangesAsync();

        if (!enviado)
            return StatusCode(503, new { error = "No se pudo publicar en MQTT. El rover puede estar desconectado." });

        return Ok(new RoverExecuteResponse
        {
            exitoso             = true,
            mensaje             = $"Instrucciones enviadas al rover. ({payload.Count} comandos)",
            transmision_id      = transmision.id,
            compilacion_id      = request.compilacion_id,
            total_instrucciones = payload.Count
        });
    }

    /// <summary>Parada de emergencia.</summary>
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

    /// <summary>Estado del MQTT y URL de la cámara.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Status()
    {
        return Ok(new
        {
            mqtt_conectado = _mqtt.EstaConectado,
            camara_url     = "https://rover.nexttechsolutionspc.xyz/?action=stream",
            snapshot_url   = "https://rover.nexttechsolutionspc.xyz/?action=snapshot",
            timestamp      = DateTime.UtcNow
        });
    }


    // ════════════════════════════════════════════════════════════════
    // HELPERS PRIVADOS
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Expande "girar(1) + avanzar_ctms(30)" en payloads individuales:
    ///   → [GIRAR n=1]  +  [AVANZAR_CTMS n=30]
    /// </summary>
    private static List<RoverInstruccionPayload> ExpandirCombinada(string raw)
    {
        var resultado = new List<RoverInstruccionPayload>();
        var partes    = raw.Split('+', StringSplitOptions.RemoveEmptyEntries);

        foreach (var parte in partes)
        {
            var p        = parte.Trim().TrimEnd(';');
            var idxAbre  = p.IndexOf('(');
            var idxCierra = p.LastIndexOf(')');

            if (idxAbre < 0 || idxCierra <= idxAbre) continue;

            var nombre   = p[..idxAbre].Trim().ToLower();
            var paramStr = p[(idxAbre + 1)..idxCierra].Trim();

            if (!int.TryParse(paramStr, out int valor)) continue;

            var cmd = MapearNombre(nombre);
            if (string.IsNullOrEmpty(cmd)) continue;

            var prms = new Dictionary<string, int>();
            switch (nombre)
            {
                case "circulo":  prms["r"] = valor; break;
                case "cuadrado": prms["l"] = valor; break;
                default:         prms["n"] = valor; break;
            }

            resultado.Add(new RoverInstruccionPayload { comando = cmd, params_ = prms });
        }

        return resultado;
    }

    /// <summary>Nombre UMG++ → comando MQTT del rover.</summary>
    private static string MapearNombre(string nombre) => nombre switch
    {
        "avanzar_ctms" or "avanzar_cms" => "AVANZAR_CTMS",
        "avanzar_vlts"                   => "AVANZAR_VLTS",
        "avanzar_mts"                    => "AVANZAR_MTS",
        "girar"                          => "GIRAR",
        "circulo"                        => "CIRCULO",
        "cuadrado"                       => "CUADRADO",
        "rotar"                          => "ROTAR",
        "caminar"                        => "CAMINAR",
        "moonwalk"                       => "MOONWALK",
        _                                => string.Empty
    };

    /// <summary>Extrae el nombre antes de '(' y lo mapea al comando MQTT.</summary>
    private static string MapearComando(string raw)
    {
        var idx    = raw.IndexOf('(');
        var nombre = idx > 0 ? raw[..idx].Trim().ToLower() : raw.Trim().ToLower();
        return MapearNombre(nombre);
    }

    /// <summary>Parámetros desde las columnas ya validadas en la BD.</summary>
    private static Dictionary<string, int> ConstruirParams(instruccion_ejecutada_entity i)
    {
        var p = new Dictionary<string, int>();
        if (i.parametro_n.HasValue) p["n"] = i.parametro_n.Value;
        if (i.parametro_r.HasValue) p["r"] = i.parametro_r.Value;
        if (i.parametro_l.HasValue) p["l"] = i.parametro_l.Value;
        return p;
    }
}
