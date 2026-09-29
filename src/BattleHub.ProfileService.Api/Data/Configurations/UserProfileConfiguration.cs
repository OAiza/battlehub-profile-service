using BattleHub.ProfileService.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BattleHub.ProfileService.Api.Data.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(profile => profile.Auth0UserId)
            .HasColumnName("auth0_user_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(profile => profile.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(profile => profile.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(profile => profile.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(2048);

        builder.Property(profile => profile.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(profile => profile.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(profile => profile.LastSyncedAtUtc).HasColumnName("last_synced_at_utc");

        // El sub de Auth0 es la clave natural con la que /sync resuelve el perfil.
        builder.HasIndex(profile => profile.Auth0UserId)
            .IsUnique()
            .HasDatabaseName("ix_user_profiles_auth0_user_id");

        builder.HasIndex(profile => profile.Email)
            .IsUnique()
            .HasDatabaseName("ix_user_profiles_email");
    }
}
