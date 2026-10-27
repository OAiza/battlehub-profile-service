using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BattleHub.ProfileService.IntegrationTests;

/// <summary>
/// Valida los tokens con la configuración real de JwtBearer del servicio (emisor, audiencia,
/// firma y vencimiento). Las claves se generan en la prueba: no se contacta a Auth0.
/// </summary>
[Trait("Category", "Integration")]
public class JwtValidationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string Domain = "battlehub-test.us.auth0.com";
    private const string Issuer = $"https://{Domain}/";
    private const string Audience = "https://api.battlehub.local/profile";

    private readonly RSA _tenantRsa = RSA.Create(2048);
    private readonly RSA _otherRsa = RSA.Create(2048);
    private readonly RsaSecurityKey _tenantKey;
    private readonly WebApplicationFactory<Program> _factory;

    public JwtValidationTests(WebApplicationFactory<Program> factory)
    {
        _tenantKey = new RsaSecurityKey(_tenantRsa) { KeyId = "clave-del-tenant" };
        var databaseName = "JwtValidationDb_" + Guid.NewGuid().ToString("N");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth0:Domain", Domain);
            builder.UseSetting("Auth0:Audience", Audience);
            builder.UseSetting("InMemoryDatabaseName", databaseName);

            builder.ConfigureTestServices(services =>
            {
                // Se reemplaza solo el origen de las claves (el JWKS que publicaría Auth0).
                // Emisor, audiencia y vencimiento se validan con la configuración de Program.cs.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                    configuration.SigningKeys.Add(_tenantKey);
                    options.Configuration = configuration;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        });
    }

    [Fact]
    public async Task TokenValido_PermiteSincronizarYConsultarElPerfil()
    {
        var client = CreateClient(CreateToken(subject: "auth0|jwt-valido"));

        var sync = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Token Válido" });
        var me = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Created, sync.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task TokenConOtroEmisor_Responde401()
    {
        var client = CreateClient(CreateToken(issuer: "https://otro-tenant.us.auth0.com/"));

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenConOtraAudiencia_Responde401()
    {
        var client = CreateClient(CreateToken(audience: "https://api.otro-servicio.local"));

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenFirmadoConOtraClave_Responde401()
    {
        var foreignKey = new RsaSecurityKey(_otherRsa) { KeyId = "clave-del-tenant" };
        var client = CreateClient(CreateToken(signingKey: foreignKey));

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenVencido_Responde401()
    {
        var expired = CreateToken(expires: DateTime.UtcNow.AddMinutes(-10), notBefore: DateTime.UtcNow.AddMinutes(-70));
        var client = CreateClient(expired);

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenAlterado_Responde401()
    {
        var token = CreateToken();
        var tampered = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");
        var client = CreateClient(tampered);

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/profiles/me")]
    [InlineData("/api/profiles/me/permissions")]
    [InlineData("/api/profiles/me/games")]
    public async Task TokenValidoSinSub_ConsultasResponden401(string path)
    {
        var client = CreateClient(CreateToken(subject: null));

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenValidoSinSub_SyncResponde401ConProblemDetails()
    {
        var client = CreateClient(CreateToken(subject: null));

        var response = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Sin sub" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TokenM2MDeAuth0_SyncResponde403()
    {
        var token = CreateToken(
            subject: "AbCdEf123456@clients",
            extraClaims: new Dictionary<string, object> { ["gty"] = "client-credentials" });
        var client = CreateClient(token);

        var response = await client.PostAsJsonAsync("/api/profiles/sync", new { displayName = "Servicio" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PermisosDeUsuarioSinPerfil_Responde404()
    {
        var client = CreateClient(CreateToken(subject: "auth0|jwt-sin-perfil"));

        var response = await client.GetAsync("/api/profiles/me/permissions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string CreateToken(
        string? subject = "auth0|jwt-usuario",
        string issuer = Issuer,
        string audience = Audience,
        SecurityKey? signingKey = null,
        DateTime? expires = null,
        DateTime? notBefore = null,
        IDictionary<string, object>? extraClaims = null)
    {
        var claims = new List<Claim>();
        if (subject != null)
        {
            claims.Add(new Claim("sub", subject));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(signingKey ?? _tenantKey, SecurityAlgorithms.RsaSha256),
            Claims = extraClaims
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public void Dispose()
    {
        _tenantRsa.Dispose();
        _otherRsa.Dispose();
    }
}
