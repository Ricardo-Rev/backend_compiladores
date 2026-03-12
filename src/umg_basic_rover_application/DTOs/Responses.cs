namespace umg_basic_rover_application.DTOs;

// ============================================================
//  DTOs DE SALIDA (Responses)
//  Definen los datos que la API devuelve al frontend.
//
//  SEGURIDAD:
//  - NUNCA se devuelve password_hash ni avatar_base64
//  - El access_token solo se devuelve en login/registro exitoso
//  - Los datos sensibles (telefono, ip) no se exponen en /me
// ============================================================


// ----------------------------------------------------------
//  AuthResponse: Respuesta al login o registro exitoso
// ----------------------------------------------------------
public class AuthResponse
{
    /// <summary>
    /// Token JWT para autenticar futuras peticiones.
    /// Enviar en el header: Authorization: Bearer {access_token}
    /// </summary>
    public string access_token { get; set; } = string.Empty;

    /// <summary>
    /// Segundos de validez del token. Ejemplo: 3600 = 1 hora.
    /// El frontend debe renovar o redirigir al login cuando expire.
    /// </summary>
    public int expires_in_seconds { get; set; }

    /// <summary>
    /// Información básica y segura del usuario autenticado.
    /// </summary>
    public UserDto user { get; set; } = null!;
}


// ----------------------------------------------------------
//  UserDto: Vista segura del usuario (sin datos sensibles)
//  Basado en los campos de la tabla [usuarios] de la BD
// ----------------------------------------------------------
public class UserDto
{
    /// <summary>ID numérico del usuario (INT de la BD).</summary>
    public int id { get; set; }

    /// <summary>Nombre de usuario único. Ej: "jperez2025"</summary>
    public string usuario { get; set; } = string.Empty;

    /// <summary>Nombre completo. Ej: "Juan Pérez García"</summary>
    public string nombre_completo { get; set; } = string.Empty;

    /// <summary>Correo electrónico.</summary>
    public string email { get; set; } = string.Empty;

    /// <summary>Rol del usuario: 'conductor' o 'administrador'.</summary>
    public string rol { get; set; } = string.Empty;

    /// <summary>URL de la foto de perfil (si tiene).</summary>
    public string? avatar_url { get; set; }

    /// <summary>Fecha de registro de la cuenta.</summary>
    public DateTime fecha_creacion { get; set; }
}
