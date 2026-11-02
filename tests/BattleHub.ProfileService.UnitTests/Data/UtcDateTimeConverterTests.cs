using BattleHub.ProfileService.Api.Data;

namespace BattleHub.ProfileService.UnitTests.Data;

[Trait("Category", "Unit")]
public class UtcDateTimeConverterTests
{
    [Fact]
    public void FechaLeidaDeMySql_QuedaMarcadaComoUtc()
    {
        var converter = new UtcDateTimeConverter();
        var leidaDeMySql = new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Unspecified);

        var resultado = (DateTime)converter.ConvertFromProvider(leidaDeMySql)!;

        Assert.Equal(DateTimeKind.Utc, resultado.Kind);
        Assert.Equal(leidaDeMySql.Ticks, resultado.Ticks);
    }

    [Fact]
    public void FechaLocal_SeGuardaEnUtc()
    {
        var converter = new UtcDateTimeConverter();
        var local = new DateTime(2026, 9, 2, 14, 0, 0, DateTimeKind.Local);

        var guardada = (DateTime)converter.ConvertToProvider(local)!;

        Assert.Equal(local.ToUniversalTime(), guardada);
    }
}
