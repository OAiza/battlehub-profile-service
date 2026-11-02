using Microsoft.AspNetCore.Hosting;

namespace BattleHub.ProfileService.IntegrationTests.Infrastructure;

/// <summary>
/// Configuración compartida de la aplicación de prueba.
///
/// R-09: la API no tiene valores por defecto de Auth0 ni de CORS, así que las pruebas deben
/// aportarlos igual que lo haría el entorno real (las variables del <c>.env</c>).
/// </summary>
public static class TestHost
{
    public const string Auth0Domain = "tenant-de-prueba.auth0.com";
    public const string Auth0Issuer = $"https://{Auth0Domain}/";
    public const string Auth0Audience = "https://api.battlehub.local/profile";
    public const string TypingApi = "https://api.battlehub.local/typing";
    public const string TriviaApi = "https://api.battlehub.local/trivia";
    public const string MemoryApi = "https://api.battlehub.local/memory";
    public const string ShellOrigin = "http://localhost:4000";

    /// <summary>
    /// Cadena de conexión para las pruebas que nunca abren la base (salud, CORS, arranque y
    /// tokens rechazados): <c>Program.cs</c> exige que esté definida, pero no se conecta al
    /// arrancar. Las pruebas que guardan datos usan el MySQL de <see cref="MySqlDatabaseFixture"/>.
    /// </summary>
    public const string ConnectionStringSinUso = "Server=localhost;Database=battlehub_profiles_sin_uso;";

    public static Dictionary<string, string?> Settings(string? connectionString = null)
    {
        return new Dictionary<string, string?>
        {
            ["DatabaseProvider"] = "MySQL",
            ["ConnectionStrings:DefaultConnection"] = connectionString ?? ConnectionStringSinUso,
            ["Auth0:Domain"] = Auth0Domain,
            ["Auth0:Audience"] = Auth0Audience,
            ["Auth0:PermissionApis:Typing"] = TypingApi,
            ["Auth0:PermissionApis:Trivia"] = TriviaApi,
            ["Auth0:PermissionApis:Memory"] = MemoryApi,
            ["Auth0:Management:ClientId"] = FakeAuth0.ClientId,
            ["Auth0:Management:ClientSecret"] = FakeAuth0.ClientSecret,
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
}
