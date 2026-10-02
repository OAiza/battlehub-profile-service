using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BattleHub.ProfileService.Api.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BattleHub.ProfileService.IntegrationTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string DefaultUserId = "auth0|integration-user-001";
    public const string DefaultUserName = "Francisco Integracion";
    public const string DefaultUserEmail = "francisco.test@ejemplo.com";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var customUserId)
            ? customUserId.ToString()
            : DefaultUserId;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId),
            new Claim(ClaimTypes.Name, DefaultUserName),
            new Claim("name", DefaultUserName),
            new Claim(ClaimTypes.Email, DefaultUserEmail),
            new Claim("email", DefaultUserEmail)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

[Trait("Category", "Integration")]
public class ProfileEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProfileEndpointTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "ProfileIntegrationDb_" + Guid.NewGuid().ToString("N");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "InMemory",
                    ["InMemoryDatabaseName"] = dbName
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
            });
        });

    }

    [Fact]
    public async Task PostSync_NuevoUsuario_Responde201CreatedYDevuelvePerfil()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "auth0|user-sync-new");

        var response = await client.PostAsJsonAsync("/api/profiles/sync", new
        {
            displayName = "Nuevo Gamer",
            email = "gamer@ejemplo.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("auth0|user-sync-new", profile.Id);
        Assert.Equal("Nuevo Gamer", profile.DisplayName);
        Assert.Equal("gamer@ejemplo.com", profile.Email);
    }

    [Fact]
    public async Task FlujoCompleto_SyncLuegoMe_Responde200OkConMismoPerfil()
    {
        var client = _factory.CreateClient();
        var userId = "auth0|user-full-flow";
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        // 1. Sync inicial
        var syncResponse = await client.PostAsJsonAsync("/api/profiles/sync", new
        {
            displayName = "Gamer Flujo",
            email = "flujo@ejemplo.com"
        });
        Assert.Equal(HttpStatusCode.Created, syncResponse.StatusCode);

        // 2. Consulta GET /api/profiles/me
        var meResponse = await client.GetAsync("/api/profiles/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var profile = await meResponse.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(userId, profile.Id);
        Assert.Equal("Gamer Flujo", profile.DisplayName);

        // 3. Segundo sync es idempotente (200 OK)
        var secondSyncResponse = await client.PostAsJsonAsync("/api/profiles/sync", new
        {
            displayName = "Gamer Flujo Actualizado"
        });
        Assert.Equal(HttpStatusCode.OK, secondSyncResponse.StatusCode);
    }

    [Fact]
    public async Task GetMe_UsuarioNoSincronizado_Responde404NotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "auth0|sin-perfil-" + Guid.NewGuid());

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetPermissions_UsuarioSincronizado_RespondePermisosPorDefecto()
    {
        var client = _factory.CreateClient();
        var userId = "auth0|user-permissions";
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        // Sincronizar
        await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Perm User" });

        // Consultar permisos
        var response = await client.GetAsync("/api/profiles/me/permissions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var permissions = await response.Content.ReadFromJsonAsync<PermissionResponse>();
        Assert.NotNull(permissions);
        Assert.Contains("matches.create", permissions.Permissions);
        Assert.Contains("games.typing.play", permissions.Permissions);
        Assert.Contains("games.trivia.play", permissions.Permissions);
        Assert.Contains("games.memory.play", permissions.Permissions);
    }

    [Fact]
    public async Task GetGames_UsuarioSincronizado_RespondeCatalogoDeJuegosHabilitados()
    {
        var client = _factory.CreateClient();
        var userId = "auth0|user-games";
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        // Sincronizar
        await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Games User" });

        // Consultar juegos
        var response = await client.GetAsync("/api/profiles/me/games");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var games = await response.Content.ReadFromJsonAsync<List<GameResponse>>();
        Assert.NotNull(games);
        Assert.True(games.Count >= 3);
        Assert.Contains(games, g => g.GameType == "typing");
        Assert.Contains(games, g => g.GameType == "trivia");
        Assert.Contains(games, g => g.GameType == "memory");
    }

    [Fact]
    public async Task Preflight_PermiteShellYRechazaOrigenAjeno()
    {
        var client = _factory.CreateClient();

        var permitido = new HttpRequestMessage(HttpMethod.Options, "/api/profiles/sync");
        permitido.Headers.Add("Origin", "http://localhost:4000");
        permitido.Headers.Add("Access-Control-Request-Method", "POST");
        var respuestaPermitida = await client.SendAsync(permitido);

        Assert.True(respuestaPermitida.Headers.TryGetValues("Access-Control-Allow-Origin", out var origenes));
        Assert.Equal("http://localhost:4000", Assert.Single(origenes));

        var ajeno = new HttpRequestMessage(HttpMethod.Options, "/api/profiles/sync");
        ajeno.Headers.Add("Origin", "https://untrusted.example");
        ajeno.Headers.Add("Access-Control-Request-Method", "POST");
        var respuestaAjena = await client.SendAsync(ajeno);

        Assert.False(respuestaAjena.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
