using System.Security.Claims;

namespace umg_basic_rover_application.Contracts;

// ============================================================
//  INTERFAZ: IJwtTokenService
//  Contrato para la generación y verificación de tokens JWT.
//
//  JWT (JSON Web Token):
//  - Es un estándar para transmitir información entre partes de forma segura.
//  - Tiene 3 partes: Header.Payload.Signature
//  - El frontend lo envía en cada petición protegida:
//    Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
// ============================================================

public interface IJwtTokenService
{
    /// <summary>
    /// Genera un nuevo token JWT firmado con los claims del usuario.
    /// </summary>
    /// <param name="claims">
    /// Información del usuario que se embebe en el token:
    /// - NameIdentifier (ID del usuario)
    /// - Email
    /// - Name (nombre completo)
    /// </param>
    /// <returns>
    /// access_token → El token JWT completo.
    /// jti          → ID único del token (para revocación).
    /// </returns>
    (string access_token, string jti) CreateToken(IEnumerable<Claim> claims);

    /// <summary>
    /// Genera el hash SHA-256 de un token JWT.
    /// Se usa para guardar en BD sin exponer el token real.
    /// </summary>
    /// <param name="token">Token JWT en texto plano.</param>
    /// <returns>Hash SHA-256 en formato hexadecimal.</returns>
    string ComputeSha256(string token);
}
