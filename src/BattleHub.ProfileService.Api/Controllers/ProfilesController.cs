using System.Security.Claims;
using BattleHub.ProfileService.Api.Dtos;
using BattleHub.ProfileService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BattleHub.ProfileService.Api.Controllers;

[ApiController]
[Route("api/profiles")]
[Authorize]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfilesController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Sync([FromBody] SyncProfileRequest? request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "No autorizado",
                Detail = "No se encontró el identificador del usuario (sub/nameidentifier) en el token.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var displayName = request?.DisplayName
            ?? User.FindFirst("name")?.Value
            ?? User.FindFirst("nickname")?.Value;

        var email = request?.Email
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value;

        var (profile, created) = await _profileService.SyncProfileAsync(userId, displayName, email, cancellationToken);

        if (created)
        {
            return StatusCode(StatusCodes.Status201Created, profile);
        }

        return Ok(profile);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await _profileService.GetProfileAsync(userId, cancellationToken);
        if (profile == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Perfil no encontrado",
                Detail = $"No existe un perfil local para el usuario '{userId}'. Realice POST /api/profiles/sync primero.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(profile);
    }

    [HttpGet("me/permissions")]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyPermissions(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var permissions = await _profileService.GetPermissionsAsync(userId, cancellationToken);
        if (permissions == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Perfil no encontrado",
                Detail = $"No existe un perfil local para el usuario '{userId}'.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(permissions);
    }

    [HttpGet("me/games")]
    [ProducesResponseType(typeof(IReadOnlyList<GameResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyGames(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var games = await _profileService.GetEnabledGamesForUserAsync(userId, cancellationToken);
        return Ok(games);
    }

    private string? GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
    }
}

public record SyncProfileRequest(string? DisplayName = null, string? Email = null);

