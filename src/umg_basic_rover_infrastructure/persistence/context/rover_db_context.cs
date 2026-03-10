using Microsoft.EntityFrameworkCore;
using umg_basic_rover_domain.entities;

namespace umg_basic_rover_infrastructure.persistence.context;

// ============================================================
//  CONTEXTO: rover_db_context
//  Puente entre el código C# y la base de datos SQL Server.
//
//  TABLAS MAPEADAS (del Script_rover.sql):
//  - usuarios  → Tabla principal de usuarios (user_entity)
//  - sesiones  → Sesiones/tokens activos (sesion_entity)
//
//  IMPORTANTE:
//  Los nombres de columnas en el mapeo DEBEN coincidir
//  exactamente con los del script SQL proporcionado.
//  Cualquier diferencia causará errores en tiempo de ejecución.
// ============================================================

public class rover_db_context : DbContext
{
    public rover_db_context(DbContextOptions<rover_db_context> options)
        : base(options)
    {
    }

    // ─── DBSETS (Representan las tablas en BD) ────────────────

    /// <summary>Tabla [usuarios] → Usuarios del sistema.</summary>
    public DbSet<user_entity> usuarios => Set<user_entity>();

    /// <summary>Tabla [sesiones] → Sesiones JWT activas.</summary>
    public DbSet<sesion_entity> sesiones => Set<sesion_entity>();

    // ─── CONFIGURACIÓN DE MAPEO ───────────────────────────────

    protected override void OnModelCreating(ModelBuilder model_builder)
    {
        base.OnModelCreating(model_builder);

        // ====================================================
        //  TABLA: usuarios
        //  Mapeo exacto con la tabla del Script_rover.sql
        // ====================================================
        model_builder.Entity<user_entity>(entity =>
        {
            entity.ToTable("usuarios");

            // PK → INT IDENTITY(1,1) generado por SQL Server
            entity.HasKey(e => e.id);
            entity.Property(e => e.id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd(); // IDENTITY

            // Nombre de usuario único
            entity.Property(e => e.usuario)
                .HasColumnName("usuario")
                .HasMaxLength(50)
                .IsRequired();

            // Email único
            entity.Property(e => e.email)
                .HasColumnName("email")
                .HasMaxLength(100)
                .IsRequired();

            // Confirmación de email
            entity.Property(e => e.email_confirmado)
                .HasColumnName("email_confirmado")
                .HasDefaultValue(false);

            // Nombre completo
            entity.Property(e => e.nombre_completo)
                .HasColumnName("nombre_completo")
                .HasMaxLength(150)
                .IsRequired();

            // Hash de contraseña (BCrypt)
            entity.Property(e => e.password_hash)
                .HasColumnName("password_hash")
                .HasMaxLength(255)
                .IsRequired();

            // Teléfono
            entity.Property(e => e.telefono)
                .HasColumnName("telefono")
                .HasMaxLength(20)
                .IsRequired();

            // Confirmación de teléfono
            entity.Property(e => e.telefono_confirmado)
                .HasColumnName("telefono_confirmado")
                .HasDefaultValue(false);

            // Avatar URL (opcional)
            entity.Property(e => e.avatar_url)
                .HasColumnName("avatar_url")
                .HasMaxLength(500);

            // Avatar en Base64 (opcional, puede ser muy grande)
            entity.Property(e => e.avatar_base64)
                .HasColumnName("avatar_base64");

            // Rol del usuario: 'conductor' o 'administrador'
            entity.Property(e => e.rol)
                .HasColumnName("rol")
                .HasMaxLength(20)
                .HasDefaultValue("conductor")
                .IsRequired();

            // Estado de la cuenta
            entity.Property(e => e.activo)
                .HasColumnName("activo")
                .HasDefaultValue(true);

            // Fecha de creación
            entity.Property(e => e.fecha_creacion)
                .HasColumnName("fecha_creacion")
                .HasDefaultValueSql("GETDATE()");

            // Índices únicos (igual que en el script SQL)
            entity.HasIndex(e => e.usuario)
                .IsUnique()
                .HasDatabaseName("UQ_usuarios_usuario");

            entity.HasIndex(e => e.email)
                .IsUnique()
                .HasDatabaseName("UQ_usuarios_email");

            // Relación 1:N → Un usuario tiene muchas sesiones
            entity.HasMany(e => e.sesiones)
                .WithOne(s => s.usuario)
                .HasForeignKey(s => s.usuario_id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ====================================================
        //  TABLA: sesiones
        //  Mapeo exacto con la tabla del Script_rover.sql
        // ====================================================
        model_builder.Entity<sesion_entity>(entity =>
        {
            entity.ToTable("sesiones");

            // PK → INT IDENTITY(1,1)
            entity.HasKey(e => e.id);
            entity.Property(e => e.id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            // FK al usuario
            entity.Property(e => e.usuario_id)
                .HasColumnName("usuario_id")
                .IsRequired();

            // Hash del token JWT (columna session_token en BD)
            entity.Property(e => e.session_token)
                .HasColumnName("session_token")
                .HasMaxLength(255)
                .IsRequired();

            // Método de login: 'password' | 'facial' | 'qr'
            entity.Property(e => e.metodo_login)
                .HasColumnName("metodo_login")
                .HasMaxLength(20)
                .HasDefaultValue("password")
                .IsRequired();

            // IP del cliente
            entity.Property(e => e.ip_origen)
                .HasColumnName("ip_origen")
                .HasMaxLength(45);

            // User-Agent del navegador/dispositivo
            entity.Property(e => e.user_agent)
                .HasColumnName("user_agent")
                .HasMaxLength(500);

            // Fecha de inicio de sesión
            entity.Property(e => e.fecha_login)
                .HasColumnName("fecha_login")
                .HasDefaultValueSql("GETDATE()");

            // Fecha de expiración del token
            entity.Property(e => e.fecha_expiracion)
                .HasColumnName("fecha_expiracion")
                .IsRequired();

            // Estado de la sesión
            entity.Property(e => e.activa)
                .HasColumnName("activa")
                .HasDefaultValue(true);

            // Índice único en el hash del token (para búsquedas rápidas en el middleware)
            entity.HasIndex(e => e.session_token)
                .IsUnique()
                .HasDatabaseName("UQ_sesiones_token");
        });
    }
}
