using System.ComponentModel.DataAnnotations;

namespace umg_basic_rover_application.DTOs;

// ============================================================
//  DTOs DE ENTRADA (Requests)
//  Definen los datos que el frontend debe enviar a la API.
//
//  VALIDACIONES AUTOMÁTICAS:
//  [Required]       → Campo obligatorio (no puede ser null/vacío)
//  [EmailAddress]   → Formato de email válido (x@x.x)
//  [MinLength(n)]   → Mínimo n caracteres
//  [MaxLength(n)]   → Máximo n caracteres
//  [RegularExpression] → Patrón personalizado
//
//  NOTA SOBRE RECAPTCHA:
//  El frontend obtiene el recaptcha_token cuando el usuario
//  resuelve el widget "No soy un robot". El backend lo valida
//  con la API de Google antes de procesar cualquier acción.
// ============================================================


// ----------------------------------------------------------
//  RegisterRequest: Datos para crear una nueva cuenta
//  Corresponde a los campos de la tabla [usuarios] en BD
// ----------------------------------------------------------
public class RegisterRequest
{
    /// <summary>
    /// Nombre de usuario único en el sistema.
    /// Ejemplo: "jperez2025"
    /// </summary>
    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [MinLength(3, ErrorMessage = "El usuario debe tener al menos 3 caracteres.")]
    [MaxLength(50, ErrorMessage = "El usuario no puede exceder 50 caracteres.")]
    public string usuario { get; set; } = string.Empty;

    /// <summary>
    /// Correo electrónico único del usuario.
    /// Ejemplo: "juan.perez@universidad.edu.gt"
    /// </summary>
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    [MaxLength(100, ErrorMessage = "El correo no puede exceder 100 caracteres.")]
    public string email { get; set; } = string.Empty;

    /// <summary>
    /// Nombre completo del usuario.
    /// Ejemplo: "Juan Pérez García"
    /// </summary>
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MinLength(3, ErrorMessage = "El nombre completo debe tener al menos 3 caracteres.")]
    [MaxLength(150, ErrorMessage = "El nombre completo no puede exceder 150 caracteres.")]
    public string nombre_completo { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña en texto plano. El backend la hashea con BCrypt.
    /// Mínimo 8 caracteres para mayor seguridad.
    /// </summary>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [MaxLength(100, ErrorMessage = "La contraseña no puede exceder 100 caracteres.")]
    public string password { get; set; } = string.Empty;

    /// <summary>
    /// Número de teléfono del usuario.
    /// Ejemplo: "+502 1234-5678"
    /// </summary>
    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [MaxLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres.")]
    public string telefono { get; set; } = string.Empty;

    /// <summary>
    /// Token generado por Google reCAPTCHA v2.
    /// El frontend lo obtiene cuando el usuario completa el captcha.
    /// El backend valida este token con la API de Google.
    /// </summary>
    [Required(ErrorMessage = "El token de reCAPTCHA es obligatorio.")]
    public string recaptcha_token { get; set; } = string.Empty;

    /// <summary>
    /// Foto de perfil en base64 (opcional). 
    /// Se incluye en la credencial PDF si se proporciona.
    /// </summary>
    public string? avatar_base64 { get; set; }
}


// ----------------------------------------------------------
//  LoginRequest: Datos para iniciar sesión
// ----------------------------------------------------------
public class LoginRequest
{
    /// <summary>
    /// Correo electrónico del usuario registrado.
    /// Ejemplo: "juan.perez@universidad.edu.gt"
    /// </summary>
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    public string email { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña del usuario en texto plano.
    /// El backend la compara contra el hash almacenado en BD con BCrypt.
    /// </summary>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string password { get; set; } = string.Empty;

    /// <summary>
    /// Token de reCAPTCHA v2 generado en el frontend.
    /// Debe ser válido y no haber expirado (Google lo invalida a los 2 min).
    /// </summary>
    [Required(ErrorMessage = "El token de reCAPTCHA es obligatorio.")]
    public string recaptcha_token { get; set; } = string.Empty;
}
