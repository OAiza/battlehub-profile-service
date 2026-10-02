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
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at").IsRequired();

            entity.HasMany(e => e.Permissions)
                .WithMany(p => p.Profiles)
                .UsingEntity(j => j.ToTable("profile_permissions"));
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(255).IsRequired();

            entity.HasData(
                new Permission { Code = "matches.create", Description = "Crear salas de partidas" },
                new Permission { Code = "games.typing.play", Description = "Jugar a Typing Battle" },
                new Permission { Code = "games.trivia.play", Description = "Jugar a Trivia Battle" },
                new Permission { Code = "games.memory.play", Description = "Jugar a Memory Match" }
            );
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.ToTable("games");
            entity.HasKey(e => e.GameType);
            entity.Property(e => e.GameType).HasColumnName("game_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.RequiredPermission).HasColumnName("required_permission").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Enabled).HasColumnName("enabled").IsRequired();

            entity.HasData(
                new Game { GameType = "typing", Name = "Typing Battle", RequiredPermission = "games.typing.play", Enabled = true },
                new Game { GameType = "trivia", Name = "Trivia Battle", RequiredPermission = "games.trivia.play", Enabled = true },
                new Game { GameType = "memory", Name = "Memory Match", RequiredPermission = "games.memory.play", Enabled = true }
            );
        });
    }
}

