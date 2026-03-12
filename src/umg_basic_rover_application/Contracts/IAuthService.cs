using umg_basic_rover_application.DTOs;

namespace umg_basic_rover_application.Contracts;

// ============================================================
//  INTERFAZ: IAuthService
//  Contrato que define las operaciones del servicio de autenticación.
//
//  PRINCIPIO:
//  La capa Application define el contrato (interfaz).
//  La capa Infrastructure implementa la lógica concreta.
//  Esto permite cambiar la implementación sin tocar el contrato.
// ============================================================

public interface IAuthService
{
    /// <summary>
    /// Registra un nuevo usuario en el sistema.
    /// Valida que el email no esté en uso, hashea la contraseña
    /// con BCrypt y devuelve un JWT listo para usar.
    /// </summary>
    /// <param name="dto">Datos del nuevo usuario.</param>
    /// <returns>Token JWT y datos básicos del usuario creado.</returns>
    /// <exception cref="InvalidOperationException">Si el email ya existe.</exception>
    Task<AuthResponse> RegisterAsync(RegisterRequest dto);

    /// <summary>
    /// Inicia sesión con email y contraseña.
    /// Verifica que las credenciales sean correctas y crea una sesión activa.
    /// </summary>
    /// <param name="dto">Email y contraseña del usuario.</param>
    /// <returns>Token JWT y datos básicos del usuario.</returns>
    /// <exception cref="UnauthorizedAccessException">Si las credenciales son incorrectas.</exception>
    Task<AuthResponse> LoginAsync(LoginRequest dto);

    /// <summary>
    /// Cierra la sesión del usuario revocando su token JWT.
    /// Marca la sesión como inactiva en la base de datos.
    /// </summary>
    /// <param name="bearer_token">Token JWT completo (con prefijo "Bearer " o sin él).</param>
    Task LogoutAsync(string bearer_token);
}
