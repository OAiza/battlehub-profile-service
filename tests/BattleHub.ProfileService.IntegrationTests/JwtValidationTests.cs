using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using BattleHub.ProfileService.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace BattleHub.ProfileService.IntegrationTests;

/// <summary>
/// Valida los tokens con la configuración real de JwtBearer del servicio (emisor, audiencia,
/// firma y vencimiento). Las claves se generan en la prueba: no se contacta a Auth0.
/// Las pruebas de tokens rechazados no llegan a la base; las que guardan un perfil usan MySQL.
/// </summary>
[Trait("Category", "Integration")]
[Collection(MySqlCollection.Name)]
public sealed class JwtValidationTests : IDisposable
{
    private readonly RSA _otherRsa = RSA.Create(2048);
    private readonly ProfileApiFactory _factory;

    public JwtValidationTests(MySqlDatabaseFixture database)
    {
        _factory = new ProfileApiFactory(database.ConnectionString);
    }

    [MySqlFact]
    public async Task TokenValido_PermiteSincronizarYConsultarElPerfil()
    {
        var client = _factory.CreateClientWithToken(TestJwt.CreateToken(subject: "auth0|jwt-valido"));

        var sync = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Token Válido" });
        var me = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Created, sync.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task SinToken_Responde401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/profiles/me/access");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenConOtroEmisor_Responde401()
    {
        var client = _factory.CreateClientWithToken(
            TestJwt.CreateToken("auth0|jwt-usuario", issuer: "https://otro-tenant.us.auth0.com/"));

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenConOtraAudiencia_Responde401()
    {
        var client = _factory.CreateClientWithToken(
            TestJwt.CreateToken("auth0|jwt-usuario", audience: "https://api.otro-servicio.local"));

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenFirmadoConOtraClave_Responde401()
    {
        var foreignKey = new RsaSecurityKey(_otherRsa) { KeyId = TestJwt.TenantKey.KeyId };
        var client = _factory.CreateClientWithToken(TestJwt.CreateToken("auth0|jwt-usuario", signingKey: foreignKey));

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenVencido_Responde401()
    {
        var expired = TestJwt.CreateToken(
            "auth0|jwt-usuario",
            expires: DateTime.UtcNow.AddMinutes(-10),
            notBefore: DateTime.UtcNow.AddMinutes(-70));
        var client = _factory.CreateClientWithToken(expired);

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenAlterado_Responde401()
    {
        var token = TestJwt.CreateToken("auth0|jwt-usuario");
        var tampered = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");
        var client = _factory.CreateClientWithToken(tampered);

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/profiles/me")]
    [InlineData("/api/profiles/me/permissions")]
    [InlineData("/api/profiles/me/games")]
    [InlineData("/api/profiles/me/access")]
    public async Task TokenValidoSinSub_ConsultasResponden401(string path)
    {
        var client = _factory.CreateClientWithToken(TestJwt.CreateToken(subject: null));

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenValidoSinSub_SyncResponde401ConProblemDetails()
    {
        var client = _factory.CreateClientWithToken(TestJwt.CreateToken(subject: null));

        var response = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Sin sub" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TokenM2MDeAuth0_SyncResponde403()
    {
        var token = TestJwt.CreateToken(
            subject: "AbCdEf123456@clients",
            extraClaims: new Dictionary<string, object> { ["gty"] = "client-credentials" });
        var client = _factory.CreateClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Servicio" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [MySqlFact]
    public async Task PermisosDeUsuarioSinPerfil_Responde404()
    {
        var client = _factory.CreateClientFor("auth0|jwt-sin-perfil");

        var response = await client.GetAsync("/api/profiles/me/permissions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _otherRsa.Dispose();
    }
}
