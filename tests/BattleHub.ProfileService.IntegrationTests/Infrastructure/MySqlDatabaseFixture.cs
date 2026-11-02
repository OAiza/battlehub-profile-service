using BattleHub.ProfileService.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;

namespace BattleHub.ProfileService.IntegrationTests.Infrastructure;

/// <summary>
/// Levanta un MySQL efímero (la misma versión que docker-compose.yml) y le aplica las
/// migraciones del servicio, incluido el catálogo de juegos. R-18: las pruebas de integración
/// usan el motor real, no una base en memoria.
///
/// Un solo contenedor se comparte entre todas las clases de la colección <see cref="MySqlCollection"/>.
/// Por eso cada prueba usa su propio <c>sub</c> y no depende de cuántos perfiles hay en la tabla.
/// </summary>
public sealed class MySqlDatabaseFixture : IAsyncLifetime
{
    public const string MySqlImage = "mysql:8.4.9";

    private MySqlContainer? _container;

    /// <summary>
    /// Cadena de conexión del contenedor, o <c>null</c> si no se levantó porque no hay Docker.
    /// Las pruebas que la necesitan se marcan con <see cref="MySqlFactAttribute"/> y se saltan
    /// en ese caso.
    /// </summary>
    public string? ConnectionString => _container?.GetConnectionString();

    public string RequiredConnectionString =>
        ConnectionString ?? throw new InvalidOperationException("El contenedor de MySQL no se inició.");

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

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Contexto directo contra el contenedor, para preparar o comprobar datos sin pasar por la API.
    /// </summary>
    public ProfileDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProfileDbContext>()
            .UseMySQL(RequiredConnectionString)
            .Options;
        return new ProfileDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class MySqlCollection : ICollectionFixture<MySqlDatabaseFixture>
{
    public const string Name = "MySQL";
}
