namespace BattleHub.ProfileService.Api.Domain.Entities;

/// <summary>
/// Perfil local del usuario, creado a partir del usuario autenticado en Auth0.
/// </summary>
public class UserProfile
{
    /// <summary>Identificador local del perfil.</summary>
    public Guid Id { get; set; }

    /// <summary>Identificador del usuario en Auth0 (claim <c>sub</c>, ejemplo: <c>auth0|abc123</c>).</summary>
    public required string Auth0UserId { get; set; }

    public required string Email { get; set; }

    /// <summary>Nombre visible del usuario, el que se envía al Shell y a los microfrontends.</summary>
    public required string DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Último <c>POST /api/profiles/sync</c> registrado para este perfil.</summary>
    public DateTime? LastSyncedAtUtc { get; set; }

    public ICollection<UserProfilePermission> Permissions { get; set; } = [];
}
