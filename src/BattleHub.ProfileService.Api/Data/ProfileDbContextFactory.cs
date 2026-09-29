using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BattleHub.ProfileService.Api.Data;

/// <summary>
/// Permite que <c>dotnet ef</c> construya el contexto sin levantar la aplicación.
/// Lee la cadena de conexión de user-secrets, appsettings o variables de entorno; si no hay
/// ninguna, usa una de marcador, porque generar una migración no necesita conectarse a MySQL.
/// </summary>
public class ProfileDbContextFactory : IDesignTimeDbContextFactory<ProfileDbContext>
{
    private const string DesignTimeConnectionString =
        "server=localhost;port=3306;database=battlehub_profile;user=root;password=;";

    public ProfileDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<ProfileDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ProfileDb") ?? DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<ProfileDbContext>()
            .UseMySQL(connectionString)
            .Options;

        return new ProfileDbContext(options);
    }
}
