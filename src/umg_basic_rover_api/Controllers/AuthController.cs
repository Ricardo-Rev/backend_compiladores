using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_api.Controllers;

// ============================================================
//  AuthController — VERSIÓN COMPLETA
//
//  CAMBIOS respecto a la versión original de Sergio:
//  ✅ GET /me → retorna campos completos (rol, usuario, avatar_url,
//               email_confirmado, telefono_confirmado, preferencias)
//  ✅ POST /register → la credencial PDF se dispara en AuthService
//                      (no hay cambio visible aquí, ya lo maneja el service)
//
//  LO QUE NO CAMBIÓ (de Sergio, estaba bien):
//  ✅ [AllowAnonymous] en register y login
//  ✅ [Authorize] en logout y me
//  ✅ Validación reCAPTCHA
//  ✅ ModelState.IsValid
//  ✅ Manejo diferenciado de excepciones
//  ✅ Logging estructurado
// ============================================================

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService              _auth;
    private readonly IRecaptchaService         _recaptcha;
    private readonly rover_db_context          _db;
    private readonly ILogger<AuthController>   _logger;

    public AuthController(
        IAuthService            auth,
        IRecaptchaService       recaptcha,
        rover_db_context        db,
        ILogger<AuthController> logger)
    {
        _auth      = auth;
        _recaptcha = recaptcha;
        _db        = db;
        _logger    = logger;
    }

    // ════════════════════════════════════════════════════════
    //  POST /api/auth/register
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Registra un nuevo usuario. Valida reCAPTCHA, crea la cuenta,
    /// emite JWT y dispara el envío de la credencial PDF automáticamente.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                _logger.LogWarning("[REGISTER] ❌ Validación fallida: {e}", string.Join(" | ", errors));
                return BadRequest(new { error = "Datos inválidos.", detalles = errors });
            }

            _logger.LogInformation("[REGISTER] 🔍 Validando reCAPTCHA...");
            var captcha_ok = await _recaptcha.ValidateAsync(dto.recaptcha_token);
            if (!captcha_ok)
            {
                _logger.LogWarning("[REGISTER] ❌ reCAPTCHA inválido.");
                return BadRequest(new { error = "Verificación de seguridad fallida. Vuelve a intentarlo." });
            }

            var response = await _auth.RegisterAsync(dto);

            _logger.LogInformation("[REGISTER] ✅ Registro exitoso: {e}", dto.email);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("[REGISTER] ⚠️ Error de negocio: {m}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REGISTER] ❌ Error inesperado para {e}", dto?.email);
            return StatusCode(500, new { error = "Error interno del servidor. Intenta más tarde." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  POST /api/auth/login
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia sesión con email y contraseña.
    /// Valida reCAPTCHA, verifica BCrypt y emite JWT.
    /// Registra el ingreso en bitacora_accesos.
    /// </summary>
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
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                _logger.LogWarning("[LOGIN] ❌ Validación fallida: {e}", string.Join(" | ", errors));
                return BadRequest(new { error = "Datos inválidos.", detalles = errors });
            }

            _logger.LogInformation("[LOGIN] 🔍 Validando reCAPTCHA...");
            var captcha_ok = await _recaptcha.ValidateAsync(dto.recaptcha_token);
            if (!captcha_ok)
            {
                _logger.LogWarning("[LOGIN] ❌ reCAPTCHA inválido.");
                return BadRequest(new { error = "Verificación de seguridad fallida. Vuelve a intentarlo." });
            }

            var response = await _auth.LoginAsync(dto);

            _logger.LogInformation("[LOGIN] ✅ Login exitoso: {e}", dto.email);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("[LOGIN] ❌ Credenciales inválidas para: {e}", dto?.email);
            return Unauthorized(new { error = "Credenciales inválidas." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LOGIN] ❌ Error inesperado para {e}", dto?.email);
            return StatusCode(500, new { error = "Error interno del servidor. Intenta más tarde." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  POST /api/auth/logout
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Cierra la sesión. Revoca el token JWT en BD
    /// y registra la fecha_salida en bitacora_accesos.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var bearer  = Request.Headers.Authorization.ToString();
            var user_id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation("[LOGOUT] 🚪 Usuario {id} cerrando sesión.", user_id);
            await _auth.LogoutAsync(bearer);
            _logger.LogInformation("[LOGOUT] ✅ Sesión cerrada. Usuario {id}.", user_id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LOGOUT] ❌ Error inesperado al cerrar sesión.");
            return StatusCode(500, new { error = "Error al cerrar sesión." });
        }
    }

    // ════════════════════════════════════════════════════════
    //  GET /api/auth/me
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Retorna el perfil completo del usuario autenticado.
    /// Consulta la BD para incluir datos actualizados y preferencias del editor.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        try
        {
            var uid_str = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uid_str, out int usuario_id))
                return Unauthorized(new { error = "Token inválido." });

            // Consultar BD para datos siempre actualizados
            // (el JWT puede tener datos viejos si el usuario actualizó su perfil)
            var usuario = await _db.usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.id == usuario_id && u.activo);

            if (usuario is null)
                return Unauthorized(new { error = "Usuario no encontrado o inactivo." });

            // Obtener preferencias del editor
            var prefs = await _db.preferencias_editor
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.usuario_id == usuario_id);

            // Contar compilaciones del usuario
            var total_compilaciones = await _db.compilaciones
                .CountAsync(c => c.usuario_id == usuario_id);

            _logger.LogDebug("[ME] Perfil consultado para usuario ID: {id}", usuario_id);

            return Ok(new
            {
                // Datos de identidad
                id                   = usuario.id,
                usuario              = usuario.usuario,
                nombre_completo      = usuario.nombre_completo,
                email                = usuario.email,
                email_confirmado     = usuario.email_confirmado,
                telefono             = usuario.telefono,
                telefono_confirmado  = usuario.telefono_confirmado,
                avatar_url           = usuario.avatar_url,
                rol                  = usuario.rol,
                activo               = usuario.activo,
                fecha_creacion       = usuario.fecha_creacion,
                total_compilaciones  = total_compilaciones,

                // Preferencias del editor (null si aún no existen)
                preferencias = prefs is null ? null : new
                {
                    tema                     = prefs.tema,
                    tamano_fuente            = prefs.tamano_fuente,
                    fuente                   = prefs.fuente,
                    color_keywords           = prefs.color_keywords,
                    color_commands           = prefs.color_commands,
                    color_parenthesis        = prefs.color_parenthesis,
                    color_integers           = prefs.color_integers,
                    interlineado             = prefs.interlineado,
                    lenguaje_destino_default = prefs.lenguaje_destino_default
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ME] ❌ Error inesperado.");
            return StatusCode(500, new { error = "Error al obtener perfil." });
        }
    }
}
