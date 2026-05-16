using umg_basic_rover_application.DTOs;

namespace umg_basic_rover_application.Contracts;

// ============================================================
//  ICredentialService
// ============================================================
public interface ICredentialService
{
    Task<CredentialResponse>          GenerarYEnviarAsync(int usuario_id);
    Task<CredentialResponse>          ReenviarAsync(int usuario_id);
    Task<VerificarCredencialResponse> VerificarCredencialAsync(byte[] pdf_bytes);
}

// ============================================================
//  IFileService
// ============================================================
public interface IFileService
{
    Task<FileResponse>           CrearAsync(CreateFileRequest request, int usuario_id);
    Task<FileResponse>           ObtenerAsync(int archivo_id, int usuario_id);
    Task<List<FileListResponse>> ListarAsync(int usuario_id);
    Task<FileResponse>           ActualizarAsync(int archivo_id, UpdateFileRequest request, int usuario_id);
    Task                         EliminarAsync(int archivo_id, int usuario_id);
    Task<List<FileListResponse>> ObtenerHistorialAsync(int archivo_id, int usuario_id);
}

// ============================================================
//  IChoreoService
//  Gestión de coreografías — endpoints públicos (conductor)
//  y endpoints de administración.
// ============================================================
public interface IChoreoService
{
    // ── Público ──────────────────────────────────────────────
    /// <summary>Lista coreografías activas para el menú del editor.</summary>
    Task<List<ChoreoListResponse>> ListarAsync();

    /// <summary>Obtiene una coreografía completa con código UMG++ y URL de canción.</summary>
    Task<ChoreoResponse> ObtenerAsync(int coreografia_id);

    /// <summary>Registra la ejecución de una coreografía y compila si hay código modificado.</summary>
    Task<ChoreoResponse> EjecutarAsync(ChoreoExecuteRequest request, int usuario_id, int sesion_id);

    // ── Administración ────────────────────────────────────────
    /// <summary>Lista todas las coreografías (activas e inactivas) para el panel admin.</summary>
    Task<List<ChoreoAdminItem>> ListarAdminAsync();

    /// <summary>
    /// Crea una nueva coreografía.
    /// cancion_url debe ser URL directa a MP3/OGG con CORS habilitado.
    /// </summary>
    Task<ChoreoResponse> CrearAsync(ChoreoCreateRequest request, int creado_por);

    /// <summary>
    /// Actualiza campos de una coreografía existente.
    /// Solo actualiza los campos enviados (null = sin cambio).
    /// Para limpiar cancion_url enviar limpiar_cancion = true.
    /// </summary>
    Task<ChoreoResponse> ActualizarAsync(int coreografia_id, ChoreoUpdateRequest request);

    /// <summary>Soft-delete de una coreografía (activa = false).</summary>
    Task EliminarAsync(int coreografia_id);
}