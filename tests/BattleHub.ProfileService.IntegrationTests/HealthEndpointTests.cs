using System.Net;
using BattleHub.ProfileService.IntegrationTests.Infrastructure;

namespace BattleHub.ProfileService.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class HealthEndpointTests : IDisposable
{
    // Ninguna de estas pruebas abre la base: no hace falta MySQL ni una base simulada.
    private readonly ProfileApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

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
