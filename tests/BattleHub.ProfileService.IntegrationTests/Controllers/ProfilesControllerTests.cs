using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleHub.ProfileService.Api.Dtos;
using BattleHub.ProfileService.IntegrationTests.Infrastructure;

namespace BattleHub.ProfileService.IntegrationTests.Controllers;

/// <summary>
/// Endpoints de <c>/api/profiles</c> de punta a punta: token JWT real firmado con la clave de
/// prueba, MySQL real y la Management API de Auth0 simulada (R-18). Cubren la lista de
/// evidencia acordada con el Shell para la prueba conjunta (R-43, R-49).
/// </summary>
[Trait("Category", "Integration")]
[Collection(MySqlCollection.Name)]
public sealed class ProfilesControllerTests : IDisposable
{
    // Permisos tal como los asigna el tenant: cada juego en su propia API y matches.create en
    // la API de Profile.
    private static readonly (string Api, string Permission) TypingPlay = (TestHost.TypingApi, "games.typing.play");
    private static readonly (string Api, string Permission) TriviaPlay = (TestHost.TriviaApi, "games.trivia.play");
    private static readonly (string Api, string Permission) MemoryPlay = (TestHost.MemoryApi, "games.memory.play");
    private static readonly (string Api, string Permission) MatchesCreate = (TestHost.Auth0Audience, "matches.create");

    private readonly ProfileApiFactory _factory;

    public ProfilesControllerTests(MySqlDatabaseFixture database)
    {
        _factory = new ProfileApiFactory(database.ConnectionString);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<UserAccessResponse> GetAccessAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/profiles/me/access");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var access = await response.Content.ReadFromJsonAsync<UserAccessResponse>();
        Assert.NotNull(access);
        return access;
    }

    // --- POST /api/profiles/sync y GET /api/profiles/me ---

