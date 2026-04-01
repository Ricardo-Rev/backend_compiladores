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
//  POST /api/credential/generate  → generar y enviar (llamado post-registro)
//  POST /api/credential/resend    → reenviar credencial
//  GET  /api/credential/mine      → ver mi credencial actual
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

    /// <summary>
    /// Genera y envía la credencial PDF al usuario autenticado.
    /// Se puede llamar justo después del registro exitoso.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Generate()
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized(new { error = "Token inválido." });

        try
        {
            _logger.LogInformation("[CREDENTIAL-CTRL] Generando credencial para usuario {u}", usuario_id);
            var resultado = await _credential.GenerarYEnviarAsync(usuario_id);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL-CTRL] Error al generar credencial.");
            return StatusCode(500, new { error = "Error al generar la credencial." });
        }
    }

    /// <summary>
    /// Reenvía la credencial PDF al usuario (última generada o genera una nueva).
    /// </summary>
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

    /// <summary>
    /// Obtiene el estado de la última credencial generada para el usuario.
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Mine()
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized(new { error = "Token inválido." });

        var credencial = await _db.credenciales_pdf
            .AsNoTracking()
            .Where(c => c.usuario_id == usuario_id)
            .OrderByDescending(c => c.fecha_generacion)
            .Select(c => new
            {
                c.id,
                c.canal_envio,
                c.estado_envio,
                c.fecha_generacion,
                c.fecha_envio,
                tiene_pdf = c.archivo_base64 != null
            })
            .FirstOrDefaultAsync();

        if (credencial == null)
            return NotFound(new { error = "No se ha generado una credencial aún." });

        return Ok(credencial);
    }
    
    /// <summary>
    /// Verifica si un PDF de credencial es auténtico.
    /// </summary>
    [HttpPost("verify")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VerificarCredencialResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verify(IFormFile? pdf)
    {
        if (pdf is null || pdf.Length == 0)
            return BadRequest(new { error = "Archivo PDF requerido." });

        if (!pdf.ContentType.Contains("pdf") &&
            !pdf.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "El archivo debe ser un PDF." });

        try
        {
            using var ms = new MemoryStream();
            await pdf.CopyToAsync(ms);
            var pdf_bytes = ms.ToArray();

            _logger.LogInformation("[CREDENTIAL-CTRL] Verificando PDF: {nombre} ({bytes} bytes)",
                pdf.FileName, pdf_bytes.Length);

            var resultado = await _credential.VerificarCredencialAsync(pdf_bytes);
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
//  GET    /api/file            → listar archivos del usuario
//  GET    /api/file/{id}       → obtener archivo por ID
//  POST   /api/file            → crear archivo .umgpp
//  PUT    /api/file/{id}       → actualizar archivo
//  DELETE /api/file/{id}       → eliminar archivo (soft delete)
//  GET    /api/file/{id}/history → historial de versiones
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

    /// <summary>Lista todos los archivos .umgpp del usuario autenticado.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<FileListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();

        var archivos = await _file_service.ListarAsync(usuario_id);
        return Ok(archivos);
    }

    /// <summary>Obtiene el contenido completo de un archivo .umgpp.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();

        try
        {
            var archivo = await _file_service.ObtenerAsync(id, usuario_id);
            return Ok(archivo);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Crea un nuevo archivo .umgpp. El nombre puede incluir o no la extensión.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFileRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();

        try
        {
            // Registrar acción en bitácora
            await RegistrarAccionAsync(usuario_id, "nuevo_archivo", $"Creando archivo: {request.nombre_archivo}");

            var archivo = await _file_service.CrearAsync(request, usuario_id);

            await RegistrarAccionAsync(usuario_id, "guardar_archivo", $"Archivo creado: {archivo.nombre_archivo} (ID: {archivo.id})");

            return CreatedAtAction(nameof(Get), new { id = archivo.id }, archivo);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FILE-CTRL] Error al crear archivo.");
            return StatusCode(500, new { error = "Error al crear el archivo." });
        }
    }

    /// <summary>Actualiza el contenido de un archivo. Guarda la versión anterior en historial.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFileRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();

        try
        {
            var archivo = await _file_service.ActualizarAsync(id, request, usuario_id);
            await RegistrarAccionAsync(usuario_id, "guardar_archivo",
                $"Archivo actualizado: {archivo.nombre_archivo} → v{archivo.version}");
            return Ok(archivo);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FILE-CTRL] Error al actualizar archivo {id}.", id);
            return StatusCode(500, new { error = "Error al actualizar el archivo." });
        }
    }

    /// <summary>Elimina un archivo (soft delete). Las compilaciones asociadas se mantienen.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();

        try
        {
            await _file_service.EliminarAsync(id, usuario_id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Retorna el historial de versiones de un archivo.</summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(List<FileListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(int id)
    {
        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized();

        try
        {
            var historial = await _file_service.ObtenerHistorialAsync(id, usuario_id);
            return Ok(historial);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
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
            var sesion = await _db.sesiones
                .AsNoTracking()
                .Where(s => s.usuario_id == usuario_id && s.activa)
                .OrderByDescending(s => s.fecha_login)
                .Select(s => s.id)
                .FirstOrDefaultAsync();

            if (sesion == 0) return;

            _db.bitacora_acciones.Add(new umg_basic_rover_domain.entities.bitacora_accion_entity
            {
                usuario_id  = usuario_id,
                sesion_id   = sesion,
                tipo_accion = tipo_accion,
                descripcion = descripcion,
                fecha_accion = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
        catch { /* No romper el flujo principal si la bitácora falla */ }
    }
}

// ============================================================
//  ChoreoController
//  GET  /api/choreo           → listar coreografías disponibles
//  GET  /api/choreo/{id}      → obtener coreografía con código
//  POST /api/choreo/execute   → ejecutar/registrar ejecución
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

    /// <summary>
    /// Lista las coreografías pregrabadas disponibles.
    /// Las siembra automáticamente si aún no existen en BD.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ChoreoListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var coreografias = await _choreo.ListarAsync();
        return Ok(coreografias);
    }

    /// <summary>
    /// Obtiene una coreografía con su código UMG++ completo para cargar en el editor.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ChoreoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        try
        {
            var coreo = await _choreo.ObtenerAsync(id);

            // Bitácora
            await RegistrarCargaCoreografiaAsync(id);

            return Ok(coreo);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Registra la ejecución de una coreografía.
    /// Si viene código modificado, lo compila y simula también.
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(ChoreoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Execute([FromBody] ChoreoExecuteRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var usuario_id = ObtenerUsuarioId();
        if (usuario_id == 0) return Unauthorized(new { error = "Token inválido." });

        var sesion = await _db.sesiones
            .AsNoTracking()
            .Where(s => s.usuario_id == usuario_id && s.activa)
            .OrderByDescending(s => s.fecha_login)
            .Select(s => s.id)
            .FirstOrDefaultAsync();

        if (sesion == 0)
            return Unauthorized(new { error = "No hay sesión activa." });

        try
        {
            var resultado = await _choreo.EjecutarAsync(request, usuario_id, sesion);
            return Ok(resultado);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CHOREO-CTRL] Error al ejecutar coreografía {id}.", request.coreografia_id);
            return StatusCode(500, new { error = "Error al ejecutar la coreografía." });
        }
    }

    private int ObtenerUsuarioId()
    {
        var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(uid_str, out int id) ? id : 0;
    }

    private async Task RegistrarCargaCoreografiaAsync(int coreografia_id)
    {
        try
        {
            var usuario_id = ObtenerUsuarioId();
            if (usuario_id == 0) return;

            var sesion = await _db.sesiones
                .AsNoTracking()
                .Where(s => s.usuario_id == usuario_id && s.activa)
                .OrderByDescending(s => s.fecha_login)
                .Select(s => s.id)
                .FirstOrDefaultAsync();
            if (sesion == 0) return;

            _db.bitacora_acciones.Add(new umg_basic_rover_domain.entities.bitacora_accion_entity
            {
                usuario_id   = usuario_id,
                sesion_id    = sesion,
                tipo_accion  = "cargar_coreografia",
                descripcion  = $"Coreografía ID:{coreografia_id} cargada en editor.",
                fecha_accion = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
        catch { /* No romper el flujo si la bitácora falla */ }
    }
}
