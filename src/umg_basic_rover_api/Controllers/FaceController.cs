using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umg_basic_rover_infrastructure.Services;
using umg_basic_rover_application.DTOs;

namespace umg_basic_rover_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FaceController : ControllerBase
{
    private readonly FaceSegmentationService _segmentation;
    private readonly ILogger<FaceController> _logger;

    public FaceController(FaceSegmentationService segmentation, ILogger<FaceController> logger)
    {
        _segmentation = segmentation;
        _logger = logger;
    }

    /// <summary>
    /// Detecta la cara en la imagen, la recorta y devuelve
    /// base64 PNG con fondo blanco. Listo para guardar como avatar.
    /// </summary>
    [HttpPost("segment")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public IActionResult Segment([FromBody] FaceSegmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.image_base64))
            return BadRequest(new { error = "image_base64 es requerido." });

        var (success, base64, message) = _segmentation.SegmentFace(request.image_base64);

        if (!success)
            return BadRequest(new { error = message });

        return Ok(new FaceSegmentResponse
        {
            success   = true,
            resultado = base64,
            mensaje   = "Cara detectada y segmentada correctamente."
        });
    }
}
