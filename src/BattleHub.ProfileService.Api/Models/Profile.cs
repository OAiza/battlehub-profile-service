namespace BattleHub.ProfileService.Api.Models;

public class Profile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}

