using System.Security.Claims;
using BattleHub.ProfileService.Api.Controllers;
using BattleHub.ProfileService.Api.Dtos;
using BattleHub.ProfileService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BattleHub.ProfileService.UnitTests.Controllers;

/// <summary>
/// R-17: el controlador se prueba con un <see cref="IProfileService"/> simulado. Aquí se
/// comprueba cómo traduce cada caso a una respuesta HTTP; el servicio real y la validación del
/// token se cubren en las pruebas de integración.
/// </summary>
[Trait("Category", "Unit")]
public class ProfilesControllerTests
{
    private readonly Mock<IProfileService> _service = new(MockBehavior.Strict);

    private ProfilesController CrearControlador(params Claim[] claims)
    {
        var identity = claims.Length == 0 ? new ClaimsIdentity() : new ClaimsIdentity(claims, "Bearer");
        return new ProfilesController(_service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private ProfilesController CrearControladorPara(string sub, params Claim[] extra)
    {
        return CrearControlador(extra.Prepend(new Claim("sub", sub)).ToArray());
    }

    private static ProfileResponse Perfil(string id, string nombre = "Jugador") => new()
    {
        Id = id,
        DisplayName = nombre,
        CreatedAt = DateTime.UtcNow,
        LastLoginAt = DateTime.UtcNow
    };

    private static void AssertProblem(IActionResult result, int status)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(status, problem.Status);
    }

    // --- POST /api/profiles/sync ---

    [Fact]
    public async Task Sync_UsuarioNuevo_Responde201ConElPerfil()
    {
        _service.Setup(s => s.SyncProfileAsync("auth0|nuevo", "Nuevo", "nuevo@mail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Perfil("auth0|nuevo", "Nuevo"), true));

        var result = await CrearControladorPara("auth0|nuevo")
            .Sync(new SyncProfileRequest("Nuevo", "nuevo@mail.com"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        Assert.Equal("auth0|nuevo", Assert.IsType<ProfileResponse>(objectResult.Value).Id);
    }

    [Fact]
    public async Task Sync_UsuarioExistente_Responde200()
    {
        _service.Setup(s => s.SyncProfileAsync("auth0|existe", "Nombre", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Perfil("auth0|existe", "Nombre"), false));

        var result = await CrearControladorPara("auth0|existe").Sync(new SyncProfileRequest("Nombre"), CancellationToken.None);

        Assert.Equal("auth0|existe", Assert.IsType<ProfileResponse>(Assert.IsType<OkObjectResult>(result).Value).Id);
    }

    [Fact]
    public async Task Sync_SinCuerpo_TomaNombreYCorreoDelToken()
    {
        _service.Setup(s => s.SyncProfileAsync("auth0|claims", "Del Token", "token@mail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Perfil("auth0|claims", "Del Token"), true));

        await CrearControladorPara("auth0|claims", new Claim("name", "Del Token"), new Claim("email", "token@mail.com"))
            .Sync(null, CancellationToken.None);

        _service.VerifyAll();
    }

    [Fact]
    public async Task Sync_SinNombreEnElToken_UsaElNickname()
    {
        _service.Setup(s => s.SyncProfileAsync("auth0|nick", "apodo", "correo@mail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Perfil("auth0|nick", "apodo"), true));

        await CrearControladorPara("auth0|nick", new Claim("nickname", "apodo"), new Claim(ClaimTypes.Email, "correo@mail.com"))
            .Sync(new SyncProfileRequest(), CancellationToken.None);

        _service.VerifyAll();
    }

    [Fact]
    public async Task Sync_IdentidadPorNameIdentifier_UsaEseClaim()
    {
        _service.Setup(s => s.SyncProfileAsync("auth0|por-nameid", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Perfil("auth0|por-nameid"), true));

        await CrearControlador(new Claim(ClaimTypes.NameIdentifier, "auth0|por-nameid")).Sync(null, CancellationToken.None);

        _service.VerifyAll();
    }

    [Fact]
    public async Task Sync_TokenSinSub_Responde401ConProblemDetails()
    {
        var result = await CrearControlador(new Claim("name", "Sin sub")).Sync(null, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ProblemDetails>(unauthorized.Value).Status);
    }

    [Theory]
    [InlineData("AbCdEf@clients", null)]
    [InlineData("auth0|servicio", "client-credentials")]
    public async Task Sync_ClienteM2M_Responde403SinCrearPerfil(string sub, string? grantType)
    {
        var extra = grantType == null ? Array.Empty<Claim>() : new[] { new Claim("gty", grantType) };

        var result = await CrearControladorPara(sub, extra).Sync(null, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status403Forbidden);
        _service.VerifyNoOtherCalls();
    }

    // --- GET /api/profiles/me ---

    [Fact]
    public async Task GetMe_PerfilExistente_Responde200()
    {
        _service.Setup(s => s.GetProfileAsync("auth0|me", It.IsAny<CancellationToken>())).ReturnsAsync(Perfil("auth0|me"));

        var result = await CrearControladorPara("auth0|me").GetMe(CancellationToken.None);

        Assert.Equal("auth0|me", Assert.IsType<ProfileResponse>(Assert.IsType<OkObjectResult>(result).Value).Id);
    }

    [Fact]
    public async Task GetMe_SinPerfil_Responde404ConProblemDetails()
    {
        _service.Setup(s => s.GetProfileAsync("auth0|nadie", It.IsAny<CancellationToken>())).ReturnsAsync((ProfileResponse?)null);

        var result = await CrearControladorPara("auth0|nadie").GetMe(CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound);
    }

    // --- GET /api/profiles/me/permissions ---

    [Fact]
    public async Task GetMyPermissions_ConPerfil_Responde200()
    {
        _service.Setup(s => s.GetPermissionsAsync("auth0|p", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionResponse { Permissions = new List<string> { "games.typing.play" } });

        var result = await CrearControladorPara("auth0|p").GetMyPermissions(CancellationToken.None);

        var permissions = Assert.IsType<PermissionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("games.typing.play", Assert.Single(permissions.Permissions));
    }

    [Fact]
    public async Task GetMyPermissions_SinPerfil_Responde404()
    {
        _service.Setup(s => s.GetPermissionsAsync("auth0|p", It.IsAny<CancellationToken>())).ReturnsAsync((PermissionResponse?)null);

        var result = await CrearControladorPara("auth0|p").GetMyPermissions(CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetMyPermissions_FalloDeAuth0_Responde503()
    {
        _service.Setup(s => s.GetPermissionsAsync("auth0|p", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Auth0ManagementException("caído"));

        var result = await CrearControladorPara("auth0|p").GetMyPermissions(CancellationToken.None);

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
    }

    // --- GET /api/profiles/me/games ---

    [Fact]
    public async Task GetMyGames_Responde200ConLosJuegos()
    {
        _service.Setup(s => s.GetEnabledGamesForUserAsync("auth0|g", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new GameResponse { GameType = "typing", Name = "Typing Battle", RequiredPermission = "games.typing.play" } });

        var result = await CrearControladorPara("auth0|g").GetMyGames(CancellationToken.None);

        var games = Assert.IsAssignableFrom<IReadOnlyList<GameResponse>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("typing", Assert.Single(games).GameType);
    }

    [Fact]
    public async Task GetMyGames_FalloDeAuth0_Responde503()
    {
        _service.Setup(s => s.GetEnabledGamesForUserAsync("auth0|g", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Auth0ManagementException("caído"));

        var result = await CrearControladorPara("auth0|g").GetMyGames(CancellationToken.None);

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
    }

    // --- GET /api/profiles/me/access ---

    [Fact]
    public async Task GetMyAccess_Responde200ConLaIdentidadDelToken()
    {
        _service.Setup(s => s.GetUserAccessAsync("auth0|a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccessResponse { UserId = "auth0|a" });

        var result = await CrearControladorPara("auth0|a").GetMyAccess(CancellationToken.None);

        Assert.Equal("auth0|a", Assert.IsType<UserAccessResponse>(Assert.IsType<OkObjectResult>(result).Value).UserId);
    }

    [Fact]
    public async Task GetMyAccess_FalloDeAuth0_Responde503SinConcederNada()
    {
        _service.Setup(s => s.GetUserAccessAsync("auth0|a", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Auth0ManagementException("caído"));

        var result = await CrearControladorPara("auth0|a").GetMyAccess(CancellationToken.None);

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        Assert.IsNotType<UserAccessResponse>(((ObjectResult)result).Value);
    }

    // --- Casos comunes a las consultas ---

    public static TheoryData<string> Consultas => new() { "me", "permissions", "games", "access" };

    private static Task<IActionResult> Consultar(ProfilesController controller, string consulta) => consulta switch
    {
        "me" => controller.GetMe(CancellationToken.None),
        "permissions" => controller.GetMyPermissions(CancellationToken.None),
        "games" => controller.GetMyGames(CancellationToken.None),
        _ => controller.GetMyAccess(CancellationToken.None)
    };

    [Theory]
    [MemberData(nameof(Consultas))]
    public async Task Consulta_TokenSinSub_Responde401(string consulta)
    {
        var result = await Consultar(CrearControlador(new Claim("name", "Sin sub")), consulta);

        Assert.IsType<UnauthorizedResult>(result);
        _service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("permissions")]
    [InlineData("games")]
    [InlineData("access")]
    public async Task Consulta_ClienteM2M_Responde403(string consulta)
    {
        var result = await Consultar(CrearControladorPara("servicio@clients"), consulta);

        AssertProblem(result, StatusCodes.Status403Forbidden);
        _service.VerifyNoOtherCalls();
    }
}
