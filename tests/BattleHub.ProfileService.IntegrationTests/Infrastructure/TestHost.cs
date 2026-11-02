using BattleHub.ProfileService.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BattleHub.ProfileService.IntegrationTests.Infrastructure;

/// <summary>
/// Configuración compartida de la aplicación de prueba.
///
/// R-09: la API no tiene valores por defecto de Auth0 ni de CORS, así que las pruebas deben
/// aportarlos igual que lo haría el entorno real.
///
/// R-12: la base en memoria ya no es una opción de la API. Las pruebas que la necesitan la
/// registran ellas mismas, reemplazando el <see cref="ProfileDbContext"/> que configuró
/// <c>Program.cs</c> contra MySQL.
/// </summary>
public static class TestHost
{
    public const string Auth0Domain = "tenant-de-prueba.auth0.com";
    public const string Auth0Audience = "https://api.battlehub.local/profile";
    public const string ShellOrigin = "http://localhost:4000";

    /// <summary>
    /// Cadena de conexión ficticia: con la base en memoria nunca se abre, pero
    /// <c>Program.cs</c> exige que esté definida.
    /// </summary>
    private const string ConnectionStringDePrueba = "Server=localhost;Database=battlehub_profiles_pruebas;";

    public static Dictionary<string, string?> Settings(
        string? auth0Domain = null,
        string? auth0Audience = null)
    {
        return new Dictionary<string, string?>
        {
            ["DatabaseProvider"] = "MySQL",
            ["ConnectionStrings:DefaultConnection"] = ConnectionStringDePrueba,
            ["Auth0:Domain"] = auth0Domain ?? Auth0Domain,
            ["Auth0:Audience"] = auth0Audience ?? Auth0Audience,
            ["Auth0:PermissionApis:Typing"] = "https://api.battlehub.local/typing",
            ["Auth0:PermissionApis:Trivia"] = "https://api.battlehub.local/trivia",
            ["Auth0:PermissionApis:Memory"] = "https://api.battlehub.local/memory",
            ["Cors:Origins:0"] = ShellOrigin
        };
    }

    /// <summary>
    /// Aplica la configuración con <see cref="HostingAbstractionsWebHostBuilderExtensions.UseSetting"/>.
    ///
    /// Tiene que ser por esta vía y no con <c>ConfigureAppConfiguration</c>: <c>Program.cs</c> lee
    /// la configuración mientras construye la aplicación, antes de <c>Build()</c>, y las fuentes
    /// que agrega <c>ConfigureAppConfiguration</c> todavía no están disponibles en ese momento.
    /// </summary>
    public static void ApplySettings(IWebHostBuilder builder, IDictionary<string, string?> settings)
    {
        foreach (var (clave, valor) in settings)
        {
            builder.UseSetting(clave, valor);
        }
    }

    public static void ApplySettings(
        IWebHostBuilder builder,
        string? auth0Domain = null,
        string? auth0Audience = null)
    {
        ApplySettings(builder, Settings(auth0Domain, auth0Audience));
    }

    /// <summary>
    /// Sustituye el proveedor de MySQL por la base en memoria de las pruebas.
    /// </summary>
    public static void UseInMemoryDatabase(IServiceCollection services, string databaseName)
    {
        services.RemoveAll<DbContextOptions<ProfileDbContext>>();
        services.RemoveAll<DbContextOptions>();

        // AddDbContext acumula configuraciones de opciones, así que la de MySQL que registró
        // Program.cs seguiría aplicándose junto con la de la base en memoria, y EF rechaza tener
        // dos proveedores a la vez. Hay que retirarla antes de registrar la de las pruebas.
        var configuracionesDeOpciones = services
            .Where(descriptor => descriptor.ServiceType.IsGenericType
                && descriptor.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal))
            .ToList();

        foreach (var descriptor in configuracionesDeOpciones)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<ProfileDbContext>(options => options.UseInMemoryDatabase(databaseName));
    }

    /// <summary>
    /// Crea el esquema de la base en memoria y carga el catálogo de juegos del seed. La API ya no
    /// lo hace al arrancar: en MySQL el catálogo llega con las migraciones.
    /// </summary>
    public static void EnsureDatabaseCreated(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ProfileDbContext>().Database.EnsureCreated();
    }
}
