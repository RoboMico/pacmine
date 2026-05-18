using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pacmine.Core;

/// <summary>
/// Custom JSON converter for <see cref="VersionIdentifier"/> that serializes
/// and deserializes it as a plain string (via <see cref="VersionIdentifier.RawString"/>).
/// </summary>
public class VersionIdentifierJsonConverter : JsonConverter<VersionIdentifier>
{
    /// <summary>
    /// Reads a <see cref="VersionIdentifier"/> from a JSON string value.
    /// </summary>
    public override VersionIdentifier? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return str != null ? new VersionIdentifier(str) : null;
    }

    /// <summary>
    /// Writes a <see cref="VersionIdentifier"/> as a JSON string value.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, VersionIdentifier value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.RawString);
    }

    /// <summary>
    /// Reads a <see cref="VersionIdentifier"/> from a JSON object property name (dictionary key).
    /// </summary>
    public override VersionIdentifier ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return new VersionIdentifier(str!);
    }

    /// <summary>
    /// Writes a <see cref="VersionIdentifier"/> as a JSON object property name (dictionary key).
    /// </summary>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, VersionIdentifier value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.RawString);
    }
}
