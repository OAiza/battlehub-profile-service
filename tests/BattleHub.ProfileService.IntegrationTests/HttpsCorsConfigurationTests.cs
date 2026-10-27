using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BattleHub.ProfileService.IntegrationTests;

/// <summary>
/// Configuración que afecta a las llamadas del Shell desde el navegador: redirección a HTTPS,
/// orígenes CORS (ADR-006) y audiencia de Auth0 (ADR-007).
/// </summary>
[Trait("Category", "Integration")]
public class HttpsCorsConfigurationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpsCorsConfigurationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RedireccionHttps_ActivaPorDefecto_RedirigeLasLlamadasHttp()
    {
        var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("https_port", "443"));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https", response.Headers.Location?.Scheme);
    }

    [Fact]
    public async Task RedireccionHttps_Desactivada_RespondeSinRedirigir()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("https_port", "443");
            builder.UseSetting("Https:Redirect", "false");
        });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Preflight_ConRedireccionDesactivada_NoSeRedirige()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("https_port", "443");
            builder.UseSetting("Https:Redirect", "false");
        });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(CreatePreflight("http://localhost:4000"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_ShellLocalYPublicado_PermiteAmbosYRechazaOtroOrigen()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Cors:Origins:0", "http://localhost:4000");
            builder.UseSetting("Cors:Origins:1", "https://shell.battlehub.example");
            // Un origen opcional sin valor en el .env no debe romper la política.
            builder.UseSetting("Cors:Origins:2", "");
        });
        var client = factory.CreateClient();

        var local = await client.SendAsync(CreatePreflight("http://localhost:4000"));
        var published = await client.SendAsync(CreatePreflight("https://shell.battlehub.example"));
        var foreign = await client.SendAsync(CreatePreflight("https://untrusted.example"));

        Assert.Equal("http://localhost:4000", Assert.Single(local.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("https://shell.battlehub.example", Assert.Single(published.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(foreign.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Audiencia_PorDefecto_EsLaDelAdr007()
    {
        var options = _factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("https://api.battlehub.local/profile", options.Audience);
    }

    private static HttpRequestMessage CreatePreflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/profiles/sync");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }
}
