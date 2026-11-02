using BattleHub.ProfileService.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.UnitTests.Data;

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
}
