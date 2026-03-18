namespace umg_basic_rover_application.Contracts;

// ============================================================
//  INTERFAZ: IRecaptchaService
//  Contrato para validar tokens de Google reCAPTCHA v2.
//
//  FLUJO DE RECAPTCHA v2:
//  1. Frontend muestra el widget "No soy un robot"
//  2. Usuario resuelve el captcha
//  3. Google genera un token (válido por 2 minutos)
//  4. Frontend envía el token junto al formulario
//  5. Backend llama a esta interfaz para validar el token con Google
//  6. Google responde: success=true/false
//  7. Si es válido → continuar, si no → rechazar el request
// ============================================================

public interface IRecaptchaService
{
    /// <summary>
    /// Valida un token de reCAPTCHA v2 con la API de Google.
    /// </summary>
    /// <param name="token">
    /// Token generado por el frontend al resolver el captcha.
    /// Se recibe en el campo "recaptcha_token" del request.
    /// </param>
    /// <returns>
    /// true → Token válido, el usuario pasó la verificación.
    /// false → Token inválido, expirado o de origen sospechoso.
    /// </returns>
    Task<bool> ValidateAsync(string token);
}
