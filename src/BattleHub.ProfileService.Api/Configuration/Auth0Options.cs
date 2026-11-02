namespace BattleHub.ProfileService.Api.Configuration;

public class Auth0Options
{
    public const string SectionName = "Auth0";

    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Identifier de la API de Profile en Auth0. Es la audiencia que valida el bearer del Shell
    /// y también la API donde vive el permiso <c>matches.create</c>.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    public Auth0ManagementOptions Management { get; set; } = new();

    public Auth0PermissionApiOptions PermissionApis { get; set; } = new();
}

public class Auth0ManagementOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}

/// <summary>
/// Identifiers de las APIs de Auth0 donde viven los permisos que este servicio acepta (R-09:
/// son valores propios de cada entorno, así que no van escritos en el código). El código de
/// cada permiso sí vive en el código y en el catálogo de juegos, porque es parte del contrato.
/// </summary>
public class Auth0PermissionApiOptions
{
    public string Typing { get; set; } = string.Empty;
    public string Trivia { get; set; } = string.Empty;
    public string Memory { get; set; } = string.Empty;

    /// <summary>
    /// API de Matchmaking. Opcional: el tenant asigna <c>matches.create</c> en la API de
    /// Profile, pero se acepta también desde aquí si el Equipo 2 llegara a moverlo.
    /// </summary>
    public string Matchmaking { get; set; } = string.Empty;
}
