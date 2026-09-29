using System.Reflection;
using BattleHub.ProfileService.Api.Data.Converters;
using BattleHub.ProfileService.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.Api.Data;

public class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<UserProfilePermission> UserProfilePermissions => Set<UserProfilePermission>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Convención del proyecto: fechas en UTC con precisión de microsegundos (datetime(6) en MySQL).
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>()
            .HavePrecision(6);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
