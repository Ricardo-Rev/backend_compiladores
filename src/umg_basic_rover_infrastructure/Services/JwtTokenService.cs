using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using umg_basic_rover_application.Contracts;

namespace umg_basic_rover_infrastructure.Services;

// ============================================================
//  IMPLEMENTACIÓN: JwtTokenService
//  Genera y administra tokens JWT.
//
//  ¿QUÉ ES UN JWT?
//  JSON Web Token: Un estándar (RFC 7519) para transmitir información
//  de forma segura entre partes usando una firma digital.
//
//  ESTRUCTURA: Header.Payload.Signature
//  - Header:    Algoritmo de firma (HS256)
//  - Payload:   Claims (datos del usuario embebidos)
//  - Signature: Firma HMAC-SHA256 con la clave secreta
//
//  CONFIGURACIÓN (appsettings.json → sección "Jwt"):
//  - Key:       Clave secreta (mínimo 32 caracteres)
//  - Issuer:    Emisor del token (nombre de tu API)
//  - Audience:  Receptor esperado del token (nombre del frontend)
//  - ExpiresMinutes: Minutos hasta que expira el token
// ============================================================

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Genera un token JWT firmado con los datos del usuario.
    /// </summary>
    public (string access_token, string jti) CreateToken(IEnumerable<Claim> claims)
    {
        // Obtener configuración desde appsettings.json
        var key = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key no está configurada en appsettings.");
        var issuer = _config["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer no está configurada.");
        var audience = _config["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience no está configurada.");
        var expires_minutes = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 60;

        // Crear clave de firma simétrica HMAC-SHA256
        var security_key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(security_key, SecurityAlgorithms.HmacSha256);

        // Generar un ID único para este token (se usa en revocación)
        var jti = Guid.NewGuid().ToString();

        // Combinar claims del usuario con claims estándar del token
        var all_claims = claims.ToList();
        all_claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));

        // Construir el token
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: all_claims,
            expires: DateTime.UtcNow.AddMinutes(expires_minutes),
            signingCredentials: credentials
        );

        var token_string = new JwtSecurityTokenHandler().WriteToken(token);
        return (token_string, jti);
    }

    /// <summary>
    /// Genera hash SHA-256 de un token para almacenarlo de forma segura en BD.
    /// </summary>
    public string ComputeSha256(string token)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
