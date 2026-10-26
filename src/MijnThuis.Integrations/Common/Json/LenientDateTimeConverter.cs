using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MijnThuis.Integrations.Common.Json;

/// <summary>
/// Parses date/time values that are not strictly ISO 8601 (e.g. "2024-05-01 13:15:00").
/// </summary>
internal sealed class LenientDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? default
            : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
}
