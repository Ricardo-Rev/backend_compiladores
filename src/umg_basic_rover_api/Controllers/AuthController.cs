using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;

namespace umg_basic_rover_api.Controllers;

// ============================================================
//  CONTROLADOR: AuthController
//  Expone los endpoints HTTP del sistema de autenticación.
//
//  BASE URL: /api/auth
//
//  ENDPOINTS DISPONIBLES:
//  ┌─────────────────────────────────────────────────────────┐
//  │ POST   /api/auth/register  → Crear nueva cuenta         │
//  │ POST   /api/auth/login     → Iniciar sesión             │
//  │ POST   /api/auth/logout    → Cerrar sesión              │
//  │ GET    /api/auth/me        → Info del usuario actual    │
//  └─────────────────────────────────────────────────────────┘
//
//  AUTENTICACIÓN:
//  Los endpoints marcados con [Authorize] requieren el header:
//  Authorization: Bearer {tu_token_jwt}
//
//  DOCUMENTACIÓN INTERACTIVA:
//  Disponible en Swagger: http://localhost:{puerto}/swagger
// ============================================================

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IRecaptchaService _recaptcha;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService auth,
        IRecaptchaService recaptcha,
        ILogger<AuthController> logger)
    {
        _auth      = auth;
        _recaptcha = recaptcha;
        _logger    = logger;
    }

    // ============================================================
    //  POST /api/auth/register
    // ============================================================

    /// <summary>
    /// Registra un nuevo usuario en el sistema.
    /// Valida el reCAPTCHA antes de crear la cuenta.
    /// </summary>
    /// <remarks>
    /// EJEMPLO DE REQUEST:
    /// <code>
    /// POST /api/auth/register
    /// Content-Type: application/json
    ///
    /// {
    ///   "name": "Juan Pérez",
    ///   "email": "juan.perez@universidad.edu.gt",
    ///   "password": "MiPassword123",
    ///   "recaptcha_token": "03AGdBq25..."
    /// }
    /// </code>
    ///
    /// EJEMPLO DE RESPUESTA EXITOSA (200):
    /// <code>
    /// {
    ///   "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///   "expires_in_seconds": 3600,
    ///   "user": {
    ///     "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "name": "Juan Pérez",
    ///     "email": "juan.perez@universidad.edu.gt",
    ///     "fecha_creacion": "2025-03-09T12:00:00Z"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    /// <param name="dto">Datos del nuevo usuario + token de reCAPTCHA.</param>
    /// <returns>Token JWT y datos del usuario creado.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest dto)
    {
        try
        {
            // 1. Validar campos del formulario (anotaciones de DataAnnotations)
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);

                _logger.LogWarning("[REGISTER] ❌ Validación fallida: {Errors}", string.Join(" | ", errors));
                return BadRequest(new { error = "Datos inválidos.", detalles = errors });
            }

            // 2. Validar reCAPTCHA con la API de Google
            _logger.LogInformation("[REGISTER] 🔍 Validando reCAPTCHA...");
            var captcha_valido = await _recaptcha.ValidateAsync(dto.recaptcha_token);

            if (!captcha_valido)
            {
                _logger.LogWarning("[REGISTER] ❌ reCAPTCHA inválido.");
                return BadRequest(new { error = "Verificación de seguridad fallida. Por favor, vuelve a intentarlo." });
            }

            _logger.LogInformation("[REGISTER] ✅ reCAPTCHA válido. Procesando registro...");

            // 3. Registrar al usuario
            var response = await _auth.RegisterAsync(dto);

            _logger.LogInformation("[REGISTER] ✅ Registro exitoso: {Email}", dto.email);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // Error de negocio conocido (ej: email duplicado)
            _logger.LogWarning("[REGISTER] ⚠️ Error de negocio: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REGISTER] ❌ Error inesperado para {Email}", dto?.email);
            return StatusCode(500, new { error = "Error interno del servidor. Intenta más tarde." });
        }
    }

    // ============================================================
    //  POST /api/auth/login
    // ============================================================

    /// <summary>
    /// Inicia sesión con email y contraseña.
    /// Valida el reCAPTCHA antes de verificar las credenciales.
    /// </summary>
    /// <remarks>
    /// EJEMPLO DE REQUEST:
    /// <code>
    /// POST /api/auth/login
    /// Content-Type: application/json
    ///
    /// {
    ///   "email": "juan.perez@universidad.edu.gt",
    ///   "password": "MiPassword123",
    ///   "recaptcha_token": "03AGdBq25..."
    /// }
    /// </code>
    ///
    /// EJEMPLO DE RESPUESTA EXITOSA (200):
    /// <code>
    /// {
    ///   "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///   "expires_in_seconds": 3600,
    ///   "user": {
    ///     "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "name": "Juan Pérez",
    ///     "email": "juan.perez@universidad.edu.gt",
    ///     "fecha_creacion": "2025-03-09T12:00:00Z"
    ///   }
    /// }
    /// </code>
    ///
    /// USO DEL TOKEN EN SIGUIENTES REQUESTS:
    /// <code>
    /// GET /api/auth/me
    /// Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
    /// </code>
    /// </remarks>
    /// <param name="dto">Credenciales del usuario + token de reCAPTCHA.</param>
    /// <returns>Token JWT y datos del usuario autenticado.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest dto)
    {
        try
        {
            // 1. Validar campos del formulario
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);

                _logger.LogWarning("[LOGIN] ❌ Validación fallida: {Errors}", string.Join(" | ", errors));
                return BadRequest(new { error = "Datos inválidos.", detalles = errors });
            }

            // 2. Validar reCAPTCHA con la API de Google
            _logger.LogInformation("[LOGIN] 🔍 Validando reCAPTCHA...");
            var captcha_valido = await _recaptcha.ValidateAsync(dto.recaptcha_token);

            if (!captcha_valido)
            {
                _logger.LogWarning("[LOGIN] ❌ reCAPTCHA inválido.");
                return BadRequest(new { error = "Verificación de seguridad fallida. Por favor, vuelve a intentarlo." });
            }

            _logger.LogInformation("[LOGIN] ✅ reCAPTCHA válido. Verificando credenciales...");

            // 3. Verificar credenciales
            var response = await _auth.LoginAsync(dto);

            _logger.LogInformation("[LOGIN] ✅ Login exitoso: {Email}", dto.email);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            // IMPORTANTE: No revelar si el email existe o no.
            // Siempre responder con el mismo mensaje genérico.
            _logger.LogWarning("[LOGIN] ❌ Credenciales inválidas para: {Email}", dto?.email);
            return Unauthorized(new { error = "Credenciales inválidas." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LOGIN] ❌ Error inesperado para {Email}", dto?.email);
            return StatusCode(500, new { error = "Error interno del servidor. Intenta más tarde." });
        }
    }

    // ============================================================
    //  POST /api/auth/logout
    // ============================================================

    /// <summary>
    /// Cierra la sesión del usuario autenticado.
    /// Revoca el token JWT actual en la base de datos.
    /// </summary>
    /// <remarks>
    /// EJEMPLO DE REQUEST:
    /// <code>
    /// POST /api/auth/logout
    /// Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
    /// </code>
    ///
    /// RESPUESTA EXITOSA: 204 No Content (sin body)
    ///
    /// NOTA: Después del logout, el token queda inválido.
    /// Cualquier request con ese token retornará 401 Unauthorized.
    /// </remarks>
    /// <returns>204 No Content al cerrar sesión correctamente.</returns>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var bearer = Request.Headers.Authorization.ToString();
            var user_id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation("[LOGOUT] 🚪 Usuario {UserId} cerrando sesión.", user_id);

            await _auth.LogoutAsync(bearer);

            _logger.LogInformation("[LOGOUT] ✅ Sesión cerrada correctamente para {UserId}.", user_id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LOGOUT] ❌ Error inesperado al cerrar sesión.");
            return StatusCode(500, new { error = "Error al cerrar sesión." });
        }
    }

    // ============================================================
    //  GET /api/auth/me
    // ============================================================

    /// <summary>
    /// Retorna la información del usuario actualmente autenticado.
    /// Los datos se extraen del token JWT (no consulta la BD).
    /// </summary>
    /// <remarks>
    /// EJEMPLO DE REQUEST:
    /// <code>
    /// GET /api/auth/me
    /// Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
    /// </code>
    ///
    /// EJEMPLO DE RESPUESTA (200):
    /// <code>
    /// {
    ///   "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///   "name": "Juan Pérez",
    ///   "email": "juan.perez@universidad.edu.gt"
    /// }
    /// </code>
    /// </remarks>
    /// <returns>Datos básicos del usuario autenticado.</returns>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        try
        {
            var id    = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name  = User.FindFirstValue(ClaimTypes.Name);
            var email = User.FindFirstValue(ClaimTypes.Email);

            _logger.LogDebug("[ME] Consulta de info para usuario {UserId}.", id);

            return Ok(new
            {
                id    = id,
                name  = name,
                email = email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ME] ❌ Error inesperado.");
            return StatusCode(500, new { error = "Error al obtener información del usuario." });
        }
    }
}
