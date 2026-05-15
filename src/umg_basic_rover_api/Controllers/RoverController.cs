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
    private readonly IMqttService             _mqtt;
    private readonly rover_db_context         _db;
    private readonly ILogger<RoverController> _logger;

    public RoverController(IMqttService mqtt, rover_db_context db, ILogger<RoverController> logger)
    {
        _mqtt   = mqtt;
        _db     = db;
        _logger = logger;
    }

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

        var comandos    = new List<string>();
        var omitidas    = new List<string>();   // instrucciones que se saltaron por parámetro null

        foreach (var inst in instrucciones)
        {
            var raw = inst.instruccion_raw?.Trim() ?? "";

            if (raw.Contains('+'))
            {
                var expandidos = ExpandirCombinada(raw);
                _logger.LogInformation("[ROVER] Combinada '{raw}' → {n} cmds: [{cmds}]",
                    raw, expandidos.Count, string.Join(", ", expandidos));
                comandos.AddRange(expandidos);
            }
            else
            {
                // [BUG-3] ConstruirSerialDesdeRaw ahora valida parámetros nullables
                var serial = ConstruirSerialDesdeRaw(raw, inst);
                if (!string.IsNullOrEmpty(serial))
                {
                    comandos.Add(serial);
                }
                else
                {
                    omitidas.Add(raw);
                    _logger.LogWarning(
                        "[ROVER] Instrucción omitida — raw='{raw}', " +
                        "parametro_n={n}, parametro_r={r}, parametro_l={l}. " +
                        "El parámetro requerido es null en la BD.",
                        raw, inst.parametro_n, inst.parametro_r, inst.parametro_l);
                }
            }
        }

        if (!comandos.Any())
            return BadRequest(new
            {
                error = "Ninguna instrucción pudo convertirse a comando serial. " +
                        $"Instrucciones omitidas: [{string.Join(", ", omitidas)}]."
            });

        if (omitidas.Any())
            _logger.LogWarning(
                "[ROVER] Compilación #{id}: {om} instrucción(es) omitida(s) por parámetros null: [{list}]",
                request.compilacion_id, omitidas.Count, string.Join(", ", omitidas));

        _logger.LogInformation(
            "[ROVER] Compilación #{id}: {orig} filas → {exp} comandos: [{cmds}]",
            request.compilacion_id, instrucciones.Count, comandos.Count,
            string.Join(", ", comandos));

        var enviado = await _mqtt.PublicarEjecucionAsync(request.compilacion_id, comandos);

        var transmision = new transmision_rover_entity
        {
            compilacion_id    = request.compilacion_id,
            usuario_id        = usuario_id,
            lenguaje_destino  = request.lenguaje_destino,
            estado_envio      = enviado ? "entregado" : "error",
            metodo_envio      = "inalambrico",
            mensaje_respuesta = enviado
                ? $"Publicado en MQTT: {comandos.Count} comandos. [{string.Join(", ", comandos)}]"
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
                           $"Comandos: {comandos.Count}. Estado: {transmision.estado_envio}",
            fecha_accion = DateTime.Now
        });

        await _db.SaveChangesAsync();

        if (!enviado)
            return StatusCode(503, new { error = "No se pudo publicar en MQTT. El rover puede estar desconectado." });

        return Ok(new RoverExecuteResponse
        {
            exitoso             = true,
            mensaje             = $"Instrucciones enviadas al rover. ({comandos.Count} comandos)" +
                                  (omitidas.Any() ? $" [{omitidas.Count} omitidas por parámetros null]" : ""),
            transmision_id      = transmision.id,
            compilacion_id      = request.compilacion_id,
            total_instrucciones = comandos.Count
        });
    }

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
    /// Extrae el nombre de instrucción desde instruccion_raw y construye
    /// el comando serial usando los parámetros ya guardados en la BD.
    ///
    /// [BUG-3] Los parámetros son int? (nullable). Si el requerido es null
    /// se retorna string.Empty para omitir la instrucción y loguear la anomalía,
    /// evitando enviar "GR:" o "CIR:" sin número (que el Arduino parsea como 0).
    ///
    /// Ejemplos:
    ///   raw="girar(-1)", parametro_n=-1  →  "GR:-1"
    ///   raw="girar(1)",  parametro_n=1   →  "GR:1"
    ///   raw="circulo(50)", parametro_r=50 → "CIR:50"
    ///   raw="girar(1)",  parametro_n=null → "" (omitida)
    /// </summary>
    private static string ConstruirSerialDesdeRaw(string raw, instruccion_ejecutada_entity inst)
    {
        var idx    = raw.IndexOf('(');
        var nombre = idx > 0 ? raw[..idx].Trim().ToLower() : raw.Trim().ToLower();

        return nombre switch
        {
            "avanzar_vlts"                  => inst.parametro_n.HasValue
                                                ? $"AV_VLT:{inst.parametro_n}"
                                                : string.Empty,

            "avanzar_ctms" or "avanzar_cms" => inst.parametro_n.HasValue
                                                ? $"AV_CM:{inst.parametro_n}"
                                                : string.Empty,

            "avanzar_mts"                   => inst.parametro_n.HasValue
                                                ? $"AV_MTS:{inst.parametro_n}"
                                                : string.Empty,

            "girar"                         => inst.parametro_n.HasValue
                                                ? $"GR:{inst.parametro_n}"
                                                : string.Empty,

            "circulo"                       => inst.parametro_r.HasValue
                                                ? $"CIR:{inst.parametro_r}"
                                                : string.Empty,

            "cuadrado"                      => inst.parametro_l.HasValue
                                                ? $"CUA:{inst.parametro_l}"
                                                : string.Empty,

            "rotar"                         => inst.parametro_n.HasValue
                                                ? $"ROT:{inst.parametro_n}"
                                                : string.Empty,

            "caminar"                       => inst.parametro_n.HasValue
                                                ? $"CAM:{inst.parametro_n}"
                                                : string.Empty,

            "moonwalk"                      => inst.parametro_n.HasValue
                                                ? $"MWK:{inst.parametro_n}"
                                                : string.Empty,

            _                               => string.Empty
        };
    }

    /// <summary>
    /// Expande "girar(-1) + avanzar_ctms(30)" en comandos seriales:
    ///   → ["GR:-1", "AV_CM:30"]
    /// int.TryParse ya filtra parámetros no numéricos de forma segura.
    /// </summary>
    private static List<string> ExpandirCombinada(string raw)
    {
        var resultado = new List<string>();
        var partes    = raw.Split('+', StringSplitOptions.RemoveEmptyEntries);

        foreach (var parte in partes)
        {
            var p         = parte.Trim().TrimEnd(';');
            var idxAbre   = p.IndexOf('(');
            var idxCierra = p.LastIndexOf(')');

            if (idxAbre < 0 || idxCierra <= idxAbre) continue;

            var nombre   = p[..idxAbre].Trim().ToLower();
            var paramStr = p[(idxAbre + 1)..idxCierra].Trim();

            if (!int.TryParse(paramStr, out int valor)) continue;

            var serial = nombre switch
            {
                "avanzar_vlts"                  => $"AV_VLT:{valor}",
                "avanzar_ctms" or "avanzar_cms" => $"AV_CM:{valor}",
                "avanzar_mts"                   => $"AV_MTS:{valor}",
                "girar"                         => $"GR:{valor}",
                "circulo"                       => $"CIR:{valor}",
                "cuadrado"                      => $"CUA:{valor}",
                "rotar"                         => $"ROT:{valor}",
                "caminar"                       => $"CAM:{valor}",
                "moonwalk"                      => $"MWK:{valor}",
                _                               => string.Empty
            };

            if (!string.IsNullOrEmpty(serial))
                resultado.Add(serial);
        }

        return resultado;
    }
}