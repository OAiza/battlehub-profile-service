using System.Net;
using System.Text.Json;
using BattleHub.ProfileService.Api.Configuration;
using BattleHub.ProfileService.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BattleHub.ProfileService.UnitTests;

[Trait("Category", "Unit")]
public class Auth0ManagementClientTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task GetUserEffectivePermissionsAsync_CredencialesFaltantes_LanzaAuth0ManagementException()
    {
        // Arrange
        var options = Options.Create(new Auth0Options
        {
            Domain = "test.auth0.com",
            Management = new Auth0ManagementOptions
            {
                ClientId = "", // Vacío
                ClientSecret = ""
            }
        });

        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpClient(handler);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var auth0Client = new Auth0ManagementClient(client, memoryCache, options, NullLogger<Auth0ManagementClient>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<Auth0ManagementException>(() => auth0Client.GetUserEffectivePermissionsAsync("auth0|123"));
    }

    [Fact]
    public async Task GetUserEffectivePermissionsAsync_FiltraCombinacionesPermitidasYDescartaAjenas()
    {
        // Arrange
        var options = Options.Create(new Auth0Options
        {
            Domain = "test.auth0.com",
            Management = new Auth0ManagementOptions
            {
                ClientId = "client-123",
                ClientSecret = "secret-xyz",
                Audience = "https://test.auth0.com/api/v2/"
            }
        });

        int tokenCalls = 0;
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/oauth/token")
            {
                tokenCalls++;
                var tokenContent = JsonSerializer.Serialize(new
                {
                    access_token = "fake-m2m-token",
                    expires_in = 3600,
                    token_type = "Bearer"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(tokenContent, System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (req.RequestUri.AbsolutePath.Contains("/permissions"))
            {
                var perms = new[]
                {
                    // Permitidos
                    new { permission_name = "games.typing.play", resource_server_identifier = "https://api.battlehub.local/typing" },
                    new { permission_name = "matches.create", resource_server_identifier = "https://api.battlehub.local/profile" },
                    // Rechazados (API incorrecta)
                    new { permission_name = "games.typing.play", resource_server_identifier = "https://untrusted.com/typing" },
                    // Rechazados (Código ajeno)
                    new { permission_name = "admin.all", resource_server_identifier = "https://api.battlehub.local/typing" }
                };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(perms), System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var auth0Client = new Auth0ManagementClient(client, memoryCache, options, NullLogger<Auth0ManagementClient>.Instance);

        // Act
        var result1 = await auth0Client.GetUserEffectivePermissionsAsync("auth0|user-test");
        var result2 = await auth0Client.GetUserEffectivePermissionsAsync("auth0|user-test"); // Segunda llamada usa token en caché

        // Assert
        Assert.Equal(2, result1.Count);
        Assert.Contains("games.typing.play", result1);
        Assert.Contains("matches.create", result1);
        Assert.DoesNotContain("admin.all", result1);

        // El token M2M debe haberse solicitado solo 1 vez gracias a la caché
        Assert.Equal(1, tokenCalls);
    }

    [Fact]
    public async Task GetUserEffectivePermissionsAsync_Paginacion_RecorreTodasLasPaginas()
    {
        // Arrange
        var options = Options.Create(new Auth0Options
        {
            Domain = "test.auth0.com",
            Management = new Auth0ManagementOptions
            {
                ClientId = "client-123",
                ClientSecret = "secret-xyz"
            }
        });

        int pageCalls = 0;
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/oauth/token")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        access_token = "token-1",
                        expires_in = 3600
                    }), System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (req.RequestUri.AbsolutePath.Contains("/permissions"))
            {
                pageCalls++;
                if (req.RequestUri.Query.Contains("page=0"))
                {
                    // 100 items en la página 0 (fuerza pedir la página 1)
                    var list = Enumerable.Range(0, 100)
                        .Select(i => new { permission_name = "other.perm", resource_server_identifier = "https://api.battlehub.local/typing" })
                        .ToList();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(list), System.Text.Encoding.UTF8, "application/json")
                    };
                }

                // Página 1: entrega el permiso válido y tiene menos de 100 items (termina)
                var lastPage = new[]
                {
                    new { permission_name = "games.trivia.play", resource_server_identifier = "https://api.battlehub.local/trivia" }
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(lastPage), System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var auth0Client = new Auth0ManagementClient(client, memoryCache, options, NullLogger<Auth0ManagementClient>.Instance);

        // Act
        var result = await auth0Client.GetUserEffectivePermissionsAsync("auth0|paginated-user");

        // Assert
        Assert.Equal(2, pageCalls);
        Assert.Single(result);
        Assert.Equal("games.trivia.play", result[0]);
    }

    [Fact]
    public async Task GetUserEffectivePermissionsAsync_FalloDeAuth0_LanzaAuth0ManagementException()
    {
        // Arrange
        var options = Options.Create(new Auth0Options
        {
            Domain = "test.auth0.com",
            Management = new Auth0ManagementOptions
            {
                ClientId = "client-123",
                ClientSecret = "secret-xyz"
            }
        });

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/oauth/token")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        access_token = "token-1",
                        expires_in = 3600
                    }), System.Text.Encoding.UTF8, "application/json")
                };
            }

            // Auth0 devuelve 500 Internal Server Error
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var client = new HttpClient(handler);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var auth0Client = new Auth0ManagementClient(client, memoryCache, options, NullLogger<Auth0ManagementClient>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<Auth0ManagementException>(() => auth0Client.GetUserEffectivePermissionsAsync("auth0|err-user"));
    }
}
