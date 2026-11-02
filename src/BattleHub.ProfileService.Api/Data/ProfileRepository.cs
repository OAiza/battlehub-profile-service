using BattleHub.ProfileService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.Api.Data;

public class ProfileRepository : IProfileRepository
{
    private readonly ProfileDbContext _context;

    public ProfileRepository(ProfileDbContext context)
    {
        _context = context;
    }

    public Task<Profile?> GetByAuth0UserIdAsync(string auth0UserId, CancellationToken cancellationToken = default)
    {
        return _context.Profiles.FirstOrDefaultAsync(p => p.Auth0UserId == auth0UserId, cancellationToken);
    }

    public Task<bool> ExistsAsync(string auth0UserId, CancellationToken cancellationToken = default)
    {
        return _context.Profiles.AnyAsync(p => p.Auth0UserId == auth0UserId, cancellationToken);
    }

    public void Add(Profile profile)
    {
        _context.Profiles.Add(profile);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public void DiscardChanges()
    {
        _context.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<Game>> GetEnabledGamesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Games
            .AsNoTracking()
            .Where(g => g.Enabled)
            .ToListAsync(cancellationToken);
    }
}
