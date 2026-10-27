namespace BattleHub.ProfileService.IntegrationTests.MySql;

/// <summary>
/// Prueba que necesita un MySQL real levantado con Docker (Testcontainers).
/// En el CI siempre se ejecuta: si falta Docker, la prueba falla en vez de saltarse.
/// En una máquina local sin Docker se salta con un aviso.
/// </summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (!MySqlEnvironment.ShouldRun)
        {
            Skip = "Requiere Docker para levantar MySQL con Testcontainers. En el CI se ejecuta siempre.";
        }
    }
}

public static class MySqlEnvironment
{
    public static bool IsCi =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldRun => IsCi || IsDockerAvailable();

    private static bool IsDockerAvailable()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true;
        }

        return OperatingSystem.IsWindows()
            ? File.Exists(@"\\.\pipe\docker_engine")
            : File.Exists("/var/run/docker.sock");
    }
}
