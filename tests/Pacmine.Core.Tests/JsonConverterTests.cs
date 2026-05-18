using Xunit;
using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Core.Tests;

public class JsonConverterTests
{
    // ── VersionIdentifierJsonConverter ───────────────────────────────────

    [Fact]
    public void VersionIdentifier_Serialize_ProducesPlainString()
    {
        var vi = new VersionIdentifier("1.2.3");
        var json = JsonSerializer.Serialize(vi);
        Assert.Equal("\"1.2.3\"", json);
    }

    [Fact]
    public void VersionIdentifier_Deserialize_FromPlainString()
    {
        var vi = JsonSerializer.Deserialize<VersionIdentifier>("\"1.2.3\"");
        Assert.NotNull(vi);
        Assert.Equal("1.2.3", vi!.RawString);
    }

    [Fact]
    public void VersionIdentifier_Deserialize_NonSemVer()
    {
        var vi = JsonSerializer.Deserialize<VersionIdentifier>("\"25w14a\"");
        Assert.NotNull(vi);
        Assert.Equal("25w14a", vi!.RawString);
        Assert.Null(vi.SemVersion);
    }

    [Fact]
    public void VersionIdentifier_Roundtrip_PreservesValue()
    {
        var original = new VersionIdentifier("1.2.3-beta.1");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<VersionIdentifier>(json);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void VersionIdentifier_AsDictionaryKey_SerializesAndDeserializes()
    {
        var dict = new Dictionary<VersionIdentifier, string>
        {
            { new VersionIdentifier("1.0.0"), "stable" },
            { new VersionIdentifier("2.0.0"), "latest" }
        };

        var json = JsonSerializer.Serialize(dict);
        var deserialized = JsonSerializer.Deserialize<Dictionary<VersionIdentifier, string>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.Equal("stable", deserialized[new VersionIdentifier("1.0.0")]);
        Assert.Equal("latest", deserialized[new VersionIdentifier("2.0.0")]);
    }

    // ── VersionRangeJsonConverter ────────────────────────────────────────

    [Fact]
    public void VersionRange_Serialize_ProducesPlainString()
    {
        var range = new VersionRange("^1.2.3");
        var json = JsonSerializer.Serialize(range);
        // Should be a JSON string like ">=1.2.3-0 <2.0.0-0" (normalized by SemVersionRange)
        Assert.StartsWith("\"", json);
        Assert.EndsWith("\"", json);
    }

    [Fact]
    public void VersionRange_Deserialize_FromPlainString()
    {
        var range = JsonSerializer.Deserialize<VersionRange>("\"^1.2.3\"");
        Assert.NotNull(range);
        Assert.NotNull(range.ToString());
    }

    [Fact]
    public void VersionRange_Roundtrip_PreservesSemantics()
    {
        var original = new VersionRange(">=1.0.0 <2.0.0");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<VersionRange>(json);

        Assert.NotNull(deserialized);
        // Both should contain the same version
        Assert.True(deserialized!.Contains(new VersionIdentifier("1.5.0")));
        Assert.False(deserialized.Contains(new VersionIdentifier("2.0.0")));
    }

    [Fact]
    public void VersionRange_AsDictionaryValue_SerializesAndDeserializes()
    {
        var dict = new Dictionary<string, VersionRange>
        {
            { "pkg-a", new VersionRange("^1.0.0") },
            { "pkg-b", new VersionRange("~2.0.0") }
        };

        var json = JsonSerializer.Serialize(dict);
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, VersionRange>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.True(deserialized["pkg-a"].Contains(new VersionIdentifier("1.5.0")));
        Assert.True(deserialized["pkg-b"].Contains(new VersionIdentifier("2.0.1")));
    }
}
