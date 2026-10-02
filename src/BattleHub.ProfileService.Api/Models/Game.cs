namespace BattleHub.ProfileService.Api.Models;

public class Game
{
    public string GameType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RequiredPermission { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

