namespace BattleHub.ProfileService.Api.Dtos;

public record UserAccessResponse
{
    public string UserId { get; init; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<GameResponse> Games { get; init; } = Array.Empty<GameResponse>();
}
