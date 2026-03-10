using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace umg_basic_rover_domain.entities;

// ============================================================
//  ENTIDAD: user_entity
//  Mapea exactamente la tabla [usuarios] de la BD real.
//
//  TABLA EN BD: usuarios
//  MOTOR: SQL Server (T-SQL)
//
//  NOTAS:
//  - PK es INT IDENTITY(1,1), generado por SQL Server automáticamente
//  - 'usuario' = nombre de usuario único (ej: "jperez2025")
//  - 'nombre_completo' = nombre real (ej: "Juan Pérez García")
//  - 'rol' solo acepta: 'conductor' o 'administrador'
//  - 'password_hash' almacena hash BCrypt (NUNCA texto plano)
// ============================================================

public class user_entity
{
    // ─── CLAVE PRIMARIA ───────────────────────────────────────
    // INT generado automáticamente por SQL Server (IDENTITY)
    public int id { get; set; }

    // ─── IDENTIFICACIÓN ───────────────────────────────────────
    // Nombre de usuario único. Máx. 50 chars. Ej: "jperez2025"
    public string usuario { get; set; } = string.Empty;

    // Correo electrónico único. Máx. 100 chars.
    public string email { get; set; } = string.Empty;

    // true = el usuario verificó su email (link de confirmación)
    public bool email_confirmado { get; set; } = false;

    // Nombre completo del usuario. Máx. 150 chars.
    public string nombre_completo { get; set; } = string.Empty;

    // ─── SEGURIDAD ────────────────────────────────────────────
    // Hash BCrypt de la contraseña. Máx. 255 chars.
    // ⚠️  NUNCA se guarda ni se retorna la contraseña en texto plano.
    public string password_hash { get; set; } = string.Empty;

    // ─── CONTACTO ─────────────────────────────────────────────
    // Teléfono del usuario. Máx. 20 chars.
    public string telefono { get; set; } = string.Empty;

    // true = el usuario verificó su teléfono (SMS)
    public bool telefono_confirmado { get; set; } = false;

    // ─── AVATAR / FOTO ────────────────────────────────────────
    // URL externa de la foto de perfil (opcional)
    public string? avatar_url { get; set; }

    // Foto en Base64 almacenada directamente en BD (opcional)
    public string? avatar_base64 { get; set; }

    // ─── ROL Y ESTADO ─────────────────────────────────────────
    // Rol del usuario. Valores: 'conductor' | 'administrador'
    // Controla qué acciones puede realizar en el sistema.
    public string rol { get; set; } = "conductor";

    // true = cuenta activa y puede iniciar sesión
    public bool activo { get; set; } = true;

    // Fecha de registro de la cuenta
    public DateTime fecha_creacion { get; set; } = DateTime.Now;

    // ─── RELACIONES ───────────────────────────────────────────
    // Un usuario puede tener múltiples sesiones (historial de accesos)
    public ICollection<sesion_entity> sesiones { get; set; } = new List<sesion_entity>();
}
