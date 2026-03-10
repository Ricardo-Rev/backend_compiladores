namespace umg_basic_rover_domain.entities;

// ============================================================
//  ENTIDAD: sesion_entity
//  Mapea exactamente la tabla [sesiones] de la BD real.
//
//  TABLA EN BD: sesiones
//  MOTOR: SQL Server (T-SQL)
//
//  PROPÓSITO:
//  Registra cada inicio de sesión activo del usuario.
//  Permite revocar tokens JWT cuando el usuario hace logout.
//
//  FLUJO DE SEGURIDAD:
//  1. Login exitoso → INSERT en sesiones (activa = 1)
//  2. Cada request → Middleware verifica session_token en BD
//  3. Logout → UPDATE sesiones SET activa = 0
//  4. Token expirado → activa sigue siendo 1 pero fecha_expiracion pasó
//
//  MÉTODOS DE LOGIN PERMITIDOS: 'password' | 'facial' | 'qr'
// ============================================================

public class sesion_entity
{
    // ─── CLAVE PRIMARIA ───────────────────────────────────────
    public int id { get; set; }

    // ─── FK AL USUARIO ────────────────────────────────────────
    // Usuario dueño de esta sesión
    public int usuario_id { get; set; }

    // ─── TOKEN DE SESIÓN ──────────────────────────────────────
    // Hash SHA-256 del token JWT. Máx. 255 chars.
    // Se guarda el HASH (no el token en claro) por seguridad.
    // Se usa para buscar y revocar la sesión en el logout.
    public string session_token { get; set; } = string.Empty;

    // ─── MÉTODO DE AUTENTICACIÓN ──────────────────────────────
    // Cómo se autenticó el usuario. Valores: 'password' | 'facial' | 'qr'
    public string metodo_login { get; set; } = "password";

    // ─── DATOS DE RED ─────────────────────────────────────────
    // IP desde donde se hizo el login (IPv4 o IPv6, máx. 45 chars)
    public string? ip_origen { get; set; }

    // Navegador/dispositivo usado en el login (máx. 500 chars)
    public string? user_agent { get; set; }

    // ─── TIEMPOS ──────────────────────────────────────────────
    // Fecha y hora del inicio de sesión
    public DateTime fecha_login { get; set; } = DateTime.Now;

    // Fecha y hora en que expira el token JWT
    public DateTime fecha_expiracion { get; set; }

    // ─── ESTADO ───────────────────────────────────────────────
    // true = sesión activa | false = sesión cerrada (logout)
    public bool activa { get; set; } = true;

    // ─── NAVEGACIÓN ───────────────────────────────────────────
    // Usuario al que pertenece esta sesión
    public user_entity usuario { get; set; } = null!;
}
