using umg_basic_rover_application.DTOs;

namespace umg_basic_rover_application.Contracts;

// ============================================================
//  ICredentialService
//  Genera el PDF con firma electrónica y lo envía por
//  email y WhatsApp al usuario recién registrado.
// ============================================================
public interface ICredentialService
{
    /// <summary>
    /// Genera la credencial PDF firmada electrónicamente y
    /// la envía por email y WhatsApp al usuario.
    /// Se llama automáticamente después del registro exitoso.
    /// </summary>
    Task<CredentialResponse> GenerarYEnviarAsync(int usuario_id);

    /// <summary>
    /// Reenvía la credencial si el usuario la solicita de nuevo.
    /// </summary>
    Task<CredentialResponse> ReenviarAsync(int usuario_id);
}

// ============================================================
//  IFileService
//  CRUD de archivos .umgpp del usuario autenticado.
// ============================================================
public interface IFileService
{
    Task<FileResponse>            CrearAsync(CreateFileRequest request, int usuario_id);
    Task<FileResponse>            ObtenerAsync(int archivo_id, int usuario_id);
    Task<List<FileListResponse>>  ListarAsync(int usuario_id);
    Task<FileResponse>            ActualizarAsync(int archivo_id, UpdateFileRequest request, int usuario_id);
    Task                          EliminarAsync(int archivo_id, int usuario_id);
    Task<List<FileListResponse>>  ObtenerHistorialAsync(int archivo_id, int usuario_id);
}

// ============================================================
//  IChoreoService
//  Gestión de coreografías pregrabadas en UMG++.
// ============================================================
public interface IChoreoService
{
    Task<List<ChoreoListResponse>> ListarAsync();
    Task<ChoreoResponse>           ObtenerAsync(int coreografia_id);
    Task<ChoreoResponse>           EjecutarAsync(ChoreoExecuteRequest request, int usuario_id, int sesion_id);
}
