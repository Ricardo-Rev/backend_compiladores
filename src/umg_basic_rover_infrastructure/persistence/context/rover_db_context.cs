using Microsoft.EntityFrameworkCore;
using umg_basic_rover_domain.entities;

namespace umg_basic_rover_infrastructure.persistence.context;

public class rover_db_context : DbContext
{
    public rover_db_context(DbContextOptions<rover_db_context> options) : base(options) { }

    // ── Auth ─────────────────────────────────────────────
    public DbSet<user_entity>                 usuarios                  => Set<user_entity>();
    public DbSet<sesion_entity>               sesiones                  => Set<sesion_entity>();

    // ── Compiler ─────────────────────────────────────────
    public DbSet<compilacion_entity>          compilaciones             => Set<compilacion_entity>();
    public DbSet<error_compilacion_entity>    errores_compilacion       => Set<error_compilacion_entity>();
    public DbSet<token_lexer_entity>          tokens_lexer              => Set<token_lexer_entity>();
    public DbSet<instruccion_umgpp_entity>    instrucciones_umgpp       => Set<instruccion_umgpp_entity>();
    public DbSet<instruccion_ejecutada_entity> instrucciones_ejecutadas => Set<instruccion_ejecutada_entity>();

    // ── Simulación ────────────────────────────────────────
    public DbSet<simulacion_entity>           simulaciones              => Set<simulacion_entity>();

    // ── Editor ───────────────────────────────────────────
    public DbSet<archivo_umgpp_entity>        archivos_umgpp            => Set<archivo_umgpp_entity>();

    // ── Rover ────────────────────────────────────────────
    public DbSet<transmision_rover_entity>    transmisiones_rover       => Set<transmision_rover_entity>();

    // ── Credencial / Firma ────────────────────────────────
    public DbSet<credencial_pdf_entity>       credenciales_pdf          => Set<credencial_pdf_entity>();

