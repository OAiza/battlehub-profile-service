namespace BattleHub.ProfileService.Api.Services;

/// <summary>
/// Permisos básicos que recibe todo jugador nuevo al sincronizarse.
/// La lista es explícita para que un permiso agregado a la tabla (por ejemplo,
/// de moderación o administración) no se conceda a todos solo por existir.
/// </summary>
public static class PlayerPermissions
{
    public static readonly IReadOnlyList<string> Default =
    [
        "matches.create",
        "games.typing.play",
        "games.trivia.play",
        "games.memory.play"
    ];
}
