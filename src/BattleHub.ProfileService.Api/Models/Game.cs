namespace BattleHub.ProfileService.Api.Models;

public class Game
{
    /// <summary>
    /// Clave primaria interna (R-13). No se expone en la API.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Identificador del juego en el contrato con el Shell ("typing", "trivia", "memory").
    /// Es la clave natural del catálogo y se mantiene única.
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Código del permiso de Auth0 que habilita este juego. Se cruza con los permisos que
    /// devuelve la Management API para decidir si el usuario tiene acceso.
    /// </summary>
    public string RequiredPermission { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
