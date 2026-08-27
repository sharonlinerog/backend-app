using CreditosApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditosApi.Data;

/// <summary>
/// Puente entre las clases de C# (Models) y las tablas reales en PostgreSQL.
/// Todo lo que el Controller le pide guardar/leer pasa por aquí, y EF Core
/// se encarga de traducirlo a SQL parametrizado (esto evita inyección SQL
/// automáticamente, sin que tengamos que escapar nada a mano).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Credito> Creditos => Set<Credito>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Credito>(entity =>
        {
            // Este mapeo debe coincidir exactamente con db/schema.sql
            entity.ToTable("creditos");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(c => c.NombreCliente)
                  .HasColumnName("nombre_cliente")
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(c => c.Cedula)
                  .HasColumnName("cedula")
                  .HasMaxLength(30)
                  .IsRequired();

            entity.Property(c => c.ValorCredito)
                  .HasColumnName("valor_credito")
                  .HasColumnType("numeric(18,2)")
                  .IsRequired();

            entity.Property(c => c.TasaInteres)
                  .HasColumnName("tasa_interes")
                  .HasColumnType("numeric(5,2)")
                  .IsRequired();

            entity.Property(c => c.PlazoMeses)
                  .HasColumnName("plazo_meses")
                  .IsRequired();

            entity.Property(c => c.Comercial)
                  .HasColumnName("comercial")
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(c => c.FechaRegistro)
                  .HasColumnName("fecha_registro")
                  .HasDefaultValueSql("now()")
                  .IsRequired();

            entity.HasIndex(c => c.NombreCliente);
            entity.HasIndex(c => c.Cedula);
            entity.HasIndex(c => c.Comercial);
            entity.HasIndex(c => c.FechaRegistro);
        });
    }
}
