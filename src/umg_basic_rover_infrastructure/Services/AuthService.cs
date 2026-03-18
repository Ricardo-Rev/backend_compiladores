using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_infrastructure.Services;

// ============================================================
//  AuthService — VERSIÓN COMPLETA
//
//  CAMBIOS respecto a la versión original de Sergio:
//  ✅ RegisterAsync → llama a ICredentialService post-registro
//  ✅ RegisterAsync → crea preferencias_editor por defecto
//  ✅ LoginAsync    → registra en bitacora_accesos al ingresar
//  ✅ LogoutAsync   → actualiza fecha_salida en bitacora_accesos
//  ✅ EmitirTokenAsync → recibe metodo_login como parámetro
//                        (soporta "password", "facial", "qr")
//
//  LO QUE NO CAMBIÓ (de Sergio, estaba bien):
//  ✅ BCrypt workFactor=12
//  ✅ Hash SHA-256 del token en BD
//  ✅ Mensajes genéricos en login
//  ✅ Transacción en RegisterAsync
//  ✅ Captura de IP y User-Agent
//  ✅ Logging estructurado
// ============================================================

public class AuthService : IAuthService
{
    private readonly rover_db_context       _db;
    private readonly IJwtTokenService       _jwt;
    private readonly IHttpContextAccessor   _http;
    private readonly IConfiguration         _config;
    private readonly ICredentialService     _credential;
    private readonly ILogger<AuthService>   _logger;

    public AuthService(
        rover_db_context     db,
        IJwtTokenService     jwt,
        IHttpContextAccessor http,
        IConfiguration       config,
        ICredentialService   credential,
        ILogger<AuthService> logger)
    {
        _db         = db;
        _jwt        = jwt;
        _http       = http;
        _config     = config;
        _credential = credential;
        _logger     = logger;
    }

    // ════════════════════════════════════════════════════════
    //  REGISTRO
    // ════════════════════════════════════════════════════════

    public async Task<AuthResponse> RegisterAsync(RegisterRequest dto)
    {
        _logger.LogInformation("[REGISTER] 🚀 Intento de registro. Usuario: {u} | Email: {e}",
            dto.usuario, dto.email);

        // 1. Verificar unicidad de usuario
        var usuario_existe = await _db.usuarios
            .AsNoTracking()
            .AnyAsync(u => u.usuario == dto.usuario.Trim());

        if (usuario_existe)
        {
            _logger.LogWarning("[REGISTER] ❌ Nombre de usuario duplicado: {u}", dto.usuario);
            throw new InvalidOperationException("El nombre de usuario ya está en uso.");
        }

        // 2. Verificar unicidad de email
        var email_existe = await _db.usuarios
            .AsNoTracking()
            .AnyAsync(u => u.email == dto.email.ToLower().Trim());

        if (email_existe)
        {
            _logger.LogWarning("[REGISTER] ❌ Email duplicado: {e}", dto.email);
            throw new InvalidOperationException("El correo electrónico ya está registrado.");
        }

        // 3. Hashear contraseña con BCrypt
        var password_hash = BCrypt.Net.BCrypt.HashPassword(dto.password, workFactor: 12);

        // 4. Crear entidad usuario
        var nuevo_usuario = new user_entity
        {
            usuario              = dto.usuario.Trim(),
            email                = dto.email.ToLower().Trim(),
            email_confirmado     = false,
            nombre_completo      = dto.nombre_completo.Trim(),
            password_hash        = password_hash,
            telefono             = dto.telefono.Trim(),
            telefono_confirmado  = false,
            rol                  = "conductor",
            activo               = true,
            fecha_creacion       = DateTime.Now,
             avatar_base64        = dto.avatar_base64
        };

        // 5. Todo dentro de una transacción
        //    NOTA: SqlServerRetryingExecutionStrategy requiere usar CreateExecutionStrategy()
        //    para que los reintentos automáticos funcionen con transacciones manuales.
        var strategy  = _db.Database.CreateExecutionStrategy();
        AuthResponse? response = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.usuarios.Add(nuevo_usuario);
                await _db.SaveChangesAsync();

                _logger.LogInformation("[REGISTER] ✅ Usuario creado en BD. ID: {id}", nuevo_usuario.id);

                // 6. Crear preferencias del editor con valores por defecto
                //    El usuario tendrá su configuración visual lista desde el primer login
                _db.preferencias_editor.Add(new preferencias_editor_entity
                {
                    usuario_id               = nuevo_usuario.id,
                    tema                     = "dark",
                    tamano_fuente            = 14,
                    fuente                   = "Fira Code",
                    color_keywords           = "#4FC3F7",
                    color_commands           = "#87CEEB",
                    color_parenthesis        = "#66BB6A",
                    color_integers           = "#EF5350",
                    interlineado             = 1.5m,
                    lenguaje_destino_default = "python",
                    fecha_actualizacion      = DateTime.Now
                });
                await _db.SaveChangesAsync();

                // 7. Emitir JWT → usuario queda logueado inmediatamente
                response = await EmitirTokenAsync(nuevo_usuario, "password");

                await tx.CommitAsync();
                _logger.LogInformation("[REGISTER] ✅ Transacción confirmada.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[REGISTER] ❌ Error en transacción. Rollback ejecutado.");
                throw;
            }
        });

        // 8. Generar y enviar credencial PDF
        //    Se hace FUERA de la transacción porque si el email falla
        //    no debe revertir el registro. El usuario ya existe en BD.
