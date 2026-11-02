using BattleHub.ProfileService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.Api.Data;

public class ProfileDbContext : DbContext
{
    public ProfileDbContext(DbContextOptions<ProfileDbContext> options)
        : base(options)
    {
    }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Game> Games => Set<Game>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Convención del proyecto: todas las fechas en UTC (ISO-8601 con sufijo Z).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");

            // R-13: la clave primaria es entera; el "sub" de Auth0 queda como columna única.
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

            entity.Property(e => e.Auth0UserId).HasColumnName("auth0_user_id").HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Auth0UserId).IsUnique().HasDatabaseName("UX_profiles_auth0_user_id");

            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at").IsRequired();
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.ToTable("games");

            // R-13: la clave primaria es entera; el tipo de juego queda como columna única.
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

            entity.Property(e => e.GameType).HasColumnName("game_type").HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.GameType).IsUnique().HasDatabaseName("UX_games_game_type");

            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.RequiredPermission).HasColumnName("required_permission").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Enabled).HasColumnName("enabled").IsRequired();

            // Catálogo de juegos: es el contrato con el Shell, no datos de ejemplo, así que
            // viaja en la migración. La generación de datos de prueba (R-28) va aparte, en
            // docker/datos/generar-datos.sql.
            entity.HasData(
                new Game { Id = 1, GameType = "typing", Name = "Typing Battle", RequiredPermission = "games.typing.play", Enabled = true },
                new Game { Id = 2, GameType = "trivia", Name = "Trivia Battle", RequiredPermission = "games.trivia.play", Enabled = true },
                new Game { Id = 3, GameType = "memory", Name = "Memory Match", RequiredPermission = "games.memory.play", Enabled = true }
            );
        });
    }
}
