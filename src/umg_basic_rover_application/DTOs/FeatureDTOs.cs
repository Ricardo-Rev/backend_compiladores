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

// ── Respuestas públicas ──────────────────────────────────────
public class ChoreoResponse
{
    public int      id               { get; set; }
    public string   nombre           { get; set; } = string.Empty;
    public string?  descripcion      { get; set; }
    public string   codigo_fuente    { get; set; } = string.Empty;
    public string?  cancion_url      { get; set; }    // URL directa a archivo MP3/OGG
    public string?  cancion_nombre   { get; set; }
    public int      duracion_min_seg { get; set; }
    public string?  comandos_arduino { get; set; }
}

public class ChoreoListResponse
{
    public int     id               { get; set; }
    public string  nombre           { get; set; } = string.Empty;
    public string? descripcion      { get; set; }
    public string? cancion_nombre   { get; set; }
    public bool    tiene_cancion    { get; set; }     // indica si tiene audio cargado
    public int     duracion_min_seg { get; set; }
}

public class ChoreoExecuteRequest
{
    [Required]
    public int coreografia_id { get; set; }

    public bool modificada { get; set; } = false;

    public string? codigo_modificado { get; set; }
}

// ── Admin: crear coreografía ─────────────────────────────────
public class ChoreoCreateRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(200)]
    public string nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? descripcion { get; set; }

    [Required(ErrorMessage = "El código fuente UMG++ es obligatorio.")]
    public string codigo_fuente { get; set; } = string.Empty;

    /// <summary>
    /// URL directa a archivo de audio MP3 u OGG.
    /// Debe ser accesible públicamente y con CORS habilitado.
    /// Recomendado: Cloudinary, Supabase Storage, o cualquier CDN público.
    /// Ejemplo: https://res.cloudinary.com/tu-cuenta/video/upload/thriller.mp3
    /// </summary>
    [MaxLength(1000)]
    [Url(ErrorMessage = "Debe ser una URL válida.")]
    public string? cancion_url { get; set; }

    [MaxLength(200)]
    public string? cancion_nombre { get; set; }

    /// <summary>Duración estimada en segundos.</summary>
    [Range(10, 3600)]
    public int duracion_min_seg { get; set; } = 90;
}

// ── Admin: actualizar coreografía ────────────────────────────
public class ChoreoUpdateRequest
{
    [MaxLength(200)]
    public string? nombre { get; set; }

    [MaxLength(500)]
    public string? descripcion { get; set; }

    public string? codigo_fuente { get; set; }

    /// <summary>
    /// URL directa a archivo de audio. Enviar null para quitar la canción,
    /// omitir el campo para no cambiarla.
    /// </summary>
    [MaxLength(1000)]
    public string? cancion_url { get; set; }

    // Enviar true explícito para limpiar la URL de canción
    public bool limpiar_cancion { get; set; } = false;

    [MaxLength(200)]
    public string? cancion_nombre { get; set; }

    [Range(10, 3600)]
    public int? duracion_min_seg { get; set; }

    public bool? activa { get; set; }
}

// ── Admin: listado extendido ─────────────────────────────────
public class ChoreoAdminItem
{
    public int      id               { get; set; }
    public string   nombre           { get; set; } = string.Empty;
    public string?  descripcion      { get; set; }
    public string?  cancion_url      { get; set; }
    public string?  cancion_nombre   { get; set; }
    public bool     tiene_cancion    { get; set; }
    public int      duracion_min_seg { get; set; }
    public bool     activa           { get; set; }
    public int      total_ejecuciones { get; set; }
    public DateTime fecha_creacion   { get; set; }
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