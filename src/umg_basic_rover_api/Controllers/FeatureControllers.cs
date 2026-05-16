using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_api.Controllers;

// ============================================================
//  CredentialController
//  POST /api/credential/generate  → generar y enviar
//  POST /api/credential/resend    → reenviar credencial
//  GET  /api/credential/mine      → ver mi credencial actual
//  POST /api/credential/verify    → verificar autenticidad PDF
// ============================================================

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CredentialController : ControllerBase
{
    private readonly ICredentialService              _credential;
    private readonly rover_db_context                _db;
    private readonly ILogger<CredentialController>   _logger;

    public CredentialController(ICredentialService credential, rover_db_context db, ILogger<CredentialController> logger)
    {
        _credential = credential;
        _db         = db;
        _logger     = logger;
    }

    [HttpPost("generate")]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate()
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized(new { error = "Token inválido." });
        try
        {
            var resultado = await _credential.GenerarYEnviarAsync(usuario_id);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL-CTRL] Error al generar credencial.");
            return StatusCode(500, new { error = "Error al generar la credencial." });
        }
    }

    [HttpPost("resend")]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resend()
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized(new { error = "Token inválido." });
        try
        {
            var resultado = await _credential.ReenviarAsync(usuario_id);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL-CTRL] Error al reenviar credencial.");
            return StatusCode(500, new { error = "Error al reenviar la credencial." });
        }
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine()
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized(new { error = "Token inválido." });

        var credencial = await _db.credenciales_pdf
            .AsNoTracking()
            .Where(c => c.usuario_id == usuario_id)
            .OrderByDescending(c => c.fecha_generacion)
            .Select(c => new { c.id, c.canal_envio, c.estado_envio, c.fecha_generacion, c.fecha_envio, tiene_pdf = c.archivo_base64 != null })
            .FirstOrDefaultAsync();

        return credencial == null
            ? NotFound(new { error = "No se ha generado una credencial aún." })
            : Ok(credencial);
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VerificarCredencialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verify(IFormFile? pdf)
    {
        if (pdf is null || pdf.Length == 0) return BadRequest(new { error = "Archivo PDF requerido." });
        if (!pdf.ContentType.Contains("pdf") && !pdf.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "El archivo debe ser un PDF." });
        try
        {
            using var ms = new MemoryStream();
            await pdf.CopyToAsync(ms);
            var resultado = await _credential.VerificarCredencialAsync(ms.ToArray());
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL-CTRL] Error al verificar credencial.");
            return StatusCode(500, new { error = "Error al verificar la credencial." });
        }
    }

    private int ObtenerUsuarioId()
    {
        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(uid_str, out int id) ? id : 0;
    }
}

