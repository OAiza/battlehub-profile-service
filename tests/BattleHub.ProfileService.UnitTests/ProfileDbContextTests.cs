using BattleHub.ProfileService.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BattleHub.ProfileService.UnitTests;

[Trait("Category", "Unit")]
public class ProfileDbContextTests
{
    [Fact]
    public void Modelo_NoTieneCambiosSinMigracion()
    {
        // Compara el modelo de EF Core con la última migración; no se conecta a MySQL.
        var options = new DbContextOptionsBuilder<ProfileDbContext>()
            .UseMySQL("Server=localhost;Database=battlehub_profiles;")
            .Options;
        using var context = new ProfileDbContext(options);

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "El modelo cambió y falta la migración: ejecute 'dotnet ef migrations add <Nombre>'.");
    }

    [Fact]
    public void UtcDateTimeConverter_FechaLeidaDeMySql_QuedaMarcadaComoUtc()
    {
        var converter = new UtcDateTimeConverter();
        var leidaDeMySql = new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Unspecified);

        var resultado = (DateTime)converter.ConvertFromProvider(leidaDeMySql)!;

        Assert.Equal(DateTimeKind.Utc, resultado.Kind);
        Assert.Equal(leidaDeMySql.Ticks, resultado.Ticks);
    }

    [Fact]
    public void UtcDateTimeConverter_FechaLocal_SeGuardaEnUtc()
    {
        var converter = new UtcDateTimeConverter();
        var local = new DateTime(2026, 9, 2, 14, 0, 0, DateTimeKind.Local);

        var guardada = (DateTime)converter.ConvertToProvider(local)!;

        Assert.Equal(local.ToUniversalTime(), guardada);
    }
}
