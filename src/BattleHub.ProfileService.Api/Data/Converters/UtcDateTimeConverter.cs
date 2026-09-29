using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BattleHub.ProfileService.Api.Data.Converters;

/// <summary>
/// MySQL no almacena la zona horaria en <c>datetime</c>. Este conversor garantiza la convención
/// del proyecto: todo se guarda en UTC y todo se lee con <see cref="DateTimeKind.Utc"/>,
/// para que se serialice en ISO-8601 con sufijo <c>Z</c>.
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
