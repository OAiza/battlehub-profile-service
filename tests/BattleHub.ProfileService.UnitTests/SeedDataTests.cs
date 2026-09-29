using BattleHub.ProfileService.Api.Data;

namespace BattleHub.ProfileService.UnitTests;

[Trait("Category", "Unit")]
public class SeedDataTests
{
    [Fact]
    public void Permisos_CubrenLosCodigosDelContrato()
    {
        var codes = SeedData.Permissions.Select(permission => permission.Code).ToArray();

        Assert.Equal(
            ["games.memory.play", "games.trivia.play", "games.typing.play", "matches.create"],
            codes.Order().ToArray());
    }

    [Fact]
    public void Permisos_NoTienenCodigosDuplicados()
    {
        Assert.Equal(
            SeedData.Permissions.Count,
            SeedData.Permissions.Select(permission => permission.Code).Distinct().Count());
    }

    [Fact]
    public void Permisos_SeConcedenTodosPorDefecto()
    {
        Assert.All(SeedData.Permissions, permission => Assert.True(permission.GrantedByDefault));
    }

    [Fact]
    public void Juegos_UsanLosGameTypeDelContrato()
    {
        var codes = SeedData.Games.Select(game => game.Code).ToArray();

        Assert.Equal(["memory", "trivia", "typing"], codes.Order().ToArray());
    }

    [Fact]
    public void Juegos_ReferencianUnPermisoExistente()
    {
        var permissionCodes = SeedData.Permissions.Select(permission => permission.Code).ToHashSet();

        Assert.All(SeedData.Games, game => Assert.Contains(game.RequiredPermissionCode, permissionCodes));
    }

    [Fact]
    public void Juegos_TienenOrdenDePresentacionUnico()
    {
        var sortOrders = SeedData.Games.Select(game => game.SortOrder).ToArray();

        Assert.Equal(sortOrders.Length, sortOrders.Distinct().Count());
    }

    [Fact]
    public void Juegos_ArrancanActivos()
    {
        Assert.All(SeedData.Games, game => Assert.True(game.IsActive));
    }
}
