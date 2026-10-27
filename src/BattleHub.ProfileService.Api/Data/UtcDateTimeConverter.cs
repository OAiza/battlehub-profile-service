using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BattleHub.ProfileService.Api.Data;

/// <summary>
/// MySQL no guarda la zona horaria en las columnas DATETIME: al leerlas, las fechas llegan
/// como <see cref="DateTimeKind.Unspecified"/> y se serializarían sin el sufijo <c>Z</c>.
/// Este conversor guarda siempre en UTC y marca como UTC lo que se lee.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