    // ── Bitácora / Dashboard ──────────────────────────────
    public DbSet<bitacora_acceso_entity>      bitacora_accesos          => Set<bitacora_acceso_entity>();
    public DbSet<bitacora_accion_entity>      bitacora_acciones         => Set<bitacora_accion_entity>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── usuarios ──────────────────────────────────────
        mb.Entity<user_entity>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.usuario).HasMaxLength(50).IsRequired();
            e.Property(x => x.email).HasMaxLength(100).IsRequired();
            e.Property(x => x.email_confirmado).HasDefaultValue(false);
            e.Property(x => x.nombre_completo).HasMaxLength(150).IsRequired();
            e.Property(x => x.password_hash).HasMaxLength(255).IsRequired();
            e.Property(x => x.telefono).HasMaxLength(20).IsRequired();
            e.Property(x => x.telefono_confirmado).HasDefaultValue(false);
            e.Property(x => x.avatar_url).HasMaxLength(500);
            e.Property(x => x.rol).HasMaxLength(20).HasDefaultValue("conductor").IsRequired();
            e.Property(x => x.activo).HasDefaultValue(true);
            e.Property(x => x.fecha_creacion).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.usuario).IsUnique().HasDatabaseName("UQ_usuarios_usuario");
            e.HasIndex(x => x.email).IsUnique().HasDatabaseName("UQ_usuarios_email");
            e.HasMany(x => x.sesiones).WithOne(s => s.usuario).HasForeignKey(s => s.usuario_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── sesiones ──────────────────────────────────────
        mb.Entity<sesion_entity>(e =>
        {
            e.ToTable("sesiones");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.session_token).HasMaxLength(255).IsRequired();
            e.Property(x => x.metodo_login).HasMaxLength(20).HasDefaultValue("password").IsRequired();
            e.Property(x => x.ip_origen).HasMaxLength(45);
            e.Property(x => x.user_agent).HasMaxLength(500);
            e.Property(x => x.fecha_login).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.fecha_expiracion).IsRequired();
            e.Property(x => x.activa).HasDefaultValue(true);
            e.HasIndex(x => x.session_token).IsUnique().HasDatabaseName("UQ_sesiones_token");
        });

        // ── archivos_umgpp ────────────────────────────────
        mb.Entity<archivo_umgpp_entity>(e =>
        {
            e.ToTable("archivos_umgpp");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.nombre_archivo).HasMaxLength(200).IsRequired();
            e.Property(x => x.version).HasDefaultValue(1);
            e.Property(x => x.descripcion).HasMaxLength(500);
            e.Property(x => x.es_coreografia).HasDefaultValue(false);
            e.Property(x => x.activo).HasDefaultValue(true);
            e.Property(x => x.fecha_creacion).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.fecha_modificacion).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.usuario).WithMany().HasForeignKey(x => x.usuario_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── compilaciones ─────────────────────────────────
        mb.Entity<compilacion_entity>(e =>
        {
            e.ToTable("compilaciones");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.codigo_fuente).IsRequired();
            e.Property(x => x.modo_compilacion).HasMaxLength(50).IsRequired();
            e.Property(x => x.resultado).HasMaxLength(50).IsRequired();
            e.Property(x => x.fecha_compilacion).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.usuario).WithMany().HasForeignKey(x => x.usuario_id).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.sesion).WithMany().HasForeignKey(x => x.sesion_id).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.archivo).WithMany(a => a.compilaciones).HasForeignKey(x => x.archivo_id).OnDelete(DeleteBehavior.NoAction);
        });

        // ── errores_compilacion ───────────────────────────
        mb.Entity<error_compilacion_entity>(e =>
        {
            e.ToTable("errores_compilacion");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.tipo_error).HasMaxLength(20).IsRequired();
            e.Property(x => x.token_encontrado).HasMaxLength(200);
            e.Property(x => x.mensaje_error).IsRequired();
            e.HasOne(x => x.compilacion).WithMany(c => c.errores).HasForeignKey(x => x.compilacion_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── tokens_lexer ──────────────────────────────────
        mb.Entity<token_lexer_entity>(e =>
        {
            e.ToTable("tokens_lexer");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.tipo_token).HasMaxLength(20).IsRequired();
            e.Property(x => x.lexema).HasMaxLength(200).IsRequired();
            e.Property(x => x.valor).HasMaxLength(200);
            e.HasOne(x => x.compilacion).WithMany(c => c.tokens).HasForeignKey(x => x.compilacion_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── instrucciones_umgpp ───────────────────────────
        mb.Entity<instruccion_umgpp_entity>(e =>
        {
            e.ToTable("instrucciones_umgpp");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.nombre_instruccion).HasMaxLength(50).IsRequired();
            e.Property(x => x.categoria).HasMaxLength(20).IsRequired();
            e.Property(x => x.sintaxis).HasMaxLength(200).IsRequired();
            e.Property(x => x.permite_cero).HasDefaultValue(false);
            e.Property(x => x.activo).HasDefaultValue(true);
            e.HasIndex(x => x.nombre_instruccion).IsUnique().HasDatabaseName("UQ_instruccion_nombre");
        });

        // ── instrucciones_ejecutadas ──────────────────────
        mb.Entity<instruccion_ejecutada_entity>(e =>
        {
            e.ToTable("instrucciones_ejecutadas");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.instruccion_raw).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.compilacion).WithMany(c => c.instrucciones).HasForeignKey(x => x.compilacion_id).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.instruccion).WithMany(i => i.instrucciones_ejecutadas).HasForeignKey(x => x.instruccion_id).OnDelete(DeleteBehavior.NoAction);
        });

        // ── simulaciones ──────────────────────────────────
        mb.Entity<simulacion_entity>(e =>
        {
            e.ToTable("simulaciones");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.trayectoria_json).IsRequired();
            e.Property(x => x.distancia_total_cm).HasPrecision(10, 2);
            e.Property(x => x.fecha_simulacion).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.compilacion_id).IsUnique().HasDatabaseName("UQ_sim_compilacion");
            e.HasOne(x => x.compilacion).WithOne(c => c.simulacion).HasForeignKey<simulacion_entity>(x => x.compilacion_id).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.usuario).WithMany().HasForeignKey(x => x.usuario_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── transmisiones_rover ───────────────────────────
        mb.Entity<transmision_rover_entity>(e =>
        {
            e.ToTable("transmisiones_rover");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.lenguaje_destino).HasMaxLength(20).IsRequired();
            e.Property(x => x.estado_envio).HasMaxLength(20).HasDefaultValue("pendiente");
            e.Property(x => x.metodo_envio).HasMaxLength(20).HasDefaultValue("inalambrico");
            e.Property(x => x.archivo_reducido_url).HasMaxLength(500);
            e.Property(x => x.archivo_ejecutable_url).HasMaxLength(500);
            e.Property(x => x.fecha_envio).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.compilacion).WithMany().HasForeignKey(x => x.compilacion_id).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.usuario).WithMany().HasForeignKey(x => x.usuario_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── credenciales_pdf ──────────────────────────────
        mb.Entity<credencial_pdf_entity>(e =>
        {
            e.ToTable("credenciales_pdf");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.canal_envio).HasMaxLength(20).IsRequired();
            e.Property(x => x.estado_envio).HasMaxLength(20).HasDefaultValue("pendiente");
            e.Property(x => x.archivo_url).HasMaxLength(500);
            e.Property(x => x.firma_electronica).HasMaxLength(1000);
            e.Property(x => x.fecha_generacion).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.usuario).WithMany().HasForeignKey(x => x.usuario_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── bitacora_accesos ──────────────────────────────
        mb.Entity<bitacora_acceso_entity>(e =>
        {
            e.ToTable("bitacora_accesos");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.metodo_login).HasMaxLength(20).IsRequired();
            e.Property(x => x.ip_origen).HasMaxLength(45);
            e.Property(x => x.user_agent).HasMaxLength(500);
            e.Property(x => x.fecha_ingreso).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.usuario).WithMany().HasForeignKey(x => x.usuario_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── bitacora_acciones ─────────────────────────────
        mb.Entity<bitacora_accion_entity>(e =>
        {
            e.ToTable("bitacora_acciones");
            e.HasKey(x => x.id);
            e.Property(x => x.id).ValueGeneratedOnAdd();
            e.Property(x => x.tipo_accion).HasMaxLength(50).IsRequired();
            e.Property(x => x.fecha_accion).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.usuario).WithMany().HasForeignKey(x => x.usuario_id).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.sesion).WithMany().HasForeignKey(x => x.sesion_id).OnDelete(DeleteBehavior.NoAction);
        });
    }
}