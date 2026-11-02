using System.Net;
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
public class SecurityEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityEndpointTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "SecurityDb_" + Guid.NewGuid().ToString("N");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            TestHost.ApplySettings(builder);
            builder.ConfigureServices(services => TestHost.UseInMemoryDatabase(services, dbName));
        });
    }

    [Fact]
    public async Task EndpointProtegido_SinToken_Responde401Unauthorized()
    {
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(TestProtectedController).Assembly);
                });
            })
            .CreateClient();

        var response = await client.GetAsync("/api/test-protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_SinToken_SigueRespondiendo200Ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
