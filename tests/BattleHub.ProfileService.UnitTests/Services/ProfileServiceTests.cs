using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Models;
using BattleHub.ProfileService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using ProfileServiceImpl = BattleHub.ProfileService.Api.Services.ProfileService;

namespace BattleHub.ProfileService.UnitTests.Services;

/// <summary>
/// R-17: el servicio se prueba con sus dependencias simuladas (el acceso a datos y el cliente de
/// Auth0). No se usa ninguna base de datos, ni siquiera en memoria.
/// </summary>
[Trait("Category", "Unit")]
public class ProfileServiceTests
{
    private static readonly IReadOnlyList<Game> Catalogo = new[]
    {
        new Game { Id = 1, GameType = "typing", Name = "Typing Battle", RequiredPermission = "games.typing.play", Enabled = true },
        new Game { Id = 2, GameType = "trivia", Name = "Trivia Battle", RequiredPermission = "games.trivia.play", Enabled = true },
        new Game { Id = 3, GameType = "memory", Name = "Memory Match", RequiredPermission = "games.memory.play", Enabled = true }
    };

    private readonly Mock<IProfileRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IAuth0ManagementClient> _auth0 = new(MockBehavior.Strict);
    private readonly ProfileServiceImpl _service;

    public ProfileServiceTests()
    {
        _service = new ProfileServiceImpl(_repository.Object, _auth0.Object);
    }

    private void DadoQueElPerfil(string userId, Profile? profile)
    {
        _repository.Setup(r => r.GetByAuth0UserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _repository.Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile != null);
    }

    private void DadoQueAuth0Devuelve(string userId, params string[] permissions)
    {
        _auth0.Setup(c => c.GetUserEffectivePermissionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);
    }

    private void DadoElCatalogo(IReadOnlyList<Game>? games = null)
    {
        _repository.Setup(r => r.GetEnabledGamesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(games ?? Catalogo);
    }

    private static Profile PerfilExistente(string userId, string displayName = "Nombre", string? email = null)
    {
        var antes = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new Profile { Id = 7, Auth0UserId = userId, DisplayName = displayName, Email = email, CreatedAt = antes, LastLoginAt = antes };
    }

    // --- SyncProfileAsync ---

    [Fact]
    public async Task SyncProfileAsync_NuevoUsuario_AgregaElPerfilYDevuelveElSubComoId()
    {
        DadoQueElPerfil("auth0|12345", null);
        Profile? agregado = null;
        _repository.Setup(r => r.Add(It.IsAny<Profile>())).Callback<Profile>(p => agregado = p);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (profile, created) = await _service.SyncProfileAsync("auth0|12345", "Francisco", "francisco@ejemplo.com");

        Assert.True(created);
        // El contrato con el Shell no cambia: la API devuelve el sub de Auth0, nunca el id interno.
        Assert.Equal("auth0|12345", profile.Id);
        Assert.Equal("Francisco", profile.DisplayName);
        Assert.Equal("francisco@ejemplo.com", profile.Email);
        Assert.NotNull(agregado);
        Assert.Equal("auth0|12345", agregado.Auth0UserId);
        Assert.Equal(DateTimeKind.Utc, agregado.CreatedAt.Kind);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncProfileAsync_NuevoUsuarioSinNombre_UsaElSubComoNombre()
    {
        DadoQueElPerfil("auth0|sin-nombre", null);
        _repository.Setup(r => r.Add(It.IsAny<Profile>()));
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (profile, _) = await _service.SyncProfileAsync("auth0|sin-nombre", "  ", null);

        Assert.Equal("auth0|sin-nombre", profile.DisplayName);
    }

    [Fact]
    public async Task SyncProfileAsync_UsuarioExistente_ActualizaSinAgregarOtro()
    {
        var existente = PerfilExistente("auth0|999", "NombreAntiguo", "viejo@mail.com");
        DadoQueElPerfil("auth0|999", existente);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (profile, created) = await _service.SyncProfileAsync("auth0|999", "NombreNuevo", "nuevo@mail.com");

        Assert.False(created);
        Assert.Equal("NombreNuevo", profile.DisplayName);
        Assert.Equal("nuevo@mail.com", profile.Email);
        Assert.True(existente.LastLoginAt > existente.CreatedAt);
        _repository.Verify(r => r.Add(It.IsAny<Profile>()), Times.Never);
    }

    [Fact]
    public async Task SyncProfileAsync_UsuarioExistenteSinDatosNuevos_ConservaNombreYCorreo()
    {
        var existente = PerfilExistente("auth0|conserva", "Nombre Guardado", "guardado@mail.com");
        DadoQueElPerfil("auth0|conserva", existente);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (profile, _) = await _service.SyncProfileAsync("auth0|conserva", null, null);

        Assert.Equal("Nombre Guardado", profile.DisplayName);
        Assert.Equal("guardado@mail.com", profile.Email);
    }

    [Fact]
    public async Task SyncProfileAsync_NoConsultaNiConcedePermisosEnAuth0()
    {
        // /sync solo crea o actualiza el perfil (R-47): con el mock estricto, cualquier llamada a
        // Auth0 haría fallar la prueba.
        DadoQueElPerfil("auth0|sin-permisos", null);
        _repository.Setup(r => r.Add(It.IsAny<Profile>()));
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _service.SyncProfileAsync("auth0|sin-permisos", "Jugador", null);

        _auth0.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SyncProfileAsync_SinUserId_LanzaArgumentException(string userId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SyncProfileAsync(userId, "Nombre", null));
    }

    [Fact]
    public async Task SyncProfileAsync_PerfilCreadoEnParalelo_DescartaElAltaYDevuelveElExistente()
    {
        // Otra llamada a /sync inserta el mismo perfil justo antes de que esta guarde.
        const string userId = "auth0|sync-simultaneo";
        var creadoPorLaOtraLlamada = PerfilExistente(userId, "Creado en paralelo");
        _repository.SetupSequence(r => r.GetByAuth0UserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null)
            .ReturnsAsync(creadoPorLaOtraLlamada);
        _repository.Setup(r => r.Add(It.IsAny<Profile>()));
        _repository.SetupSequence(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException($"Duplicate entry '{userId}' for key 'UX_profiles_auth0_user_id'"))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.DiscardChanges());
        _repository.Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var (profile, created) = await _service.SyncProfileAsync(userId, "Francisco", null);

        Assert.False(created);
        Assert.Equal(userId, profile.Id);
        Assert.Equal("Francisco", profile.DisplayName);
        _repository.Verify(r => r.DiscardChanges(), Times.Once);
        _repository.Verify(r => r.Add(It.IsAny<Profile>()), Times.Once);
    }

    [Fact]
    public async Task SyncProfileAsync_ErrorAlGuardarSinPerfilExistente_PropagaLaExcepcion()
    {
        const string userId = "auth0|error-real";
        DadoQueElPerfil(userId, null);
        _repository.Setup(r => r.Add(It.IsAny<Profile>()));
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Data too long for column 'display_name'"));
        _repository.Setup(r => r.DiscardChanges());

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.SyncProfileAsync(userId, "Nombre", null));
    }

