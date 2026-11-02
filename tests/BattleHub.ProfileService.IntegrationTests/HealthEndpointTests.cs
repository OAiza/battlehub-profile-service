using System.Net;
using BattleHub.ProfileService.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleHub.ProfileService.IntegrationTests;

[Trait("Category", "Integration")]
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "HealthDb_" + Guid.NewGuid().ToString("N");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            TestHost.ApplySettings(builder);
            builder.ConfigureServices(services => TestHost.UseInMemoryDatabase(services, dbName));
        });
    }

    [Fact]
    public async Task Health_RespondeOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RutaInexistente_RespondeProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/ruta-que-no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType
        );
    }
}
