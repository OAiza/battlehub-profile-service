using System.Net;
using System.Net.Http.Headers;
using BattleHub.ProfileService.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.ProfileService.IntegrationTests;

[ApiController]
[Route("api/test-protected")]
[Authorize]
public class TestProtectedController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "autorizado" });
    }
}

[Trait("Category", "Integration")]
public sealed class SecurityEndpointTests : IDisposable
{
    // Ninguna de estas pruebas abre la base: no hace falta MySQL ni una base simulada.
    private readonly ProfileApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private WebApplicationFactory<Program> ConControladorProtegido()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(TestProtectedController).Assembly);
            });
        });
    }

    [Fact]
    public async Task EndpointProtegido_SinToken_Responde401Unauthorized()
    {
        var client = ConControladorProtegido().CreateClient();

        var response = await client.GetAsync("/api/test-protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EndpointProtegido_ConTokenValido_Responde200()
    {
        var client = ConControladorProtegido().CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken("auth0|usuario-autorizado"));

        var response = await client.GetAsync("/api/test-protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_SinToken_SigueRespondiendo200Ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
