using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleHub.ProfileService.IntegrationTests;

[Trait("Category", "Integration")]
public class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_RespondeOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RutaInexistente_RespondeProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ruta-que-no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
