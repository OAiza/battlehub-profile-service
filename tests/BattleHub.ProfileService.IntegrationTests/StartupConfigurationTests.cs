using BattleHub.ProfileService.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleHub.ProfileService.IntegrationTests;

/// <summary>
/// R-09: la API no trae ningún valor de configuración escrito en el código, así que arrancar sin
/// una de estas claves debe fallar con un mensaje que diga cuál falta, en vez de levantar un
/// servicio que rechaza todos los tokens o deja un juego invisible.
/// </summary>
[Trait("Category", "Integration")]
public class StartupConfigurationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StartupConfigurationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Arranca con la configuración completa salvo las claves indicadas, para que cada prueba
    /// examine una sola ausencia.
    /// </summary>
    private WebApplicationFactory<Program> ArrancarSin(params string[] clavesOmitidas)
    {
        var settings = TestHost.Settings();
        foreach (var clave in clavesOmitidas)
        {
            settings.Remove(clave);
        }

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            TestHost.ApplySettings(builder, settings);
        });
    }

    private WebApplicationFactory<Program> ArrancarCon(string clave, string valor)
    {
        var settings = TestHost.Settings();
        settings[clave] = valor;

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            TestHost.ApplySettings(builder, settings);
        });
    }

    [Fact]
    public void Arranque_SinDatabaseProvider_Falla()
    {
        var exception = Record.Exception(() => ArrancarSin("DatabaseProvider").CreateClient());

        AssertStartupFailed(exception, "DatabaseProvider");
    }

    [Fact]
    public void Arranque_DatabaseProviderDesconocido_Falla()
    {
        var exception = Record.Exception(() => ArrancarCon("DatabaseProvider", "SqlServer").CreateClient());

        AssertStartupFailed(exception, "DatabaseProvider");
    }

    [Fact]
    public void Arranque_ConBaseEnMemoria_Falla()
    {
        // R-12: la base en memoria solo se permite en pruebas, que registran su propio proveedor.
        // La API no la acepta ni siquiera en Development.
        var exception = Record.Exception(() => ArrancarCon("DatabaseProvider", "InMemory").CreateClient());

        AssertStartupFailed(exception, "DatabaseProvider");
    }

    [Fact]
    public void Arranque_SinCadenaDeConexion_Falla()
    {
        var exception = Record.Exception(() => ArrancarSin("ConnectionStrings:DefaultConnection").CreateClient());

        AssertStartupFailed(exception, "DefaultConnection");
    }

    [Fact]
    public void Arranque_SinDominioDeAuth0_Falla()
    {
        var exception = Record.Exception(() => ArrancarSin("Auth0:Domain").CreateClient());

        AssertStartupFailed(exception, "Auth0:Domain");
    }

    [Fact]
    public void Arranque_SinAudienciaDeAuth0_Falla()
    {
        var exception = Record.Exception(() => ArrancarSin("Auth0:Audience").CreateClient());

        AssertStartupFailed(exception, "Auth0:Audience");
    }

    [Fact]
    public void Arranque_SinLaApiDeUnJuego_Falla()
    {
        // Sin el Identifier de la API del juego, su permiso no se reconocería y ese juego
        // desaparecería para todos los usuarios sin ningún error visible.
        var exception = Record.Exception(() => ArrancarSin("Auth0:PermissionApis:Trivia").CreateClient());

        AssertStartupFailed(exception, "Auth0:PermissionApis:Trivia");
    }

    [Fact]
    public void Arranque_SinOrigenesCors_Falla()
    {
        var exception = Record.Exception(() => ArrancarSin("Cors:Origins:0").CreateClient());

        AssertStartupFailed(exception, "Cors:Origins");
    }

    private static void AssertStartupFailed(Exception? exception, string expectedText)
    {
        Assert.NotNull(exception);

        // El host puede envolver la excepción original; se busca en toda la cadena.
        var messages = new List<string>();
        for (var current = exception; current != null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        Assert.Contains(messages, message => message.Contains(expectedText));
    }
}
