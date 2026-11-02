using BattleHub.ProfileService.Api.Services;
using Microsoft.AspNetCore.Http;

namespace BattleHub.ProfileService.IntegrationTests;

public class TestAuth0ManagementClient : IAuth0ManagementClient
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TestAuth0ManagementClient(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<IReadOnlyList<string>> GetUserEffectivePermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            if (context.Request.Headers.ContainsKey("X-Test-Auth0-Fail"))
            {
                throw new Auth0ManagementException("Fallo simulado al consultar Auth0 Management API.");
            }

            if (context.Request.Headers.ContainsKey("X-Test-Zero-Permissions"))
            {
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }

            if (context.Request.Headers.TryGetValue("X-Test-Permissions", out var customPerms))
            {
                var perms = customPerms.ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return Task.FromResult<IReadOnlyList<string>>(perms);
            }
        }

        // Por defecto para pruebas de integración existentes
        var defaultPerms = new[] { "matches.create", "games.typing.play", "games.trivia.play", "games.memory.play" };
        return Task.FromResult<IReadOnlyList<string>>(defaultPerms);
    }
}
