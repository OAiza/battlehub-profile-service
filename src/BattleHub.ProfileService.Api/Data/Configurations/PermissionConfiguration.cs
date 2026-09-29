using BattleHub.ProfileService.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BattleHub.ProfileService.Api.Data.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(permission => permission.Code);

        builder.Property(permission => permission.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasColumnName("description")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(permission => permission.GrantedByDefault)
            .HasColumnName("granted_by_default");

        builder.HasIndex(permission => permission.GrantedByDefault)
            .HasDatabaseName("ix_permissions_granted_by_default");
    }
}
