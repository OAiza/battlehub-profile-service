using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Dtos;
using BattleHub.ProfileService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.Api.Services;

public class ProfileService : IProfileService
{
    private readonly IProfileRepository _repository;
    private readonly IAuth0ManagementClient _auth0ManagementClient;

    public ProfileService(IProfileRepository repository, IAuth0ManagementClient auth0ManagementClient)
    {
        _repository = repository;
        _auth0ManagementClient = auth0ManagementClient;
    }

    public async Task<(ProfileResponse Profile, bool Created)> SyncProfileAsync(
        string userId,
        string? displayName,
        string? email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("El userId no puede ser nulo ni vacío.", nameof(userId));
        }

        var profile = await _repository.GetByAuth0UserIdAsync(userId, cancellationToken);

        bool created = false;
        var now = DateTime.UtcNow;

        if (profile == null)
        {
            created = true;
            profile = new Profile
            {
                Auth0UserId = userId,
                DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : userId,
                Email = email,
                CreatedAt = now,
                LastLoginAt = now
            };

            // R-47 / R-48: /sync solo crea o actualiza el perfil. Los permisos y juegos se leen
            // de Auth0 en cada consulta, así que aquí no se concede ni se restaura ninguno.
            _repository.Add(profile);
        }
        else
        {
            profile.LastLoginAt = now;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                profile.DisplayName = displayName;
            }
            if (!string.IsNullOrWhiteSpace(email))
            {
                profile.Email = email;
            }
        }

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (created)
        {
            // Otra llamada simultánea a /sync creó el mismo perfil primero.
            // Se descarta el alta y se continúa con el perfil que ya existe.
            _repository.DiscardChanges();
            var alreadyExists = await _repository.ExistsAsync(userId, cancellationToken);
            if (!alreadyExists)
            {
                throw;
            }

            return await SyncProfileAsync(userId, displayName, email, cancellationToken);
        }

        return (ToResponse(profile), created);
    }

    public async Task<ProfileResponse?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByAuth0UserIdAsync(userId, cancellationToken);
        return profile == null ? null : ToResponse(profile);
    }

    public async Task<PermissionResponse?> GetPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!await _repository.ExistsAsync(userId, cancellationToken))
        {
            return null;
        }

        var effectivePermissions = await _auth0ManagementClient.GetUserEffectivePermissionsAsync(userId, cancellationToken);

        return new PermissionResponse
        {
            Permissions = effectivePermissions.ToList()
        };
    }

    public async Task<IReadOnlyList<GameResponse>> GetEnabledGamesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!await _repository.ExistsAsync(userId, cancellationToken))
        {
            return Array.Empty<GameResponse>();
        }

        var effectivePermissions = await _auth0ManagementClient.GetUserEffectivePermissionsAsync(userId, cancellationToken);
        return await GetGamesAllowedByAsync(effectivePermissions, cancellationToken);
    }

    public async Task<UserAccessResponse> GetUserAccessAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("El userId no puede ser nulo ni vacío.", nameof(userId));
        }

        var effectivePermissions = await _auth0ManagementClient.GetUserEffectivePermissionsAsync(userId, cancellationToken);

        return new UserAccessResponse
        {
            UserId = userId,
            Permissions = effectivePermissions,
            Games = await GetGamesAllowedByAsync(effectivePermissions, cancellationToken)
        };
    }

    /// <summary>
    /// Cruza los permisos de Auth0 con el catálogo habilitado: un juego aparece solo si el
    /// usuario tiene exactamente el permiso que ese juego requiere.
    /// </summary>
    private async Task<IReadOnlyList<GameResponse>> GetGamesAllowedByAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken)
    {
        var userPermissions = permissions.ToHashSet(StringComparer.Ordinal);
        var games = await _repository.GetEnabledGamesAsync(cancellationToken);

        return games
            .Where(g => userPermissions.Contains(g.RequiredPermission))
            .Select(g => new GameResponse
            {
                GameType = g.GameType,
                Name = g.Name,
                RequiredPermission = g.RequiredPermission
            })
            .ToList();
    }

    private static ProfileResponse ToResponse(Profile profile)
    {
        return new ProfileResponse
        {
            Id = profile.Auth0UserId,
            DisplayName = profile.DisplayName,
            Email = profile.Email,
            CreatedAt = profile.CreatedAt,
            LastLoginAt = profile.LastLoginAt
        };
    }
}
