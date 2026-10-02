namespace BattleHub.ProfileService.Api.Models;

public class Permission
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<Profile> Profiles { get; set; } = new List<Profile>();
}

