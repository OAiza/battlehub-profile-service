using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Dtos;
using BattleHub.ProfileService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.Api.Services;

public class ProfileService : IProfileService
{
    private readonly ProfileDbContext _context;

    public ProfileService(ProfileDbContext context)
    {
        _context = context;
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

            var defaultPermissions = await _context.Permissions.ToListAsync(cancellationToken);
            if (defaultPermissions.Count == 0)
            {
                var defaultCodes = new[] { "matches.create", "games.typing.play", "games.trivia.play", "games.memory.play" };
                defaultPermissions = defaultCodes.Select(code => new Permission { Code = code, Description = code }).ToList();
                _context.Permissions.AddRange(defaultPermissions);
            }

            foreach (var perm in defaultPermissions)
            {
                profile.Permissions.Add(perm);
            }

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

        await _context.SaveChangesAsync(cancellationToken);

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
            .Include(p => p.Permissions)
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (profile == null)
        {
            return null;
        }

        return new PermissionResponse
        {
            Permissions = profile.Permissions.Select(p => p.Code).ToList()
        };
    }

    public async Task<IReadOnlyList<GameResponse>> GetEnabledGamesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _context.Profiles
            .AsNoTracking()
            .Include(p => p.Permissions)
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (profile == null)
        {
            return Array.Empty<GameResponse>();
        }

        var userPermissions = profile.Permissions.Select(p => p.Code).ToHashSet();

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
}

