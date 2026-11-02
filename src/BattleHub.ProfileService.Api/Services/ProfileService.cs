using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Dtos;
using BattleHub.ProfileService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.Api.Services;

public class ProfileService : IProfileService
{
    private readonly ProfileDbContext _context;
    private readonly IAuth0ManagementClient _auth0ManagementClient;

    public ProfileService(ProfileDbContext context, IAuth0ManagementClient auth0ManagementClient)
    {
        _context = context;
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

        var profile = await _context.Profiles
            .Include(p => p.Permissions)
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        bool created = false;
        var now = DateTime.UtcNow;

        if (profile == null)
        {
            created = true;
            profile = new Profile
            {
                Id = userId,
                DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : userId,
                Email = email,
                CreatedAt = now,
                LastLoginAt = now
            };

            // NOTA R-47 / R-48: Ya no se asignan permisos de juegos por defecto en MySQL.
            // Los permisos provienen exclusivamente de Auth0 Management API.
            _context.Profiles.Add(profile);
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
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (created)
        {
            // Otra llamada simultánea a /sync creó el mismo perfil primero.
            // Se descarta el alta y se continúa con el perfil que ya existe.
            _context.ChangeTracker.Clear();
            var alreadyExists = await _context.Profiles.AnyAsync(p => p.Id == userId, cancellationToken);
            if (!alreadyExists)
            {
                throw;
            }

            return await SyncProfileAsync(userId, displayName, email, cancellationToken);
        }

        var response = new ProfileResponse
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Email = profile.Email,
            CreatedAt = profile.CreatedAt,
            LastLoginAt = profile.LastLoginAt
        };

        return (response, created);
    }

    public async Task<ProfileResponse?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (profile == null)
        {
            return null;
        }

        return new ProfileResponse
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Email = profile.Email,
            CreatedAt = profile.CreatedAt,
            LastLoginAt = profile.LastLoginAt
        };
    }

    public async Task<PermissionResponse?> GetPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (profile == null)
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
        var profile = await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (profile == null)
        {
            return Array.Empty<GameResponse>();
        }

        var effectivePermissions = await _auth0ManagementClient.GetUserEffectivePermissionsAsync(userId, cancellationToken);
        var userPermissions = effectivePermissions.ToHashSet(StringComparer.Ordinal);

        var games = await _context.Games
            .AsNoTracking()
            .Where(g => g.Enabled)
            .ToListAsync(cancellationToken);

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

    public async Task<UserAccessResponse> GetUserAccessAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("El userId no puede ser nulo ni vacío.", nameof(userId));
        }

        var effectivePermissions = await _auth0ManagementClient.GetUserEffectivePermissionsAsync(userId, cancellationToken);
        var userPermissions = effectivePermissions.ToHashSet(StringComparer.Ordinal);

        var games = await _context.Games
            .AsNoTracking()
            .Where(g => g.Enabled)
            .ToListAsync(cancellationToken);

        var userGames = games
            .Where(g => userPermissions.Contains(g.RequiredPermission))
            .Select(g => new GameResponse
            {
                GameType = g.GameType,
                Name = g.Name,
                RequiredPermission = g.RequiredPermission
            })
            .ToList();

        return new UserAccessResponse
        {
            UserId = userId,
            Permissions = effectivePermissions,
            Games = userGames
        };
    }
}
