using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Models;
using BattleHub.ProfileService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using ProfileServiceImpl = BattleHub.ProfileService.Api.Services.ProfileService;

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

    private static Mock<IAuth0ManagementClient> CreateAuth0Mock(IReadOnlyList<string>? permissions = null)
    {
        var mock = new Mock<IAuth0ManagementClient>();
        var perms = permissions ?? new[] { "matches.create", "games.typing.play", "games.trivia.play", "games.memory.play" };
        mock.Setup(c => c.GetUserEffectivePermissionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(perms);
        return mock;
    }

    [Fact]
    public async Task SyncProfileAsync_NuevoUsuario_GuardaElSubEnSuColumnaYUsaClavePrimariaInterna()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(SyncProfileAsync_NuevoUsuario_GuardaElSubEnSuColumnaYUsaClavePrimariaInterna));
        var auth0Mock = CreateAuth0Mock();
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        // Act
        var (profile, created) = await service.SyncProfileAsync("auth0|12345", "Francisco", "francisco@ejemplo.com");

        // Assert
        Assert.True(created);
        // El contrato con el Shell no cambia: la API sigue devolviendo el sub de Auth0.
        Assert.Equal("auth0|12345", profile.Id);
        Assert.Equal("Francisco", profile.DisplayName);
        Assert.Equal("francisco@ejemplo.com", profile.Email);

        var savedProfile = await context.Profiles.FirstOrDefaultAsync(p => p.Auth0UserId == "auth0|12345");
        Assert.NotNull(savedProfile);
        // R-13: la clave primaria es entera e interna, y el sub vive en su propia columna.
        Assert.True(savedProfile.Id > 0);
        Assert.Equal("auth0|12345", savedProfile.Auth0UserId);
    }

    [Fact]
    public async Task SyncProfileAsync_UsuarioExistente_ActualizaSinDuplicar()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(SyncProfileAsync_UsuarioExistente_ActualizaSinDuplicar));
        var auth0Mock = CreateAuth0Mock();
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        await service.SyncProfileAsync("auth0|999", "NombreAntiguo", "viejo@mail.com");

        // Act
        var (updatedProfile, created) = await service.SyncProfileAsync("auth0|999", "NombreNuevo", "nuevo@mail.com");

        // Assert
        Assert.False(created);
        Assert.Equal("NombreNuevo", updatedProfile.DisplayName);
        Assert.Equal("nuevo@mail.com", updatedProfile.Email);

        var count = await context.Profiles.CountAsync(p => p.Auth0UserId == "auth0|999");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetProfileAsync_UsuarioExistente_DevuelvePerfil()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetProfileAsync_UsuarioExistente_DevuelvePerfil));
        var auth0Mock = CreateAuth0Mock();
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

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
        var auth0Mock = CreateAuth0Mock();
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        // Act
        var result = await service.GetProfileAsync("auth0|inexistente");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPermissionsAsync_ConsultaAuth0ManagementClient()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetPermissionsAsync_ConsultaAuth0ManagementClient));
        var auth0Mock = CreateAuth0Mock(new[] { "matches.create", "games.typing.play" });
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        await service.SyncProfileAsync("auth0|user1", "User 1", null);

        // Act
        var permissions = await service.GetPermissionsAsync("auth0|user1");

        // Assert
        Assert.NotNull(permissions);
        Assert.Equal(2, permissions.Permissions.Count);
        Assert.Contains("matches.create", permissions.Permissions);
        Assert.Contains("games.typing.play", permissions.Permissions);
    }

    [Fact]
    public async Task GetEnabledGamesForUserAsync_DevuelveJuegosSegunPermisosDeAuth0()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetEnabledGamesForUserAsync_DevuelveJuegosSegunPermisosDeAuth0));
        var auth0Mock = CreateAuth0Mock(new[] { "games.typing.play" });
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        await service.SyncProfileAsync("auth0|solo_typing", "Typing Only", null);

        // Act
        var games = await service.GetEnabledGamesForUserAsync("auth0|solo_typing");

        // Assert
        Assert.Single(games);
        Assert.Equal("typing", games[0].GameType);
    }

    [Fact]
    public async Task GetUserAccessAsync_UsuarioSinPermisos_DevuelveListasVacias()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetUserAccessAsync_UsuarioSinPermisos_DevuelveListasVacias));
        var auth0Mock = CreateAuth0Mock(Array.Empty<string>());
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        // Act
        var access = await service.GetUserAccessAsync("auth0|zero-user");

        // Assert
        Assert.NotNull(access);
        Assert.Equal("auth0|zero-user", access.UserId);
        Assert.Empty(access.Permissions);
        Assert.Empty(access.Games);
    }

    [Fact]
    public async Task GetUserAccessAsync_UsuarioConUnJuego_DevuelveSoloEseJuego()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetUserAccessAsync_UsuarioConUnJuego_DevuelveSoloEseJuego));
        var auth0Mock = CreateAuth0Mock(new[] { "matches.create", "games.typing.play" });
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        // Act
        var access = await service.GetUserAccessAsync("auth0|user-typing");

        // Assert
        Assert.NotNull(access);
        Assert.Equal("auth0|user-typing", access.UserId);
        Assert.Equal(2, access.Permissions.Count);
        Assert.Single(access.Games);
        Assert.Equal("typing", access.Games[0].GameType);
        Assert.Equal("Typing Battle", access.Games[0].Name);
    }

    [Fact]
    public async Task GetUserAccessAsync_UsuarioConTresJuegos_DevuelveLosTresJuegos()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetUserAccessAsync_UsuarioConTresJuegos_DevuelveLosTresJuegos));
        var auth0Mock = CreateAuth0Mock(new[] { "matches.create", "games.typing.play", "games.trivia.play", "games.memory.play" });
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        // Act
        var access = await service.GetUserAccessAsync("auth0|user-all-games");

        // Assert
        Assert.NotNull(access);
        Assert.Equal("auth0|user-all-games", access.UserId);
        Assert.Equal(4, access.Permissions.Count);
        Assert.Equal(3, access.Games.Count);
        Assert.Contains(access.Games, g => g.GameType == "typing");
        Assert.Contains(access.Games, g => g.GameType == "trivia");
        Assert.Contains(access.Games, g => g.GameType == "memory");
    }

    [Fact]
    public async Task GetUserAccessAsync_FalloDeAuth0_PropagaExcepcion()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetUserAccessAsync_FalloDeAuth0_PropagaExcepcion));
        var auth0Mock = new Mock<IAuth0ManagementClient>();
        auth0Mock.Setup(c => c.GetUserEffectivePermissionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Auth0ManagementException("Auth0 no responde"));

        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Auth0ManagementException>(() => service.GetUserAccessAsync("auth0|failed-user"));
    }

    [Fact]
    public async Task SyncProfileAsync_PerfilCreadoEnParalelo_DevuelveElExistenteSinDuplicar()
    {
        // Arrange: otra llamada a /sync inserta el mismo perfil justo antes de que esta guarde.
        const string userId = "auth0|sync-simultaneo";
        var databaseName = nameof(SyncProfileAsync_PerfilCreadoEnParalelo_DevuelveElExistenteSinDuplicar);
        var root = new InMemoryDatabaseRoot();

        var auth0Mock = CreateAuth0Mock();

        var interceptor = new PerfilCreadoEnParaleloInterceptor(() => new ProfileDbContext(CreateOptions(databaseName, root)), userId);
        using var context = new ProfileDbContext(CreateOptions(databaseName, root, interceptor));
        var service = new ProfileServiceImpl(context, auth0Mock.Object);

        // Act
        var (profile, created) = await service.SyncProfileAsync(userId, "Francisco", null);

        // Assert
        Assert.False(created);
        Assert.Equal(userId, profile.Id);
        Assert.Equal("Francisco", profile.DisplayName);

        using var verifyContext = new ProfileDbContext(CreateOptions(databaseName, root));
        Assert.Equal(1, await verifyContext.Profiles.CountAsync(p => p.Auth0UserId == userId));
    }

    private static DbContextOptions<ProfileDbContext> CreateOptions(
        string databaseName,
        InMemoryDatabaseRoot root,
        IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ProfileDbContext>()
            .UseInMemoryDatabase(databaseName, root);

        if (interceptor != null)
        {
            builder.AddInterceptors(interceptor);
        }

        return builder.Options;
    }

    private sealed class PerfilCreadoEnParaleloInterceptor(Func<ProfileDbContext> createOtherContext, string userId)
        : SaveChangesInterceptor
    {
        private bool _alreadyInserted;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_alreadyInserted)
            {
                _alreadyInserted = true;
                using var otherContext = createOtherContext();
                otherContext.Profiles.Add(new Profile
                {
                    Auth0UserId = userId,
                    DisplayName = "Creado en paralelo",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                });
                await otherContext.SaveChangesAsync(cancellationToken);

                throw new DbUpdateException($"Duplicate entry '{userId}' for key 'profiles.PRIMARY'");
            }

            return result;
        }
    }
}
