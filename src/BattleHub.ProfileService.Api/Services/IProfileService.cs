using BattleHub.ProfileService.Api.Dtos;

namespace BattleHub.ProfileService.Api.Services;

public interface IProfileService
{
    Task<(ProfileResponse Profile, bool Created)> SyncProfileAsync(
        string userId,
        string? displayName,
        string? email,
        CancellationToken cancellationToken = default);

    Task<ProfileResponse?> GetProfileAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<PermissionResponse?> GetPermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameResponse>> GetEnabledGamesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

