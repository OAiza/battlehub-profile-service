using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleHub.ProfileService.Api.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.ProfileService.IntegrationTests.MySql;

/// <summary>
/// Flujos de la API contra MySQL real (05-cicd-testing-commits.md §3 y ADR-006).
/// </summary>
[Trait("Category", "Integration")]
public class MySqlProfileEndpointTests : IClassFixture<MySqlDatabaseFixture>
{
    private readonly MySqlDatabaseFixture _database;

    public MySqlProfileEndpointTests(MySqlDatabaseFixture database)
    {
        _database = database;
    }

    [MySqlFact]
    public async Task SyncLuegoMe_ContraMySql_PersistePerfilConFechasEnUtc()
    {
        const string userId = "auth0|mysql-sync-me";
        await using var factory = CreateFactory();
        var client = CreateClient(factory, userId);

        var syncResponse = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Gamer MySQL" });
        Assert.Equal(HttpStatusCode.Created, syncResponse.StatusCode);

        var meResponse = await client.GetAsync("/api/profiles/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        using var json = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("id").GetString());
        Assert.Equal("Gamer MySQL", json.RootElement.GetProperty("displayName").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("lastLoginAt").GetString());
    }

    [MySqlFact]
    public async Task Perfil_SeConservaAlReiniciarElServicio()
    {
        const string userId = "auth0|mysql-reinicio";

        await using (var firstRun = CreateFactory())
        {
            var client = CreateClient(firstRun, userId);
            var syncResponse = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Antes del reinicio" });
            Assert.Equal(HttpStatusCode.Created, syncResponse.StatusCode);
        }

        await using var secondRun = CreateFactory();
        var clientAfterRestart = CreateClient(secondRun, userId);

        var profile = await clientAfterRestart.GetFromJsonAsync<ProfileResponse>("/api/profiles/me");

        Assert.NotNull(profile);
        Assert.Equal("Antes del reinicio", profile.DisplayName);
    }

    [MySqlFact]
    public async Task UsuarioNuevo_RecibePermisosYJuegosDeLaMigracion()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory, "auth0|mysql-catalogo");

        await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Catálogo" });

        var permissions = await client.GetFromJsonAsync<PermissionResponse>("/api/profiles/me/permissions");
        Assert.NotNull(permissions);
        Assert.Equal(
            new[] { "games.memory.play", "games.trivia.play", "games.typing.play", "matches.create" },
            permissions.Permissions.OrderBy(code => code));

        var games = await client.GetFromJsonAsync<List<GameResponse>>("/api/profiles/me/games");
        Assert.NotNull(games);
        Assert.Equal(new[] { "memory", "trivia", "typing" }, games.Select(g => g.GameType).OrderBy(type => type));
    }

    [MySqlFact]
    public async Task SyncRepetido_ContraMySql_NoDuplicaElPerfil()
    {
        const string userId = "auth0|mysql-repetido";
        await using var factory = CreateFactory();
        var client = CreateClient(factory, userId);

        var first = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Primera vez" });
        var second = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Segunda vez" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/profiles/me");
        Assert.NotNull(profile);
        Assert.Equal("Segunda vez", profile.DisplayName);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Un entorno distinto de Development: no carga user-secrets y exige configuración explícita.
            builder.UseEnvironment("Testing");
            builder.UseSetting("DatabaseProvider", "MySQL");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _database.ConnectionString);

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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }
}
