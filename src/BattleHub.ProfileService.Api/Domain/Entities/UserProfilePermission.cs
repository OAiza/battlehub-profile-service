namespace BattleHub.ProfileService.Api.Domain.Entities;

/// <summary>Permiso concedido a un perfil (tabla de unión entre <see cref="UserProfile"/> y <see cref="Permission"/>).</summary>
public class UserProfilePermission
{
    public Guid UserProfileId { get; set; }

    public required string PermissionCode { get; set; }

    public DateTime GrantedAtUtc { get; set; }

    public UserProfile UserProfile { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}