    [MySqlFact]
    public async Task PostSync_NuevoUsuario_Responde201YDevuelvePerfil()
    {
        var client = _factory.CreateClientFor("auth0|user-sync-new");

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

    [MySqlFact]
    public async Task SyncLuegoMe_DevuelveElMismoPerfilConFechasEnUtc()
    {
        const string userId = "auth0|user-full-flow";
        var client = _factory.CreateClientFor(userId);

        var sync = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Gamer Flujo" });
        Assert.Equal(HttpStatusCode.Created, sync.StatusCode);

        var me = await client.GetAsync("/api/profiles/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        using var json = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("id").GetString());
        Assert.Equal("Gamer Flujo", json.RootElement.GetProperty("displayName").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("lastLoginAt").GetString());
    }

    [MySqlFact]
    public async Task SyncRepetido_Responde200YActualizaSinDuplicar()
    {
        var client = _factory.CreateClientFor("auth0|user-sync-repetido");

        var first = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Primera vez" });
        var second = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Segunda vez" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/profiles/me");
        Assert.Equal("Segunda vez", profile?.DisplayName);
    }

    [MySqlFact]
    public async Task GetMe_UsuarioNoSincronizado_Responde404ConProblemDetails()
    {
        var client = _factory.CreateClientFor("auth0|sin-perfil-" + Guid.NewGuid());

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [MySqlFact]
    public async Task PostSync_ClienteM2M_Responde403YNoCreaPerfil()
    {
        var client = _factory.CreateClientFor(
            "abc123XYZ@clients",
            new Dictionary<string, object> { ["gty"] = "client-credentials" });

        var sync = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Servicio" });
        Assert.Equal(HttpStatusCode.Forbidden, sync.StatusCode);
        Assert.Equal("application/problem+json", sync.Content.Headers.ContentType?.MediaType);

        var me = await client.GetAsync("/api/profiles/me");
        Assert.Equal(HttpStatusCode.NotFound, me.StatusCode);
    }

    // --- GET /api/profiles/me/access: contrato con el Shell ---

    [MySqlFact]
    public async Task GetAccess_Usuario1SinPermisos_Responde200ConListasVacias()
    {
        const string userId = "auth0|demo-usuario-1";
        var client = _factory.CreateClientFor(userId);

        var access = await GetAccessAsync(client);

        Assert.Equal(userId, access.UserId);
        Assert.Empty(access.Permissions);
        Assert.Empty(access.Games);
    }

    [MySqlFact]
    public async Task GetAccess_Usuario2_RespondeSoloTyping()
    {
        const string userId = "auth0|demo-usuario-2";
        _factory.Auth0.SetPermissions(userId, TypingPlay, MatchesCreate);

        var access = await GetAccessAsync(_factory.CreateClientFor(userId));

        Assert.Equal(userId, access.UserId);
        Assert.Equal(new[] { "games.typing.play", "matches.create" }, access.Permissions.OrderBy(p => p));
        var game = Assert.Single(access.Games);
        Assert.Equal("typing", game.GameType);
        Assert.Equal("Typing Battle", game.Name);
        Assert.Equal("games.typing.play", game.RequiredPermission);
    }

    [MySqlFact]
    public async Task GetAccess_Usuario3_RespondeLosTresJuegos()
    {
        const string userId = "auth0|demo-usuario-3";
        _factory.Auth0.SetPermissions(userId, TypingPlay, TriviaPlay, MemoryPlay, MatchesCreate);

        var access = await GetAccessAsync(_factory.CreateClientFor(userId));

        Assert.Equal(4, access.Permissions.Count);
        Assert.Equal(new[] { "memory", "trivia", "typing" }, access.Games.Select(g => g.GameType).OrderBy(t => t));
    }

    [MySqlFact]
    public async Task GetAccess_FormatoJson_CoincideConElContratoDelShell()
    {
        const string userId = "auth0|formato-contrato";
        _factory.Auth0.SetPermissions(userId, TypingPlay, MatchesCreate);

        var response = await _factory.CreateClientFor(userId).GetAsync("/api/profiles/me/access");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(new[] { "games", "permissions", "userId" }, root.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.Equal(userId, root.GetProperty("userId").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("permissions").ValueKind);
        var game = Assert.Single(root.GetProperty("games").EnumerateArray());
        Assert.Equal(new[] { "gameType", "name", "requiredPermission" }, game.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.Equal("typing", game.GetProperty("gameType").GetString());
    }

    [MySqlFact]
    public async Task GetAccess_RespetaLaIdentidadDelToken()
    {
        // El usuario se toma siempre del token: un parámetro con otro sub no cambia la respuesta.
        _factory.Auth0.SetPermissions("auth0|otra-cuenta", TypingPlay, TriviaPlay, MemoryPlay);
        var client = _factory.CreateClientFor("auth0|cuenta-propia");

        var response = await client.GetAsync("/api/profiles/me/access?userId=auth0%7Cotra-cuenta");
        var access = await response.Content.ReadFromJsonAsync<UserAccessResponse>();

        Assert.NotNull(access);
        Assert.Equal("auth0|cuenta-propia", access.UserId);
        Assert.Empty(access.Games);
    }

    [MySqlFact]
    public async Task GetAccess_AceptaLosPermisosDeLasTresApisDeJuegosYRechazaCombinacionesAjenas()
    {
        const string userId = "auth0|combinaciones";
        _factory.Auth0.SetPermissions(
            userId,
            TypingPlay,
            TriviaPlay,
            MemoryPlay,
            // El nombre correcto emitido por otra API no cuenta.
            ("https://api.ajena.example/typing", "games.typing.play"),
            // Un permiso de juego en la API de otro juego tampoco.
            (TestHost.TypingApi, "games.memory.play"),
            // Ni un permiso que no es del contrato, aunque venga de una API conocida.
            (TestHost.TriviaApi, "admin.all"));

        var access = await GetAccessAsync(_factory.CreateClientFor(userId));

        Assert.Equal(new[] { "games.memory.play", "games.trivia.play", "games.typing.play" }, access.Permissions.OrderBy(p => p));
        Assert.DoesNotContain("admin.all", access.Permissions);
        Assert.Equal(3, access.Games.Count);
    }

    [MySqlFact]
    public async Task GetAccess_SoloMemoryEnLaApiDeTyping_NoHabilitaMemory()
    {
        const string userId = "auth0|api-cruzada";
        _factory.Auth0.SetPermissions(userId, (TestHost.TypingApi, "games.memory.play"));

        var access = await GetAccessAsync(_factory.CreateClientFor(userId));

        Assert.Empty(access.Permissions);
        Assert.Empty(access.Games);
    }

    [MySqlFact]
    public async Task GetAccess_RecorreTodasLasPaginasDeAuth0()
    {
        // Auth0 entrega como máximo 100 permisos por página: el permiso válido queda en la segunda.
        const string userId = "auth0|muchos-permisos";
        var relleno = Enumerable.Range(0, 100).Select(i => ("https://api.otra.example/" + i, "otro.permiso"));
        _factory.Auth0.SetPermissions(userId, relleno.Append(TriviaPlay));

        var access = await GetAccessAsync(_factory.CreateClientFor(userId));

        Assert.Equal("games.trivia.play", Assert.Single(access.Permissions));
        Assert.Equal("trivia", Assert.Single(access.Games).GameType);
    }

    [MySqlFact]
    public async Task GetAccess_ReutilizaElTokenM2MEntreConsultas()
    {
        _factory.Auth0.SetPermissions("auth0|cache-a", TypingPlay);
        _factory.Auth0.SetPermissions("auth0|cache-b", MemoryPlay);

        await GetAccessAsync(_factory.CreateClientFor("auth0|cache-a"));
        await GetAccessAsync(_factory.CreateClientFor("auth0|cache-b"));
        await GetAccessAsync(_factory.CreateClientFor("auth0|cache-a"));

        Assert.Equal(1, _factory.Auth0.TokenRequests);
    }

    [MySqlFact]
    public async Task GetAccess_FalloDeAuth0_Responde503ProblemDetailsSinConcederNada()
    {
        const string userId = "auth0|auth0-caido";
        _factory.Auth0.SetPermissions(userId, TypingPlay, TriviaPlay, MemoryPlay, MatchesCreate);
        _factory.Auth0.Unavailable = true;

        var response = await _factory.CreateClientFor(userId).GetAsync("/api/profiles/me/access");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("games.", body);
        Assert.DoesNotContain("matches.create", body);
    }

    [MySqlFact]
    public async Task GetAccess_ClienteM2M_Responde403()
    {
        var client = _factory.CreateClientFor("serv-m2m@clients");

        var response = await client.GetAsync("/api/profiles/me/access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // --- /sync no concede permisos ---

    [MySqlFact]
    public async Task Sync_NoConcedeJuegosNiPermisos()
    {
        const string userId = "auth0|sync-sin-permisos";
        var client = _factory.CreateClientFor(userId);

        var sync = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Sin permisos" });
        Assert.Equal(HttpStatusCode.Created, sync.StatusCode);

        var access = await GetAccessAsync(client);
        Assert.Empty(access.Permissions);
        Assert.Empty(access.Games);
        Assert.Empty((await client.GetFromJsonAsync<PermissionResponse>("/api/profiles/me/permissions"))!.Permissions);
        Assert.Empty((await client.GetFromJsonAsync<List<GameResponse>>("/api/profiles/me/games"))!);
    }

    [MySqlFact]
    public async Task PermisoRetiradoEnAuth0_SeReflejaYNoReapareceAlVolverASincronizar()
    {
        const string userId = "auth0|permiso-retirado";
        var client = _factory.CreateClientFor(userId);

        // 1. El usuario entra con los tres juegos habilitados.
        _factory.Auth0.SetPermissions(userId, TypingPlay, TriviaPlay, MemoryPlay, MatchesCreate);
        await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Permiso Retirado" });
        Assert.Equal(3, (await GetAccessAsync(client)).Games.Count);

        // 2. Le retiran games.trivia.play en Auth0: la siguiente consulta ya no lo incluye.
        _factory.Auth0.SetPermissions(userId, TypingPlay, MemoryPlay, MatchesCreate);
        var trasRetiro = await GetAccessAsync(client);
        Assert.DoesNotContain("games.trivia.play", trasRetiro.Permissions);

        // 3. Vuelve a sincronizarse: /sync no escribe permisos, así que no puede devolvérselo.
        var reSync = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Permiso Retirado" });
        Assert.Equal(HttpStatusCode.OK, reSync.StatusCode);

        var final = await GetAccessAsync(client);
        Assert.DoesNotContain("games.trivia.play", final.Permissions);
        Assert.Equal(new[] { "memory", "typing" }, final.Games.Select(g => g.GameType).OrderBy(t => t));
    }

    // --- Dos cuentas ---

    [MySqlFact]
    public async Task DosCuentas_ConservanIdentidadesYAccesosSeparados()
    {
        _factory.Auth0.SetPermissions("auth0|cuenta-a", TypingPlay);
        _factory.Auth0.SetPermissions("auth0|cuenta-b", TriviaPlay, MemoryPlay);
        var clientA = _factory.CreateClientFor("auth0|cuenta-a");
        var clientB = _factory.CreateClientFor("auth0|cuenta-b");

        await clientA.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Cuenta A" });
        await clientB.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Cuenta B" });

        var profileA = await clientA.GetFromJsonAsync<ProfileResponse>("/api/profiles/me");
        var profileB = await clientB.GetFromJsonAsync<ProfileResponse>("/api/profiles/me");
        Assert.Equal(("auth0|cuenta-a", "Cuenta A"), (profileA?.Id, profileA?.DisplayName));
        Assert.Equal(("auth0|cuenta-b", "Cuenta B"), (profileB?.Id, profileB?.DisplayName));

        Assert.Equal(new[] { "typing" }, (await GetAccessAsync(clientA)).Games.Select(g => g.GameType));
        Assert.Equal(new[] { "memory", "trivia" }, (await GetAccessAsync(clientB)).Games.Select(g => g.GameType).OrderBy(t => t));
    }

    // --- Endpoints anteriores con la misma fuente de Auth0 ---

    [MySqlFact]
    public async Task EndpointsAnteriores_LeenLosPermisosDeAuth0()
    {
        const string userId = "auth0|endpoints-anteriores";
        _factory.Auth0.SetPermissions(userId, TypingPlay, MemoryPlay, MatchesCreate);
        var client = _factory.CreateClientFor(userId);
        await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Anteriores" });

        var permissions = await client.GetFromJsonAsync<PermissionResponse>("/api/profiles/me/permissions");
        var games = await client.GetFromJsonAsync<List<GameResponse>>("/api/profiles/me/games");
        var access = await GetAccessAsync(client);

        Assert.Equal(access.Permissions.OrderBy(p => p), permissions!.Permissions.OrderBy(p => p));
        Assert.Equal(access.Games.Select(g => g.GameType).OrderBy(t => t), games!.Select(g => g.GameType).OrderBy(t => t));
        Assert.Equal(new[] { "memory", "typing" }, games!.Select(g => g.GameType).OrderBy(t => t));
    }

    [MySqlTheory]
    [InlineData("/api/profiles/me/permissions")]
    [InlineData("/api/profiles/me/games")]
    public async Task EndpointsAnteriores_FalloDeAuth0_Responden503(string path)
    {
        const string userId = "auth0|anteriores-caido";
        var client = _factory.CreateClientFor(userId);
        await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Caído" });
        _factory.Auth0.Unavailable = true;

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // --- CORS con el Shell ---

    [MySqlFact]
    public async Task Preflight_PermiteShellYRechazaOrigenAjeno()
    {
        var client = _factory.CreateClient();

        var permitido = new HttpRequestMessage(HttpMethod.Options, "/api/profiles/me/access");
        permitido.Headers.Add("Origin", TestHost.ShellOrigin);
        permitido.Headers.Add("Access-Control-Request-Method", "GET");
        var respuestaPermitida = await client.SendAsync(permitido);

        Assert.True(respuestaPermitida.Headers.TryGetValues("Access-Control-Allow-Origin", out var origenes));
        Assert.Equal(TestHost.ShellOrigin, Assert.Single(origenes));

        var ajeno = new HttpRequestMessage(HttpMethod.Options, "/api/profiles/me/access");
        ajeno.Headers.Add("Origin", "https://untrusted.example");
        ajeno.Headers.Add("Access-Control-Request-Method", "GET");
        var respuestaAjena = await client.SendAsync(ajeno);

        Assert.False(respuestaAjena.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
