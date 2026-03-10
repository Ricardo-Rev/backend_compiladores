using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_infrastructure.Services;

// ============================================================
//  IMPLEMENTACIÓN: AuthService
//  Lógica principal del sistema de autenticación.
//
//  FLUJOS IMPLEMENTADOS:
//  1. RegisterAsync → Valida unicidad, hashea BCrypt, emite JWT
//  2. LoginAsync    → Verifica email + BCrypt, emite JWT
//  3. LogoutAsync   → Revoca token marcando sesión como inactiva
//
//  SEGURIDAD:
//  ✅ Contraseñas hasheadas con BCrypt (workFactor=12)
//  ✅ JWT firmado con HMAC-SHA256
//  ✅ Solo el HASH del token se guarda en BD (columna session_token)
//  ✅ Mensajes de error genéricos (no revelan si el email existe)
//  ✅ Captura de IP y User-Agent en cada sesión
//  ✅ Logging estructurado con prefijos claros
// ============================================================

public class AuthService : IAuthService
{
    private readonly rover_db_context _db;
    private readonly IJwtTokenService _jwt;
    private readonly IHttpContextAccessor _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        rover_db_context db,
        IJwtTokenService jwt,
        IHttpContextAccessor http,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _db     = db;
        _jwt    = jwt;
        _http   = http;
        _config = config;
        _logger = logger;
    }

    // ============================================================
    //  REGISTRO DE NUEVO USUARIO
    // ============================================================

    public async Task<AuthResponse> RegisterAsync(RegisterRequest dto)
    {
        _logger.LogInformation("[REGISTER] 🚀 Intento de registro. Usuario: {Usuario} | Email: {Email}",
            dto.usuario, dto.email);

        // 1. Verificar que el nombre de usuario no esté en uso
        var usuario_existe = await _db.usuarios
            .AsNoTracking()
            .AnyAsync(u => u.usuario == dto.usuario.Trim());

        if (usuario_existe)
        {
            _logger.LogWarning("[REGISTER] ❌ Nombre de usuario duplicado: {Usuario}", dto.usuario);
            throw new InvalidOperationException("El nombre de usuario ya está en uso.");
        }

        // 2. Verificar que el email no esté en uso
        var email_existe = await _db.usuarios
            .AsNoTracking()
            .AnyAsync(u => u.email == dto.email.ToLower().Trim());

        if (email_existe)
        {
            _logger.LogWarning("[REGISTER] ❌ Email duplicado: {Email}", dto.email);
            throw new InvalidOperationException("El correo electrónico ya está registrado.");
        }

        // 3. Hashear la contraseña con BCrypt
        // workFactor=12 → buen balance entre seguridad y velocidad
        var password_hash = BCrypt.Net.BCrypt.HashPassword(dto.password, workFactor: 12);
        _logger.LogDebug("[REGISTER] ✅ Contraseña hasheada con BCrypt (workFactor=12).");

        // 4. Crear la entidad usuario con todos los campos requeridos por la BD
        var nuevo_usuario = new user_entity
        {
            usuario           = dto.usuario.Trim(),
            email             = dto.email.ToLower().Trim(),
            email_confirmado  = false,         // Por defecto no confirmado
            nombre_completo   = dto.nombre_completo.Trim(),
            password_hash     = password_hash,
            telefono          = dto.telefono.Trim(),
            telefono_confirmado = false,       // Por defecto no confirmado
            rol               = "conductor",  // Rol por defecto al registrarse
            activo            = true,
            fecha_creacion    = DateTime.Now
        };

        // 5. Guardar en BD dentro de una transacción
        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            _db.usuarios.Add(nuevo_usuario);
            await _db.SaveChangesAsync();

            _logger.LogInformation("[REGISTER] ✅ Usuario creado en BD. ID: {UserId}", nuevo_usuario.id);

            // 6. Emitir JWT → el usuario queda logueado inmediatamente tras registrarse
            var response = await EmitirTokenAsync(nuevo_usuario);

            await transaction.CommitAsync();
            _logger.LogInformation("[REGISTER] ✅ Transacción confirmada.");

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[REGISTER] ❌ Error en transacción. Rollback ejecutado.");
            throw;
        }
    }

    // ============================================================
    //  LOGIN
    // ============================================================

    public async Task<AuthResponse> LoginAsync(LoginRequest dto)
    {
        _logger.LogInformation("[LOGIN] 🔑 Intento de login: {Email}", dto.email);

        // 1. Buscar usuario por email
        var usuario = await _db.usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.email == dto.email.ToLower().Trim());

        // 2. Verificar existencia y estado
        // SEGURIDAD: Mismo mensaje para "no existe" y "contraseña incorrecta"
        // Esto evita "user enumeration attacks" (saber qué emails están registrados).
        if (usuario is null || !usuario.activo)
        {
            _logger.LogWarning("[LOGIN] ❌ Usuario no encontrado o inactivo: {Email}", dto.email);
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        // 3. Verificar contraseña con BCrypt
        // BCrypt.Verify compara el texto plano contra el hash almacenado
        var password_ok = BCrypt.Net.BCrypt.Verify(dto.password, usuario.password_hash);

        if (!password_ok)
        {
            _logger.LogWarning("[LOGIN] ❌ Contraseña incorrecta para usuario ID: {UserId}", usuario.id);
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        _logger.LogInformation("[LOGIN] ✅ Login exitoso. Usuario ID: {UserId}", usuario.id);
        return await EmitirTokenAsync(usuario);
    }

    // ============================================================
    //  LOGOUT (REVOCACIÓN DE TOKEN)
    // ============================================================

    public async Task LogoutAsync(string bearer_token)
    {
        // Extraer el token puro del header "Bearer {token}"
        var token = bearer_token?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? bearer_token[7..].Trim()
            : bearer_token?.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("[LOGOUT] ⚠️ Token vacío recibido.");
            return;
        }

        // Calcular el hash del token para buscarlo en BD
        var token_hash = _jwt.ComputeSha256(token);

        // Buscar la sesión activa con ese hash
        // Nota: La columna en BD se llama 'session_token' y guarda el hash
        var sesion = await _db.sesiones
            .FirstOrDefaultAsync(s => s.session_token == token_hash && s.activa);

        if (sesion is not null)
        {
            sesion.activa = false;
            await _db.SaveChangesAsync();
            _logger.LogInformation("[LOGOUT] ✅ Sesión {SessionId} revocada correctamente.", sesion.id);
        }
        else
        {
            _logger.LogWarning("[LOGOUT] ⚠️ No se encontró sesión activa para el token.");
        }
    }

    // ============================================================
    //  MÉTODO PRIVADO: Generar Token + Registrar Sesión en BD
    // ============================================================

    private async Task<AuthResponse> EmitirTokenAsync(user_entity usuario)
    {
        // Leer duración del token desde configuración
        var expires_minutes = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 60;
        var fecha_expiracion = DateTime.Now.AddMinutes(expires_minutes);

        // Claims que se embeben dentro del JWT
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.id.ToString()),
            new Claim(ClaimTypes.Email, usuario.email),
            new Claim(ClaimTypes.Name, usuario.nombre_completo),
            new Claim(ClaimTypes.Role, usuario.rol),
            // Claim personalizado con el nombre de usuario
            new Claim("usuario", usuario.usuario)
        };

        // Generar el token JWT firmado
        var (access_token, _) = _jwt.CreateToken(claims);
        var token_hash = _jwt.ComputeSha256(access_token);

        // Capturar IP y User-Agent del request actual
        var ip_origen  = _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        var user_agent = _http.HttpContext?.Request?.Headers["User-Agent"].ToString();

        // Registrar la sesión en BD (columna session_token guarda el HASH)
        _db.sesiones.Add(new sesion_entity
        {
            usuario_id       = usuario.id,
            session_token    = token_hash,         // Hash SHA-256 del JWT
            metodo_login     = "password",         // Este servicio solo maneja password
            ip_origen        = ip_origen,
            user_agent       = user_agent,
            fecha_login      = DateTime.Now,
            fecha_expiracion = fecha_expiracion,
            activa           = true
        });

        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            access_token       = access_token,
            expires_in_seconds = expires_minutes * 60,
            user = new UserDto
            {
                id              = usuario.id,
                usuario         = usuario.usuario,
                nombre_completo = usuario.nombre_completo,
                email           = usuario.email,
                rol             = usuario.rol,
                avatar_url      = usuario.avatar_url,
                fecha_creacion  = usuario.fecha_creacion
            }
        };
    }
}
