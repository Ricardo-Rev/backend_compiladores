using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umg_basic_rover_application.Contracts;

namespace umg_basic_rover_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;

    public WhatsAppController(IWhatsAppService whatsAppService)
    {
        _whatsAppService = whatsAppService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var result = await _whatsAppService.GetStatusAsync();

        return Ok(new
        {
            ready = result.ready,
            state = result.state,
            qr_available = result.qrAvailable
        });
    }

    [HttpGet("qr")]
    public async Task<IActionResult> Qr()
    {
        var qr = await _whatsAppService.GetQrBase64Async();

        if (string.IsNullOrWhiteSpace(qr))
            return NotFound(new { error = "No hay QR disponible." });

        return Ok(new { qr_base64 = qr });
    }
}