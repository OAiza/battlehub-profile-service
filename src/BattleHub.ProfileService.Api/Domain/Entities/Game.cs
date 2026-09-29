namespace BattleHub.ProfileService.Api.Domain.Entities;

/// <summary>
/// Juego del catálogo. Se expone vía <c>GET /api/profiles/me/games</c>, filtrado por el
/// permiso que habilita cada juego.
/// </summary>
public class Game
{
    /// <summary>Código del juego; coincide con el <c>gameType</c> del contrato (<c>typing</c>, <c>trivia</c>, <c>memory</c>).</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    /// <summary>Permiso que debe tener el usuario para que el juego aparezca en su catálogo.</summary>
    public required string RequiredPermissionCode { get; set; }

    /// <summary>Permite retirar un juego del catálogo sin borrarlo ni perder su historial.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Orden de presentación en el Shell.</summary>
    public int SortOrder { get; set; }

    public Permission RequiredPermission { get; set; } = null!;
}
