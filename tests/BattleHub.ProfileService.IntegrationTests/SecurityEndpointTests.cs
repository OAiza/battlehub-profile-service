using System.Net;
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
    public IActionResult Get() => Ok(new { message = "autorizado" });
}

[Trait("Category", "Integration")]
public class SecurityEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task EndpointProtegido_SinToken_Responde401Unauthorized()
    {
        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(SecurityEndpointTests).Assembly);
            });
        }).CreateClient();

        var response = await client.GetAsync("/api/test-protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_SinToken_SigueRespondiendo200Ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

