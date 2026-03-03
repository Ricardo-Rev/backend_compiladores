using Microsoft.EntityFrameworkCore;
using umg_basic_rover_domain.entities;

namespace umg_basic_rover_infrastructure.persistence.context;

public class rover_db_context : DbContext
{
    public rover_db_context(DbContextOptions<rover_db_context> options)
        : base(options)
    {
    }

    public DbSet<user_entity> users => Set<user_entity>();

    protected override void OnModelCreating(ModelBuilder model_builder)
    {
        base.OnModelCreating(model_builder);

        model_builder.Entity<user_entity>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(e => e.id);

            entity.Property(e => e.id)
                .HasColumnName("id");

            entity.Property(e => e.name)
                .HasColumnName("name")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.email)
                .HasColumnName("email")
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(e => e.email)
                .IsUnique();
        });
    }
}