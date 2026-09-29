namespace BattleHub.ProfileService.Api.Domain.Entities;

/// <summary>
/// Permiso de negocio expuesto por <c>GET /api/profiles/me/permissions</c>.
/// El código sigue el formato del contrato: <c>matches.create</c>, <c>games.trivia.play</c>.
/// </summary>
public class Permission
{
    public required string Code { get; set; }

    public required string Description { get; set; }

    /// <summary>Si es <c>true</c>, se asigna automáticamente a todo perfil recién creado.</summary>
    public bool GrantedByDefault { get; set; }

    public ICollection<UserProfilePermission> UserProfiles { get; set; } = [];
}
