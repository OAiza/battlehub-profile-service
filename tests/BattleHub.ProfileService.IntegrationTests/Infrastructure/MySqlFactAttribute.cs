namespace BattleHub.ProfileService.IntegrationTests.Infrastructure;

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
            Skip = MySqlEnvironment.SkipReason;
        }
    }
}

/// <summary>
/// Igual que <see cref="MySqlFactAttribute"/>, para pruebas con varios casos.
/// </summary>
public sealed class MySqlTheoryAttribute : TheoryAttribute
{
    public MySqlTheoryAttribute()
    {
        if (!MySqlEnvironment.ShouldRun)
        {
            Skip = MySqlEnvironment.SkipReason;
        }
    }
}

public static class MySqlEnvironment
{
    public const string SkipReason = "Requiere Docker para levantar MySQL con Testcontainers. En el CI se ejecuta siempre.";

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