    [Fact]
    public async Task SyncProfileAsync_ErrorAlActualizar_NoReintentaYPropagaLaExcepcion()
    {
        const string userId = "auth0|error-actualizar";
        DadoQueElPerfil(userId, PerfilExistente(userId));
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Fallo al actualizar"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.SyncProfileAsync(userId, "Nombre", null));
        _repository.Verify(r => r.DiscardChanges(), Times.Never);
    }

    // --- GetProfileAsync ---

    [Fact]
    public async Task GetProfileAsync_UsuarioExistente_DevuelvePerfil()
    {
        DadoQueElPerfil("auth0|abc", PerfilExistente("auth0|abc", "Usuario ABC", "abc@mail.com"));

        var result = await _service.GetProfileAsync("auth0|abc");

        Assert.NotNull(result);
        Assert.Equal("auth0|abc", result.Id);
        Assert.Equal("Usuario ABC", result.DisplayName);
        Assert.Equal("abc@mail.com", result.Email);
    }

    [Fact]
    public async Task GetProfileAsync_UsuarioInexistente_DevuelveNull()
    {
        DadoQueElPerfil("auth0|inexistente", null);

        Assert.Null(await _service.GetProfileAsync("auth0|inexistente"));
    }

    // --- GetPermissionsAsync ---

