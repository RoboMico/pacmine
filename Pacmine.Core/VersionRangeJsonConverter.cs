using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pacmine.Core;

/// <summary>
/// Custom JSON converter for <see cref="VersionRange"/> that serializes
/// and deserializes it as a plain string (via <see cref="VersionRange.ToString()"/>).
/// </summary>
public class VersionRangeJsonConverter : JsonConverter<VersionRange>
{
    /// <summary>
    /// Reads a <see cref="VersionRange"/> from a JSON string value.
    /// </summary>
    public override VersionRange? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return str != null ? new VersionRange(str) : null;
    }

    /// <summary>
    /// Writes a <see cref="VersionRange"/> as a JSON string value.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, VersionRange value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
