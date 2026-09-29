using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Data.Converters;
using BattleHub.ProfileService.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.UnitTests;

/// <summary>
/// Valida el mapeo del modelo. Construir el modelo de EF Core no abre una conexión,
/// así que estas pruebas no necesitan MySQL: la cadena de conexión nunca se usa.
/// </summary>
[Trait("Category", "Unit")]
public class ProfileDbContextModelTests
{
    private static ProfileDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ProfileDbContext>()
            .UseMySQL("server=localhost;database=battlehub_profile_modelo;user=root;password=")
            .Options);

    [Theory]
    [InlineData(typeof(UserProfile), "user_profiles")]
    [InlineData(typeof(Permission), "permissions")]
    [InlineData(typeof(Game), "games")]
    [InlineData(typeof(UserProfilePermission), "user_profile_permissions")]
    public void Entidades_MapeanALaTablaEsperada(Type entityClrType, string expectedTable)
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(entityClrType);

        Assert.NotNull(entityType);
        Assert.Equal(expectedTable, entityType.GetTableName());
    }

    [Fact]
    public void Auth0UserId_TieneIndiceUnico()
    {
        using var context = CreateContext();

        var index = context.Model.FindEntityType(typeof(UserProfile))!
            .GetIndexes()
            .Single(index => index.Properties.Any(propiedad => propiedad.Name == nameof(UserProfile.Auth0UserId)));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Fechas_UsanElConversorUtc()
    {
        using var context = CreateContext();

        var createdAt = context.Model.FindEntityType(typeof(UserProfile))!
            .FindProperty(nameof(UserProfile.CreatedAtUtc))!;

        Assert.IsType<UtcDateTimeConverter>(createdAt.GetValueConverter());
    }

    [Fact]
    public void Fechas_SeLeenComoUtc()
    {
        using var context = CreateContext();

        var converter = context.Model.FindEntityType(typeof(UserProfile))!
            .FindProperty(nameof(UserProfile.CreatedAtUtc))!
            .GetValueConverter()!;

        var read = (DateTime)converter.ConvertFromProvider(new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Unspecified))!;

        Assert.Equal(DateTimeKind.Utc, read.Kind);
    }

    [Fact]
    public void Fechas_SeGuardanEnUtc()
    {
        using var context = CreateContext();

        var converter = context.Model.FindEntityType(typeof(UserProfile))!
            .FindProperty(nameof(UserProfile.CreatedAtUtc))!
            .GetValueConverter()!;

        var localTime = new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Local);
        var stored = (DateTime)converter.ConvertToProvider(localTime)!;

        Assert.Equal(localTime.ToUniversalTime(), stored);
    }

    [Fact]
    public void PerfilBorrado_ArrastraSusPermisosConcedidos()
    {
        using var context = CreateContext();

        var relationship = context.Model.FindEntityType(typeof(UserProfilePermission))!
            .GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(UserProfile));

        Assert.Equal(DeleteBehavior.Cascade, relationship.DeleteBehavior);
    }

    [Fact]
    public void PermisoUsadoPorUnJuego_NoSePuedeBorrar()
    {
        using var context = CreateContext();

        var relationship = context.Model.FindEntityType(typeof(Game))!
            .GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Permission));

        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }
}