    [Fact]
    public async Task GetPermissionsAsync_UsuarioConPerfil_DevuelveLosPermisosDeAuth0()
    {
        DadoQueElPerfil("auth0|user1", PerfilExistente("auth0|user1"));
        DadoQueAuth0Devuelve("auth0|user1", "matches.create", "games.typing.play");

        var permissions = await _service.GetPermissionsAsync("auth0|user1");

        Assert.NotNull(permissions);
        Assert.Equal(new[] { "matches.create", "games.typing.play" }, permissions.Permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_UsuarioSinPerfil_DevuelveNullSinConsultarAuth0()
    {
        DadoQueElPerfil("auth0|sin-perfil", null);

        Assert.Null(await _service.GetPermissionsAsync("auth0|sin-perfil"));
        _auth0.VerifyNoOtherCalls();
    }

    // --- GetEnabledGamesForUserAsync ---

    [Fact]
    public async Task GetEnabledGamesForUserAsync_DevuelveSoloLosJuegosQueHabilitanSusPermisos()
    {
        DadoQueElPerfil("auth0|solo-typing", PerfilExistente("auth0|solo-typing"));
        DadoQueAuth0Devuelve("auth0|solo-typing", "games.typing.play");
        DadoElCatalogo();

        var games = await _service.GetEnabledGamesForUserAsync("auth0|solo-typing");

        var game = Assert.Single(games);
        Assert.Equal("typing", game.GameType);
        Assert.Equal("Typing Battle", game.Name);
        Assert.Equal("games.typing.play", game.RequiredPermission);
    }

    [Fact]
    public async Task GetEnabledGamesForUserAsync_UsuarioSinPerfil_DevuelveListaVaciaSinConsultarAuth0()
    {
        DadoQueElPerfil("auth0|sin-perfil", null);

        Assert.Empty(await _service.GetEnabledGamesForUserAsync("auth0|sin-perfil"));
        _auth0.VerifyNoOtherCalls();
    }

    // --- GetUserAccessAsync (contrato con el Shell, R-47 / R-48 / R-49) ---

    [Fact]
    public async Task GetUserAccessAsync_UsuarioSinPermisos_DevuelveListasVacias()
    {
        DadoQueAuth0Devuelve("auth0|usuario-1");
        DadoElCatalogo();

        var access = await _service.GetUserAccessAsync("auth0|usuario-1");

        Assert.Equal("auth0|usuario-1", access.UserId);
        Assert.Empty(access.Permissions);
        Assert.Empty(access.Games);
    }

    [Fact]
    public async Task GetUserAccessAsync_UsuarioConUnJuego_DevuelveSoloEseJuego()
    {
        DadoQueAuth0Devuelve("auth0|usuario-2", "matches.create", "games.typing.play");
        DadoElCatalogo();

        var access = await _service.GetUserAccessAsync("auth0|usuario-2");

        Assert.Equal("auth0|usuario-2", access.UserId);
        Assert.Equal(new[] { "matches.create", "games.typing.play" }, access.Permissions);
        Assert.Equal("typing", Assert.Single(access.Games).GameType);
    }

    [Fact]
    public async Task GetUserAccessAsync_UsuarioConTresJuegos_DevuelveLosTresJuegos()
    {
        DadoQueAuth0Devuelve("auth0|usuario-3", "matches.create", "games.typing.play", "games.trivia.play", "games.memory.play");
        DadoElCatalogo();

        var access = await _service.GetUserAccessAsync("auth0|usuario-3");

        Assert.Equal(4, access.Permissions.Count);
        Assert.Equal(new[] { "memory", "trivia", "typing" }, access.Games.Select(g => g.GameType).OrderBy(t => t));
    }

    [Fact]
    public async Task GetUserAccessAsync_JuegoDeshabilitadoEnElCatalogo_NoSeDevuelve()
    {
        // El repositorio solo entrega juegos habilitados: si trivia se deshabilita, desaparece
        // aunque el usuario conserve el permiso en Auth0.
        DadoQueAuth0Devuelve("auth0|usuario-3", "games.typing.play", "games.trivia.play");
        DadoElCatalogo(Catalogo.Where(g => g.GameType != "trivia").ToList());

        var access = await _service.GetUserAccessAsync("auth0|usuario-3");

        Assert.Equal("typing", Assert.Single(access.Games).GameType);
    }

    [Fact]
    public async Task GetUserAccessAsync_NoNecesitaPerfilLocal()
    {
        // /me/access responde aunque el usuario aún no haya llamado a /sync: el contrato con el
        // Shell solo depende de Auth0 y del catálogo.
        DadoQueAuth0Devuelve("auth0|sin-sync", "games.memory.play");
        DadoElCatalogo();

        var access = await _service.GetUserAccessAsync("auth0|sin-sync");

        Assert.Equal("memory", Assert.Single(access.Games).GameType);
        _repository.Verify(r => r.GetByAuth0UserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUserAccessAsync_FalloDeAuth0_PropagaLaExcepcionSinConsultarElCatalogo()
    {
        _auth0.Setup(c => c.GetUserEffectivePermissionsAsync("auth0|caido", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Auth0ManagementException("Auth0 no responde"));

        await Assert.ThrowsAsync<Auth0ManagementException>(() => _service.GetUserAccessAsync("auth0|caido"));
        _repository.Verify(r => r.GetEnabledGamesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUserAccessAsync_SinUserId_LanzaArgumentException(string userId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetUserAccessAsync(userId));
    }
}
