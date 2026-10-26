using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleHub.ProfileService.IntegrationTests;

[Trait("Category", "Integration")]
public class StartupConfigurationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StartupConfigurationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Arranque_FueraDeDevelopmentSinDatabaseProvider_Falla()
    {
        var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var exception = Record.Exception(() => factory.CreateClient());

        AssertStartupFailed(exception, "DatabaseProvider");
    }

    [Fact]
    public void Arranque_DatabaseProviderDesconocido_Falla()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("DatabaseProvider", "SqlServer");
        });

        var exception = Record.Exception(() => factory.CreateClient());

        AssertStartupFailed(exception, "DatabaseProvider");
    }

    [Fact]
    public void Arranque_MySqlSinCadenaDeConexion_Falla()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("DatabaseProvider", "MySQL");
        });

        var exception = Record.Exception(() => factory.CreateClient());

        AssertStartupFailed(exception, "DefaultConnection");
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
