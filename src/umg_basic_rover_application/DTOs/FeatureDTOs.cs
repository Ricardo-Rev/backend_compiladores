using System.ComponentModel.DataAnnotations;

namespace umg_basic_rover_application.DTOs;

// ============================================================
//  CredentialDTOs.cs
// ============================================================

public class CredentialResponse
{
    public int      credencial_id    { get; set; }
    public string   estado_envio     { get; set; } = string.Empty;
    public string?  archivo_base64   { get; set; }
    public bool     email_enviado    { get; set; }
    public bool     whatsapp_enviado { get; set; }
    public DateTime fecha_generacion { get; set; }
}

// ── NUEVO ─────────────────────────────────────────────────
public class VerificarCredencialResponse
{
    public bool     valido      { get; set; }
    public string   mensaje     { get; set; } = string.Empty;
    public string   algoritmo   { get; set; } = string.Empty;
    public DateTime? fecha_firma { get; set; }
}

// ============================================================
//  FileDTOs.cs
// ============================================================

public class CreateFileRequest
{
    [Required(ErrorMessage = "El nombre del archivo es obligatorio.")]
    [MaxLength(200)]
    public string nombre_archivo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El contenido es obligatorio.")]
    public string contenido { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? descripcion { get; set; }
}

public class UpdateFileRequest
{
    [Required]
    public string contenido { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? comentario { get; set; }
    public bool    guardar_historial { get; set; } = false;
}

public class FileResponse
{
    public int      id                 { get; set; }
    public string   nombre_archivo     { get; set; } = string.Empty;
    public string   contenido          { get; set; } = string.Empty;
    public int      version            { get; set; }
    public string?  descripcion        { get; set; }
    public bool     es_coreografia     { get; set; }
    public DateTime fecha_creacion     { get; set; }
    public DateTime fecha_modificacion { get; set; }
}

public class FileListResponse
{
    public int      id                 { get; set; }
    public string   nombre_archivo     { get; set; } = string.Empty;
    public int      version            { get; set; }
    public string?  descripcion        { get; set; }
    public bool     es_coreografia     { get; set; }
    public DateTime fecha_modificacion { get; set; }
}

// ============================================================
//  ChoreoDTOs.cs
// ============================================================

public class ChoreoResponse
{
    public int      id               { get; set; }
    public string   nombre           { get; set; } = string.Empty;
    public string?  descripcion      { get; set; }
    public string   codigo_fuente    { get; set; } = string.Empty;
    public string?  cancion_url      { get; set; }
    public string?  cancion_nombre   { get; set; }
    public int      duracion_min_seg { get; set; }
    public string?  comandos_arduino { get; set; }  // comandos seriales para el rover
}

public class ChoreoListResponse
{
    public int     id               { get; set; }
    public string  nombre           { get; set; } = string.Empty;
    public string? descripcion      { get; set; }
    public string? cancion_nombre   { get; set; }
    public int     duracion_min_seg { get; set; }
}

public class ChoreoExecuteRequest
{
    [Required]
    public int coreografia_id { get; set; }

    public bool modificada { get; set; } = false;

    public string? codigo_modificado { get; set; }
}

// ============================================================
//  FaceDTOs.cs
// ============================================================

public class FaceSegmentRequest
{
    public string image_base64 { get; set; } = string.Empty;
}

public class FaceSegmentResponse
{
    public bool    success   { get; set; }
    public string? resultado { get; set; }
    public string? mensaje   { get; set; }
    public string? error     { get; set; }
}