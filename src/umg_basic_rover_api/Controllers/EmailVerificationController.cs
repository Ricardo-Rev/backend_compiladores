using Microsoft.AspNetCore.Mvc;
using umg_basic_rover_infrastructure.Services;

namespace umg_basic_rover_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EmailVerificationController : ControllerBase
{
    private readonly EmailVerificationService _svc;
    private readonly ILogger<EmailVerificationController> _logger;

    public EmailVerificationController(EmailVerificationService svc,
        ILogger<EmailVerificationController> logger)
    {
        _svc    = svc;
        _logger = logger;
    }

    /// <summary>
    /// Valida el token del link de verificación enviado al correo.
    /// </summary>
    [HttpGet("verify")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Token requerido." });

        var (ok, mensaje) = await _svc.VerificarTokenAsync(token);

        if (!ok)
            return BadRequest(new { error = mensaje });

        return Ok(new { mensaje });
    }

    /// <summary>
    /// Reenvía el email de verificación al usuario autenticado.
    /// </summary>
    [HttpPost("resend")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendVerification()
    {
        var usuario_id = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var email      = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;
        var nombre     = User.FindFirst(System.Security.Claims.ClaimTypes.Name)!.Value;

        await _svc.EnviarVerificacionAsync(usuario_id, email, nombre);

        return Ok(new { mensaje = "Email de verificación reenviado." });
    }
}