// ============================================================
//  FileController
// ============================================================

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class FileController : ControllerBase
{
    private readonly IFileService              _file_service;
    private readonly rover_db_context          _db;
    private readonly ILogger<FileController>   _logger;

    public FileController(IFileService file_service, rover_db_context db, ILogger<FileController> logger)
    {
        _file_service = file_service;
        _db           = db;
        _logger       = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<FileListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();
        return Ok(await _file_service.ListarAsync(usuario_id));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(int id)
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();
        try { return Ok(await _file_service.ObtenerAsync(id, usuario_id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateFileRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();
        try
        {
            await RegistrarAccionAsync(usuario_id, "nuevo_archivo", $"Creando archivo: {request.nombre_archivo}");
            var archivo = await _file_service.CrearAsync(request, usuario_id);
            await RegistrarAccionAsync(usuario_id, "guardar_archivo", $"Archivo creado: {archivo.nombre_archivo} (ID: {archivo.id})");
            return CreatedAtAction(nameof(Get), new { id = archivo.id }, archivo);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FILE-CTRL] Error al crear archivo.");
            return StatusCode(500, new { error = "Error al crear el archivo." });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFileRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();
        try
        {
            var archivo = await _file_service.ActualizarAsync(id, request, usuario_id);
            await RegistrarAccionAsync(usuario_id, "guardar_archivo", $"Archivo actualizado: {archivo.nombre_archivo} → v{archivo.version}");
            return Ok(archivo);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FILE-CTRL] Error al actualizar archivo {id}.", id);
            return StatusCode(500, new { error = "Error al actualizar el archivo." });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id)
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();
        try { await _file_service.EliminarAsync(id, usuario_id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(List<FileListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(int id)
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();
        try { return Ok(await _file_service.ObtenerHistorialAsync(id, usuario_id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("{id:int}/history/{version:int}")]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> HistoryVersion(int id, int version)
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();
        try
        {
            var entrada = await _db.historial_archivos
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.archivo_id == id && h.version == version && h.usuario_id == usuario_id)
                ?? throw new KeyNotFoundException($"Versión {version} no encontrada.");
            return Ok(new FileResponse
            {
                id = entrada.id, nombre_archivo = $"v{entrada.version}", contenido = entrada.contenido,
                version = entrada.version, descripcion = entrada.comentario, es_coreografia = false,
                fecha_creacion = entrada.fecha_guardado, fecha_modificacion = entrada.fecha_guardado,
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    private int ObtenerUsuarioId()
    {
        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(uid_str, out int id) ? id : 0;
    }

    private async Task RegistrarAccionAsync(int usuario_id, string tipo_accion, string descripcion)
    {
        try
        {
            var sesion = await _db.sesiones.AsNoTracking()
                .Where(s => s.usuario_id == usuario_id && s.activa)
                .OrderByDescending(s => s.fecha_login).Select(s => s.id).FirstOrDefaultAsync();
            if (sesion == 0) return;
            _db.bitacora_acciones.Add(new umg_basic_rover_domain.entities.bitacora_accion_entity
            { usuario_id = usuario_id, sesion_id = sesion, tipo_accion = tipo_accion, descripcion = descripcion, fecha_accion = DateTime.Now });
            await _db.SaveChangesAsync();
        }
        catch { /* No romper el flujo principal */ }
    }
}

// ============================================================
//  ChoreoController
//
//  Público (conductor):
//    GET  /api/choreo           → listar coreografías activas
//    GET  /api/choreo/{id}      → obtener coreografía + cancion_url
//    POST /api/choreo/execute   → registrar ejecución
//
//  Administración (solo rol "administrador"):
//    GET    /api/choreo/admin        → listar todas (activas + inactivas)
//    POST   /api/choreo/admin        → crear nueva coreografía
//    PUT    /api/choreo/admin/{id}   → actualizar (cancion_url, código, etc.)
//    DELETE /api/choreo/admin/{id}   → desactivar (soft delete)
//
//  Para asignar una canción a una coreografía:
//    PUT /api/choreo/admin/{id}
//    Body: { "cancion_url": "https://res.cloudinary.com/.../cancion.mp3",
//            "cancion_nombre": "Thriller — Michael Jackson" }
// ============================================================

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ChoreoController : ControllerBase
{
    private readonly IChoreoService              _choreo;
    private readonly rover_db_context            _db;
    private readonly ILogger<ChoreoController>   _logger;

    public ChoreoController(IChoreoService choreo, rover_db_context db, ILogger<ChoreoController> logger)
    {
        _choreo = choreo;
        _db     = db;
        _logger = logger;
    }

    // ── Público ──────────────────────────────────────────────

    /// <summary>Lista las coreografías activas para el menú del editor.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ChoreoListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        return Ok(await _choreo.ListarAsync());
    }

    /// <summary>
    /// Obtiene una coreografía con su código UMG++ y la URL directa al archivo MP3.
    /// El frontend usa cancion_url en el elemento HTML5 &lt;audio&gt;.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ChoreoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        try
        {
            var coreo = await _choreo.ObtenerAsync(id);
            await RegistrarCargaAsync(id);
            return Ok(coreo);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Registra y opcionalmente re-compila la ejecución de una coreografía.</summary>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(ChoreoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Execute([FromBody] ChoreoExecuteRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized(new { error = "Token inválido." });
        var sesion = await _db.sesiones.AsNoTracking()
            .Where(s => s.usuario_id == usuario_id && s.activa)
            .OrderByDescending(s => s.fecha_login).Select(s => s.id).FirstOrDefaultAsync();
        if (sesion == 0) return Unauthorized(new { error = "No hay sesión activa." });
        try { return Ok(await _choreo.EjecutarAsync(request, usuario_id, sesion)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CHOREO-CTRL] Error al ejecutar coreografía {id}.", request.coreografia_id);
            return StatusCode(500, new { error = "Error al ejecutar la coreografía." });
        }
    }

    // ── Administración ────────────────────────────────────────

    /// <summary>
    /// Lista TODAS las coreografías (activas e inactivas) para el panel admin.
    /// Incluye URL de canción, total de ejecuciones y estado.
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "administrador")]
    [ProducesResponseType(typeof(List<ChoreoAdminItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminList()
    {
        return Ok(await _choreo.ListarAdminAsync());
    }

    /// <summary>
    /// Crea una nueva coreografía.
    /// Para agregar música: incluir cancion_url con URL directa al MP3.
    /// La URL debe ser accesible públicamente con CORS habilitado.
    /// Recomendado: Cloudinary (https://res.cloudinary.com/...).
    /// </summary>
    [HttpPost("admin")]
    [Authorize(Roles = "administrador")]
    [ProducesResponseType(typeof(ChoreoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminCreate([FromBody] ChoreoCreateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();
        try
        {
            var coreo = await _choreo.CrearAsync(request, usuario_id);
            _logger.LogInformation("[CHOREO-CTRL] Coreografía creada: {n} (cancion_url: {url})", coreo.nombre, coreo.cancion_url ?? "sin canción");
            return CreatedAtAction(nameof(Get), new { id = coreo.id }, coreo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CHOREO-CTRL] Error al crear coreografía.");
            return StatusCode(500, new { error = "Error al crear la coreografía." });
        }
    }

    /// <summary>
    /// Actualiza una coreografía existente.
    ///
    /// Para actualizar solo la canción:
    ///   { "cancion_url": "https://res.cloudinary.com/mi-cuenta/video/upload/cancion.mp3",
    ///     "cancion_nombre": "Thriller — Michael Jackson" }
    ///
    /// Para quitar la canción:
    ///   { "limpiar_cancion": true }
    ///
    /// Solo los campos enviados se actualizan (null = sin cambio).
    /// </summary>
    [HttpPut("admin/{id:int}")]
    [Authorize(Roles = "administrador")]
    [ProducesResponseType(typeof(ChoreoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminUpdate(int id, [FromBody] ChoreoUpdateRequest request)
    {
        try
        {
            var coreo = await _choreo.ActualizarAsync(id, request);
            _logger.LogInformation("[CHOREO-CTRL] Coreografía {id} actualizada (cancion_url: {url})", id, coreo.cancion_url ?? "sin canción");
            return Ok(coreo);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CHOREO-CTRL] Error al actualizar coreografía {id}.", id);
            return StatusCode(500, new { error = "Error al actualizar la coreografía." });
        }
    }

    /// <summary>Desactiva una coreografía (soft delete). No elimina datos ni ejecuciones.</summary>
    [HttpDelete("admin/{id:int}")]
    [Authorize(Roles = "administrador")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminDelete(int id)
    {
        try { await _choreo.EliminarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── Helpers ───────────────────────────────────────────────

    private int ObtenerUsuarioId()
    {
        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(uid_str, out int id) ? id : 0;
    }

    private async Task RegistrarCargaAsync(int coreografia_id)
    {
        try
        {
            var usuario_id = ObtenerUsuarioId();
            if (usuario_id == 0) return;
            var sesion = await _db.sesiones.AsNoTracking()
                .Where(s => s.usuario_id == usuario_id && s.activa)
                .OrderByDescending(s => s.fecha_login).Select(s => s.id).FirstOrDefaultAsync();
            if (sesion == 0) return;
            _db.bitacora_acciones.Add(new umg_basic_rover_domain.entities.bitacora_accion_entity
            { usuario_id = usuario_id, sesion_id = sesion, tipo_accion = "cargar_coreografia", descripcion = $"Coreografía ID:{coreografia_id} cargada en editor.", fecha_accion = DateTime.Now });
            await _db.SaveChangesAsync();
        }
        catch { /* No romper el flujo */ }
    }
}