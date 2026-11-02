namespace BattleHub.ProfileService.Api.Models;

public class Profile
{
    /// <summary>
    /// Clave primaria interna (R-13). No se expone en la API.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Claim "sub" del token de Auth0. Es la identidad con la que el Shell y los demás
    /// servicios conocen al usuario, y la que devuelve la API como identificador.
    /// </summary>
    public string Auth0UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
}
