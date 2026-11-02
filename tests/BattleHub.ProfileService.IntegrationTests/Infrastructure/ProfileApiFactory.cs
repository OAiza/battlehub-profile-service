using System.Net.Http.Headers;
using BattleHub.ProfileService.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.ProfileService.IntegrationTests.Infrastructure;

/// <summary>
/// La API completa, tal como arranca en el contenedor, con dos cambios y nada más (R-18):
/// <list type="bullet">
/// <item>Las claves públicas del JWT son las de prueba (<see cref="TestJwt"/>), en vez del JWKS
/// del tenant. La validación del token sigue siendo la de <c>Program.cs</c>.</item>
/// <item>Las llamadas a la Management API las responde <see cref="FakeAuth0"/>.</item>
/// </list>
/// La base es el MySQL de <see cref="MySqlDatabaseFixture"/> cuando la prueba guarda datos.
/// </summary>
public sealed class ProfileApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings;

    public ProfileApiFactory(string? connectionString = null)
    {
        _settings = TestHost.Settings(connectionString);
    }

    public FakeAuth0 Auth0 { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Un entorno distinto de Development: no carga user-secrets y exige configuración explícita.
        builder.UseEnvironment("Testing");
        TestHost.ApplySettings(builder, _settings);

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, TestJwt.UseTestSigningKey);

            // Mismo cliente tipado que registra Program.cs; solo cambia el handler que hace la
            // petición HTTP.
            services.AddHttpClient<IAuth0ManagementClient, Auth0ManagementClient>()
                .ConfigurePrimaryHttpMessageHandler(() => Auth0.CreateHandler());
        });
    }

    /// <summary>
    /// Cliente con el token de un usuario de Auth0, como el que envía el Shell.
    /// </summary>
    public HttpClient CreateClientFor(string sub, IDictionary<string, object>? extraClaims = null)
    {
        return CreateClientWithToken(TestJwt.CreateToken(sub, extraClaims: extraClaims));
    }

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
