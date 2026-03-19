using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace umg_basic_rover_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AvatarController : ControllerBase
{
    private static readonly List<object> _avatares = new()
    {
        new { id = 1, nombre = "Conductor Espacial",   svg = Avatar1() },
        new { id = 2, nombre = "Piloto Racing",        svg = Avatar2() },
        new { id = 3, nombre = "Conductora Aventurera",svg = Avatar3() },
        new { id = 4, nombre = "Explorador Retro",     svg = Avatar4() },
        new { id = 5, nombre = "Conductora Techie",    svg = Avatar5() },
        new { id = 6, nombre = "Piloto Invierno",      svg = Avatar6() },
        new { id = 7, nombre = "Conductor Urbano",     svg = Avatar7() },
        new { id = 8, nombre = "Conductora Afro",      svg = Avatar8() },
    };

    /// <summary>
    /// Retorna el catálogo de avatares disponibles.
    /// El frontend muestra el grid y manda el svg del elegido como avatar_base64 en el registro.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetAvatares()
    {
        return Ok(new { avatares = _avatares, total = _avatares.Count });
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAvatar(int id)
    {
        var avatar = _avatares.FirstOrDefault(a => ((dynamic)a).id == id);
        if (avatar is null) return NotFound(new { error = "Avatar no encontrado." });
        return Ok(avatar);
    }

    // ── AVATARES SVG ─────────────────────────────────────────

    private static string Avatar1() => @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 120'>
      <circle cx='50' cy='35' r='22' fill='#1a1a2e'/>
      <circle cx='50' cy='33' r='18' fill='#f4a261'/>
      <ellipse cx='50' cy='85' rx='28' ry='30' fill='#e63946'/>
      <rect x='22' y='70' width='56' height='8' rx='4' fill='#c1121f'/>
      <circle cx='50' cy='108' r='10' fill='#457b9d' opacity='0.6'/>
      <rect x='35' y='95' width='30' height='6' rx='3' fill='#1d3557'/>
      <circle cx='38' cy='30' r='3' fill='#2b2d42' opacity='0.4'/>
      <circle cx='62' cy='30' r='3' fill='#2b2d42' opacity='0.4'/>
      <path d='M43 38 Q50 43 57 38' stroke='#c1121f' stroke-width='2' fill='none'/>
    </svg>";

    private static string Avatar2() => @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 120'>
      <circle cx='50' cy='33' r='18' fill='#ffb347'/>
      <rect x='28' y='18' width='44' height='12' rx='6' fill='#e63946'/>
      <ellipse cx='50' cy='85' rx='28' ry='30' fill='#2b2d42'/>
      <rect x='22' y='70' width='56' height='8' rx='4' fill='#e63946'/>
      <circle cx='50' cy='108' r='10' fill='#457b9d' opacity='0.6'/>
      <rect x='35' y='95' width='30' height='6' rx='3' fill='#e63946'/>
      <circle cx='38' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <circle cx='62' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <path d='M43 39 Q50 44 57 39' stroke='#f4a261' stroke-width='2' fill='none'/>
      <rect x='44' y='18' width='12' height='3' fill='#ffd700'/>
    </svg>";

    private static string Avatar3() => @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 120'>
      <circle cx='50' cy='33' r='18' fill='#f4a261'/>
      <path d='M32 25 Q50 8 68 25 Q65 15 50 12 Q35 15 32 25Z' fill='#8b4513'/>
      <ellipse cx='50' cy='85' rx='28' ry='30' fill='#4a90e2'/>
      <rect x='22' y='70' width='56' height='8' rx='4' fill='#2171c7'/>
      <circle cx='50' cy='108' r='10' fill='#457b9d' opacity='0.6'/>
      <rect x='35' y='95' width='30' height='6' rx='3' fill='#1d3557'/>
      <circle cx='38' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <circle cx='62' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <path d='M43 39 Q50 45 57 39' stroke='#e63946' stroke-width='2' fill='none'/>
    </svg>";

    private static string Avatar4() => @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 120'>
      <circle cx='50' cy='33' r='18' fill='#dda15e'/>
      <rect x='30' y='16' width='40' height='10' rx='5' fill='#606c38'/>
      <ellipse cx='50' cy='85' rx='28' ry='30' fill='#606c38'/>
      <rect x='22' y='70' width='56' height='8' rx='4' fill='#283618'/>
      <circle cx='50' cy='108' r='10' fill='#457b9d' opacity='0.6'/>
      <rect x='35' y='95' width='30' height='6' rx='3' fill='#283618'/>
      <circle cx='38' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <circle cx='62' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <path d='M43 39 Q50 44 57 39' stroke='#bc6c25' stroke-width='2' fill='none'/>
      <rect x='46' y='38' width='8' height='10' fill='#dda15e'/>
    </svg>";

    private static string Avatar5() => @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 120'>
      <circle cx='50' cy='33' r='18' fill='#f9c784'/>
      <path d='M32 28 Q50 10 68 28 L65 22 Q50 8 35 22Z' fill='#2b2d42'/>
      <ellipse cx='50' cy='85' rx='28' ry='30' fill='#7209b7'/>
      <rect x='22' y='70' width='56' height='8' rx='4' fill='#560bad'/>
      <circle cx='50' cy='108' r='10' fill='#457b9d' opacity='0.6'/>
      <rect x='35' y='95' width='30' height='6' rx='3' fill='#3a0ca3'/>
      <circle cx='38' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <circle cx='62' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <path d='M43 39 Q50 45 57 39' stroke='#f72585' stroke-width='2' fill='none'/>
      <rect x='43' y='42' width='14' height='3' rx='1' fill='#f72585'/>
    </svg>";

    private static string Avatar6() => @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 120'>
      <circle cx='50' cy='33' r='18' fill='#f4a261'/>
      <rect x='30' y='14' width='40' height='14' rx='7' fill='#e63946'/>
      <rect x='28' y='24' width='44' height='6' rx='3' fill='#white'/>
      <ellipse cx='50' cy='85' rx='28' ry='30' fill='#457b9d'/>
      <rect x='22' y='70' width='56' height='8' rx='4' fill='#1d3557'/>
      <circle cx='50' cy='108' r='10' fill='#457b9d' opacity='0.6'/>
      <rect x='35' y='95' width='30' height='6' rx='3' fill='#1d3557'/>
      <circle cx='38' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <circle cx='62' cy='31' r='3' fill='#2b2d42' opacity='0.5'/>
      <path d='M43 39 Q50 44 57 39' stroke='#e63946' stroke-width='2' fill='none'/>
    </svg>";

    private static string Avatar7() => @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 120'>
      <circle cx='50' cy='33' r='18' fill='#8d5524'/>
      <ellipse cx='50' cy='85' rx='28' ry='30' fill='#2b2d42'/>
      <rect x='22' y='70' width='56' height='8' rx='4' fill='#e63946'/>
      <circle cx='50' cy='108' r='10' fill='#457b9d' opacity='0.6'/>
      <rect x='35' y='95' width='30' height='6' rx='3' fill='#1d3557'/>
      <circle cx='38' cy='31' r='3' fill='#1a1a2e' opacity='0.6'/>
      <circle cx='62' cy='31' r='3' fill='#1a1a2e' opacity='0.6'/>
      <path d='M43 39 Q50 45 57 39' stroke='#f4a261' stroke-width='2' fill='none'/>
      <rect x='38' y='14' width='24' height='8' rx='4' fill='#1a1a2e'/>
    </svg>";

    private static string Avatar8() => @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 120'>
      <circle cx='50' cy='33' r='18' fill='#5c3317'/>
      <path d='M32 28 Q50 5 68 28 Q60 10 50 8 Q40 10 32 28Z' fill='#1a1a2e'/>
      <ellipse cx='50' cy='85' rx='28' ry='30' fill='#e63946'/>
      <rect x='22' y='70' width='56' height='8' rx='4' fill='#c1121f'/>
      <circle cx='50' cy='108' r='10' fill='#457b9d' opacity='0.6'/>
      <rect x='35' y='95' width='30' height='6' rx='3' fill='#1d3557'/>
      <circle cx='38' cy='31' r='3' fill='#1a1a2e' opacity='0.6'/>
      <circle cx='62' cy='31' r='3' fill='#1a1a2e' opacity='0.6'/>
      <path d='M43 39 Q50 45 57 39' stroke='#ffd700' stroke-width='2' fill='none'/>
    </svg>";
}