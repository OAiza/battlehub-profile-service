using BattleHub.ProfileService.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BattleHub.ProfileService.Api.Data.Configurations;

public class UserProfilePermissionConfiguration : IEntityTypeConfiguration<UserProfilePermission>
{
    public void Configure(EntityTypeBuilder<UserProfilePermission> builder)
    {
        builder.ToTable("user_profile_permissions");

        builder.HasKey(grant => new { grant.UserProfileId, grant.PermissionCode });

        builder.Property(grant => grant.UserProfileId).HasColumnName("user_profile_id");

        builder.Property(grant => grant.PermissionCode)
            .HasColumnName("permission_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(grant => grant.GrantedAtUtc).HasColumnName("granted_at_utc");

        // Si se elimina el perfil, sus concesiones se van con él.
        builder.HasOne(grant => grant.UserProfile)
            .WithMany(profile => profile.Permissions)
            .HasForeignKey(grant => grant.UserProfileId)
            .HasConstraintName("fk_user_profile_permissions_user_profile")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(grant => grant.Permission)
            .WithMany(permission => permission.UserProfiles)
            .HasForeignKey(grant => grant.PermissionCode)
            .HasConstraintName("fk_user_profile_permissions_permission")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(grant => grant.PermissionCode)
            .HasDatabaseName("ix_user_profile_permissions_permission_code");
    }
}
