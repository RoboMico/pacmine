using Xunit;
using Pacmine.Core;

namespace Pacmine.Core.Tests;

public class VersionIdentifierTests
{
    // ── Parsing ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidSemVer_StoresParsedVersion()
    {
        var vi = new VersionIdentifier("1.2.3");
        Assert.Equal("1.2.3", vi.RawString);
        Assert.NotNull(vi.SemVersion);
    }

    [Fact]
    public void Parse_SemVerWithPrerelease_ParsesCorrectly()
    {
        var vi = new VersionIdentifier("1.2.3-beta.1");
        Assert.NotNull(vi.SemVersion);
        Assert.Equal("1.2.3-beta.1", vi.RawString);
    }

    [Fact]
    public void Parse_SemVerWithPrefixV_ParsesCorrectly()
    {
        var vi = new VersionIdentifier("v1.2.3");
        Assert.NotNull(vi.SemVersion);
        Assert.Equal("v1.2.3", vi.RawString);
    }

    [Fact]
    public void Parse_NonSemVerString_FallsBackToRaw()
    {
        // "25w14a" is a Minecraft snapshot version — not valid SemVer
        var vi = new VersionIdentifier("25w14a");
        Assert.Equal("25w14a", vi.RawString);
        Assert.Null(vi.SemVersion);
    }

    [Fact]
    public void Parse_ArbitraryString_FallsBackToRaw()
    {
        var vi = new VersionIdentifier("random-version-string");
        Assert.Equal("random-version-string", vi.RawString);
        Assert.Null(vi.SemVersion);
    }

    [Fact]
    public void Parse_EmptyString_DoesNotThrow()
    {
        var vi = new VersionIdentifier("");
        Assert.Equal("", vi.RawString);
        Assert.Null(vi.SemVersion);
    }

    // ── Segments ─────────────────────────────────────────────────────────

    [Fact]
    public void Segments_ValidSemVer_ReturnsMajorMinorPatch()
    {
        var vi = new VersionIdentifier("1.2.3");
        Assert.Equal(["1", "2", "3"], vi.Segments);
    }

    [Fact]
    public void Segments_SemVerWithPrerelease_IncludesPrereleaseIdentifiers()
    {
        var vi = new VersionIdentifier("1.2.3-beta.1");
        Assert.Equal(["1", "2", "3", "beta", "1"], vi.Segments);
    }

    [Fact]
    public void Segments_NonSemVer_ReturnsRawStringAsSingleSegment()
    {
        var vi = new VersionIdentifier("25w14a");
        Assert.Equal(["25w14a"], vi.Segments);
    }

    // ── Comparison (SemVer vs SemVer) ────────────────────────────────────

    [Theory]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("2.0.0", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0-alpha", "1.0.0", -1)]
    [InlineData("1.0.0", "1.0.0-alpha", 1)]
    public void CompareTo_SemVerVersions_ReturnsCorrectOrder(string a, string b, int expected)
    {
        var viA = new VersionIdentifier(a);
        var viB = new VersionIdentifier(b);
        int result = Math.Sign(viA.CompareTo(viB));
        Assert.Equal(expected, result);
    }

    // ── Comparison (fallback to string ordinal) ──────────────────────────

    [Theory]
    [InlineData("25w14a", "25w15a", -1)]
    [InlineData("25w15a", "25w14a", 1)]
    [InlineData("abc", "abc", 0)]
    [InlineData("abc", "def", -1)]
    public void CompareTo_NonSemVerVersions_UsesOrdinalStringCompare(string a, string b, int expected)
    {
        var viA = new VersionIdentifier(a);
        var viB = new VersionIdentifier(b);
        int result = Math.Sign(viA.CompareTo(viB));
        Assert.Equal(expected, result);
    }

    // ── Comparison (mixed) ──────────────────────────────────────────────

    [Fact]
    public void CompareTo_MixedSemVerAndNonSemVer_UsesOrdinalStringCompare()
    {
        var semver = new VersionIdentifier("1.0.0");
        var nonSemver = new VersionIdentifier("25w14a");
        // "1.0.0" vs "25w14a" → ordinal compare: '1' < '2' → "1.0.0" < "25w14a"
        Assert.True(semver.CompareTo(nonSemver) < 0);
    }

    // ── Null handling ────────────────────────────────────────────────────

    [Fact]
    public void CompareTo_NullOther_Returns1()
    {
        var vi = new VersionIdentifier("1.0.0");
        Assert.Equal(1, vi.CompareTo(null));
    }

    // ── Operators ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.0.0", "2.0.0", true, false, true, false)]
    [InlineData("2.0.0", "1.0.0", false, true, false, true)]
    [InlineData("1.0.0", "1.0.0", false, false, true, true)]
    public void Operators_AllComparisonOperators_WorkCorrectly(
        string a, string b,
        bool expectedLess, bool expectedGreater,
        bool expectedLessOrEqual, bool expectedGreaterOrEqual)
    {
        var viA = new VersionIdentifier(a);
        var viB = new VersionIdentifier(b);

        Assert.Equal(expectedLess, viA < viB);
        Assert.Equal(expectedGreater, viA > viB);
        Assert.Equal(expectedLessOrEqual, viA <= viB);
        Assert.Equal(expectedGreaterOrEqual, viA >= viB);
    }

    // ── Equality ─────────────────────────────────────────────────────────

    [Fact]
    public void Equals_SameSemVer_ReturnsTrue()
    {
        var a = new VersionIdentifier("1.2.3");
        var b = new VersionIdentifier("1.2.3");
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_DifferentSemVer_ReturnsFalse()
    {
        var a = new VersionIdentifier("1.2.3");
        var b = new VersionIdentifier("2.0.0");
        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equals_SameNonSemVer_ReturnsTrue()
    {
        var a = new VersionIdentifier("26.1");
        var b = new VersionIdentifier("26.1");
        Assert.True(a.Equals(b));
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_NullOther_ReturnsFalse()
    {
        var a = new VersionIdentifier("1.0.0");
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void Equals_ObjectNotVersionIdentifier_ReturnsFalse()
    {
        var a = new VersionIdentifier("1.0.0");
        Assert.False(a.Equals("not a version"));
    }

    [Fact]
    public void OperatorEquals_BothNull_ReturnsTrue()
    {
        VersionIdentifier? a = null;
        VersionIdentifier? b = null;
        Assert.True(a == b);
    }

    [Fact]
    public void OperatorEquals_OneNull_ReturnsFalse()
    {
        var a = new VersionIdentifier("1.0.0");
        VersionIdentifier? b = null;
        Assert.False(a == b);
        Assert.False(b == a);
    }

    [Fact]
    public void GetHashCode_SameVersion_ReturnsSameHash()
    {
        var a = new VersionIdentifier("1.2.3");
        var b = new VersionIdentifier("1.2.3");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentVersions_ReturnsDifferentHash()
    {
        var a = new VersionIdentifier("1.2.3");
        var b = new VersionIdentifier("2.0.0");
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    // ── ToString ─────────────────────────────────────────────────────────

    [Fact]
    public void ToString_ReturnsRawString()
    {
        var vi = new VersionIdentifier("1.2.3-beta");
        Assert.Equal("1.2.3-beta", vi.ToString());
    }

    // ── RawString setter re-parses ───────────────────────────────────────

    [Fact]
    public void RawString_Setter_ReparsesVersion()
    {
        var vi = new VersionIdentifier("1.0.0");
        Assert.NotNull(vi.SemVersion);

        vi.RawString = "not-semver";
        Assert.Null(vi.SemVersion);
        Assert.Equal("not-semver", vi.RawString);
    }
}
