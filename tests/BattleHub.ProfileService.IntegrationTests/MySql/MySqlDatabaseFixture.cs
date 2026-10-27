using BattleHub.ProfileService.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;

namespace BattleHub.ProfileService.IntegrationTests.MySql;

/// <summary>
/// Levanta un MySQL efímero (la misma versión que docker-compose.yml) y le aplica las
/// migraciones del servicio, incluidos los datos iniciales de permisos y juegos.
/// </summary>
public sealed class MySqlDatabaseFixture : IAsyncLifetime
{
    public const string MySqlImage = "mysql:8.4.9";

    private MySqlContainer? _container;

    public string ConnectionString =>
        _container?.GetConnectionString()
        ?? throw new InvalidOperationException("El contenedor de MySQL no se inició.");

    public async Task InitializeAsync()
    {
        if (!MySqlEnvironment.ShouldRun)
        {
            return;
        }

        _container = new MySqlBuilder(MySqlImage)
            .WithDatabase("battlehub_profiles")
            .WithUsername("battlehub")
            .WithPassword("battlehub_test")
            .Build();

        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<ProfileDbContext>()
            .UseMySQL(ConnectionString)
            .Options;
        await using var context = new ProfileDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
