using BattleHub.ProfileService.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BattleHub.ProfileService.Api.Data.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("games");

        builder.HasKey(game => game.Code);

        builder.Property(game => game.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(game => game.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(game => game.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(game => game.RequiredPermissionCode)
            .HasColumnName("required_permission_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(game => game.IsActive).HasColumnName("is_active");
        builder.Property(game => game.SortOrder).HasColumnName("sort_order");

        // Restrict: un permiso referenciado por un juego no se puede borrar sin retirar el juego antes.
        builder.HasOne(game => game.RequiredPermission)
            .WithMany()
            .HasForeignKey(game => game.RequiredPermissionCode)
            .HasConstraintName("fk_games_required_permission")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
