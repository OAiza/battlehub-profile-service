using BattleHub.ProfileService.Api.Models;

namespace BattleHub.ProfileService.Api.Data;

/// <summary>
/// Acceso a datos del servicio. Separa a <see cref="Services.ProfileService"/> de EF Core para
/// que sus pruebas unitarias usen un mock en vez de una base de datos (R-17). La implementación
/// con MySQL se prueba en las pruebas de integración.
/// </summary>
public interface IProfileRepository
{
    /// <summary>
    /// Busca el perfil por el <c>sub</c> de Auth0. El perfil queda en seguimiento, así que los
    /// cambios que se le hagan se guardan con <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<Profile?> GetByAuth0UserIdAsync(string auth0UserId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string auth0UserId, CancellationToken cancellationToken = default);

    void Add(Profile profile);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta los cambios pendientes, por ejemplo un alta que otra petición ya hizo primero.
    /// </summary>
    void DiscardChanges();

    Task<IReadOnlyList<Game>> GetEnabledGamesAsync(CancellationToken cancellationToken = default);
}
