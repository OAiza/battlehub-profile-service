using System.Net;
using System.Net.Http.Json;
using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Dtos;
using BattleHub.ProfileService.Api.Models;
using BattleHub.ProfileService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.ProfileService.IntegrationTests.Data;

/// <summary>
/// Acceso a datos contra MySQL real: las migraciones, la restricción única del <c>sub</c> y la
/// persistencia entre reinicios. Es la parte que las pruebas unitarias reemplazan con un mock.
/// </summary>
[Trait("Category", "Integration")]
[Collection(MySqlCollection.Name)]
public sealed class ProfileRepositoryTests
{
    private readonly MySqlDatabaseFixture _database;

    public ProfileRepositoryTests(MySqlDatabaseFixture database)
    {
        _database = database;
    }

    private static Profile NuevoPerfil(string userId, string displayName = "Jugador") => new()
    {
        Auth0UserId = userId,
        DisplayName = displayName,
        CreatedAt = DateTime.UtcNow,
        LastLoginAt = DateTime.UtcNow
    };

    [MySqlFact]
    public async Task Migraciones_CarganElCatalogoDeLosTresJuegos()
    {
        await using var context = _database.CreateContext();
        var repository = new ProfileRepository(context);

        var games = await repository.GetEnabledGamesAsync();

        Assert.Equal(
            new[]
            {
                ("memory", "Memory Match", "games.memory.play"),
                ("trivia", "Trivia Battle", "games.trivia.play"),
                ("typing", "Typing Battle", "games.typing.play")
            },
            games.Select(g => (g.GameType, g.Name, g.RequiredPermission)).OrderBy(g => g.GameType));
    }

    [MySqlFact]
    public async Task AddYSaveChanges_GuardaElPerfilConClaveEnteraYElSubEnSuColumna()
    {
        const string userId = "auth0|repo-alta";
        await using (var context = _database.CreateContext())
        {
            var repository = new ProfileRepository(context);
            repository.Add(NuevoPerfil(userId, "Alta"));
            await repository.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var saved = await new ProfileRepository(verify).GetByAuth0UserIdAsync(userId);
        Assert.NotNull(saved);
        Assert.True(saved.Id > 0);
        Assert.Equal("Alta", saved.DisplayName);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
        Assert.True(await new ProfileRepository(verify).ExistsAsync(userId));
        Assert.False(await new ProfileRepository(verify).ExistsAsync("auth0|repo-no-existe"));
    }

    [MySqlFact]
    public async Task MismoSubDosVeces_LaRestriccionUnicaLoRechaza()
    {
        // ProfileService confía en esta restricción para resolver dos /sync simultáneos.
        const string userId = "auth0|repo-duplicado";
        await using (var first = _database.CreateContext())
        {
            var repository = new ProfileRepository(first);
            repository.Add(NuevoPerfil(userId));
            await repository.SaveChangesAsync();
        }

        await using var second = _database.CreateContext();
        var secondRepository = new ProfileRepository(second);
        secondRepository.Add(NuevoPerfil(userId));

        await Assert.ThrowsAsync<DbUpdateException>(() => secondRepository.SaveChangesAsync());

        // Tras descartar el alta fallida, el mismo contexto sigue sirviendo.
        secondRepository.DiscardChanges();
        Assert.Empty(second.ChangeTracker.Entries());
        Assert.True(await secondRepository.ExistsAsync(userId));
    }

    [MySqlFact]
    public async Task Perfil_SeConservaAlReiniciarElServicio()
    {
        const string userId = "auth0|mysql-reinicio";

        using (var firstRun = new ProfileApiFactory(_database.RequiredConnectionString))
        {
            var sync = await firstRun.CreateClientFor(userId)
                .PostAsJsonAsync("/api/profiles/sync", new { displayName = "Antes del reinicio" });
            Assert.Equal(HttpStatusCode.Created, sync.StatusCode);
        }

        using var secondRun = new ProfileApiFactory(_database.RequiredConnectionString);
        var profile = await secondRun.CreateClientFor(userId).GetFromJsonAsync<ProfileResponse>("/api/profiles/me");

        Assert.Equal("Antes del reinicio", profile?.DisplayName);
    }
}