_ = Task.Run(async () =>
{
    using var scope      = _http.HttpContext!.RequestServices
                               .GetRequiredService<IServiceScopeFactory>()
                               .CreateScope();
    var credential_svc   = scope.ServiceProvider.GetRequiredService<ICredentialService>();
    try
    {
        await credential_svc.GenerarYEnviarAsync(nuevo_usuario.id);
        _logger.LogInformation("[REGISTER] ✅ Credencial PDF enviada. Usuario ID: {id}", nuevo_usuario.id);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[REGISTER] ⚠️ Error al enviar credencial. Usuario ID: {id}", nuevo_usuario.id);
    }
});

        return response!;
    }

    // ════════════════════════════════════════════════════════
    //  LOGIN
    // ════════════════════════════════════════════════════════

    public async Task<AuthResponse> LoginAsync(LoginRequest dto)
    {
        _logger.LogInformation("[LOGIN] 🔑 Intento de login: {e}", dto.email);

        // 1. Buscar usuario por email
        var usuario = await _db.usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.email == dto.email.ToLower().Trim());

        // 2. Verificar existencia y estado
        //    Mismo mensaje para "no existe" y "contraseña incorrecta"
        //    Evita user enumeration attacks
        if (usuario is null || !usuario.activo)
        {
            _logger.LogWarning("[LOGIN] ❌ Usuario no encontrado o inactivo: {e}", dto.email);
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        // 3. Verificar contraseña con BCrypt
        var password_ok = BCrypt.Net.BCrypt.Verify(dto.password, usuario.password_hash);
        if (!password_ok)
        {
            _logger.LogWarning("[LOGIN] ❌ Contraseña incorrecta. Usuario ID: {id}", usuario.id);
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        // 4. Emitir JWT
        var response = await EmitirTokenAsync(usuario, "password");

        // 5. Registrar ingreso en bitacora_accesos
        //    El Dashboard de admin necesita esta tabla para mostrar ingresos/salidas
        var ip_origen  = _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        var user_agent = _http.HttpContext?.Request?.Headers["User-Agent"].ToString();

        _db.bitacora_accesos.Add(new bitacora_acceso_entity
        {
            usuario_id    = usuario.id,
            metodo_login  = "password",
            ip_origen     = ip_origen,
            user_agent    = user_agent,
            fecha_ingreso = DateTime.Now,
            fecha_salida  = null          // Se actualiza en LogoutAsync
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("[LOGIN] ✅ Login exitoso. Usuario ID: {id}", usuario.id);
        return response;
    }

    // ════════════════════════════════════════════════════════
    //  LOGOUT
    // ════════════════════════════════════════════════════════

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

        var token_hash = _jwt.ComputeSha256(token);

        // 1. Revocar sesión JWT
        var sesion = await _db.sesiones
            .FirstOrDefaultAsync(s => s.session_token == token_hash && s.activa);

        if (sesion is not null)
        {
            sesion.activa = false;
            await _db.SaveChangesAsync();
            _logger.LogInformation("[LOGOUT] ✅ Sesión JWT {id} revocada.", sesion.id);

            // 2. Registrar fecha_salida en bitacora_accesos
            //    Busca el último acceso activo (sin fecha_salida) del usuario
            var ultimo_acceso = await _db.bitacora_accesos
                .Where(b => b.usuario_id == sesion.usuario_id && b.fecha_salida == null)
                .OrderByDescending(b => b.fecha_ingreso)
                .FirstOrDefaultAsync();

            if (ultimo_acceso is not null)
            {
                ultimo_acceso.fecha_salida = DateTime.Now;
                await _db.SaveChangesAsync();
                _logger.LogInformation("[LOGOUT] ✅ Bitácora actualizada. Acceso ID: {id}", ultimo_acceso.id);
            }
        }
        else
        {
            _logger.LogWarning("[LOGOUT] ⚠️ No se encontró sesión activa para el token.");
        }
    }

    // ════════════════════════════════════════════════════════
    //  PRIVADO: Generar Token + Registrar Sesión en BD
    // ════════════════════════════════════════════════════════

    private async Task<AuthResponse> EmitirTokenAsync(user_entity usuario, string metodo_login)
    {
        var expires_minutes  = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 60;
        var fecha_expiracion = DateTime.Now.AddMinutes(expires_minutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.id.ToString()),
            new Claim(ClaimTypes.Email,          usuario.email),
            new Claim(ClaimTypes.Name,           usuario.nombre_completo),
            new Claim(ClaimTypes.Role,           usuario.rol),
            new Claim("usuario",                 usuario.usuario)
        };

        var (access_token, _) = _jwt.CreateToken(claims);
        var token_hash        = _jwt.ComputeSha256(access_token);

        var ip_origen  = _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        var user_agent = _http.HttpContext?.Request?.Headers["User-Agent"].ToString();

        // Guardar sesión con el HASH del token (nunca el token real)
        _db.sesiones.Add(new sesion_entity
        {
            usuario_id       = usuario.id,
            session_token    = token_hash,
            metodo_login     = metodo_login,
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