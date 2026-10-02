using ProfileServiceImpl = BattleHub.ProfileService.Api.Services.ProfileService;
using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BattleHub.ProfileService.UnitTests;

[Trait("Category", "Unit")]
public class ProfileServiceTests
{
    private ProfileDbContext CreateInMemoryContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ProfileDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;

        var context = new ProfileDbContext(options);

        // Aseguramos que la base en memoria tenga los permisos y juegos seed
        if (!context.Permissions.Any())
        {
            context.Permissions.AddRange(
                new Permission { Code = "matches.create", Description = "Crear salas de partidas" },
                new Permission { Code = "games.typing.play", Description = "Jugar a Typing Battle" },
                new Permission { Code = "games.trivia.play", Description = "Jugar a Trivia Battle" },
                new Permission { Code = "games.memory.play", Description = "Jugar a Memory Match" }
            );
        }

        if (!context.Games.Any())
        {
            context.Games.AddRange(
                new Game { GameType = "typing", Name = "Typing Battle", RequiredPermission = "games.typing.play", Enabled = true },
                new Game { GameType = "trivia", Name = "Trivia Battle", RequiredPermission = "games.trivia.play", Enabled = true },
                new Game { GameType = "memory", Name = "Memory Match", RequiredPermission = "games.memory.play", Enabled = true }
            );
        }

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task SyncProfileAsync_NuevoUsuario_CreaPerfilConPermisosPorDefecto()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(SyncProfileAsync_NuevoUsuario_CreaPerfilConPermisosPorDefecto));
        var service = new ProfileServiceImpl(context);

        // Act
        var (profile, created) = await service.SyncProfileAsync("auth0|12345", "Francisco", "francisco@ejemplo.com");

        // Assert
        Assert.True(created);
        Assert.Equal("auth0|12345", profile.Id);
        Assert.Equal("Francisco", profile.DisplayName);
        Assert.Equal("francisco@ejemplo.com", profile.Email);

        var savedProfile = await context.Profiles.Include(p => p.Permissions).FirstOrDefaultAsync(p => p.Id == "auth0|12345");
        Assert.NotNull(savedProfile);
        Assert.Equal(4, savedProfile.Permissions.Count);
    }

    [Fact]
    public async Task SyncProfileAsync_UsuarioExistente_ActualizaSinDuplicar()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(SyncProfileAsync_UsuarioExistente_ActualizaSinDuplicar));
        var service = new ProfileServiceImpl(context);

        await service.SyncProfileAsync("auth0|999", "NombreAntiguo", "viejo@mail.com");

        // Act
        var (updatedProfile, created) = await service.SyncProfileAsync("auth0|999", "NombreNuevo", "nuevo@mail.com");

        // Assert
        Assert.False(created);
        Assert.Equal("NombreNuevo", updatedProfile.DisplayName);
        Assert.Equal("nuevo@mail.com", updatedProfile.Email);

        var count = await context.Profiles.CountAsync(p => p.Id == "auth0|999");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetProfileAsync_UsuarioExistente_DevuelvePerfil()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetProfileAsync_UsuarioExistente_DevuelvePerfil));
        var service = new ProfileServiceImpl(context);

        await service.SyncProfileAsync("auth0|abc", "Usuario ABC", "abc@mail.com");

        // Act
        var result = await service.GetProfileAsync("auth0|abc");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("auth0|abc", result.Id);
        Assert.Equal("Usuario ABC", result.DisplayName);
    }

    [Fact]
    public async Task GetProfileAsync_UsuarioInexistente_DevuelveNull()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetProfileAsync_UsuarioInexistente_DevuelveNull));
        var service = new ProfileServiceImpl(context);

        // Act
        var result = await service.GetProfileAsync("auth0|inexistente");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPermissionsAsync_DevuelveCodigosDePermisos()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetPermissionsAsync_DevuelveCodigosDePermisos));
        var service = new ProfileServiceImpl(context);

        await service.SyncProfileAsync("auth0|user1", "User 1", null);

        // Act
        var permissions = await service.GetPermissionsAsync("auth0|user1");

        // Assert
        Assert.NotNull(permissions);
        Assert.Contains("matches.create", permissions.Permissions);
        Assert.Contains("games.typing.play", permissions.Permissions);
    }

    [Fact]
    public async Task GetEnabledGamesForUserAsync_DevuelveJuegosSegunPermisos()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetEnabledGamesForUserAsync_DevuelveJuegosSegunPermisos));
        var service = new ProfileServiceImpl(context);

        // Creamos perfil con solo un permiso de juego
        var typingPerm = await context.Permissions.FirstAsync(p => p.Code == "games.typing.play");
        var user = new Profile
        {
            Id = "auth0|solo_typing",
            DisplayName = "Typing Only",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            Permissions = new List<Permission> { typingPerm }
        };
        context.Profiles.Add(user);
        await context.SaveChangesAsync();

        // Act
        var games = await service.GetEnabledGamesForUserAsync("auth0|solo_typing");

        // Assert
        Assert.Single(games);
        Assert.Equal("typing", games[0].GameType);
    }
}
