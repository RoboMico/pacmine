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

    [Theory]
    [InlineData("1.0.0", "v1.0.0", -1)]          // SemVer-equivalent, '1' < 'v'
    [InlineData("v1.0.0", "1.0.0", 1)]           // SemVer-equivalent, 'v' > '1'
    [InlineData("1.0.0+build.1", "1.0.0+build.2", -1)] // same precedence, ordinal tiebreaker
    [InlineData("1.0.0+build.2", "1.0.0+build.1", 1)]
    [InlineData("1.0", "v1.0.0", -1)]            // optional patch → same SemVer, different raw
    [InlineData("v1.0.0", "1.0", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]            // identical raw strings still return 0
    [InlineData("1.0.0-alpha", "v1.0.0-alpha", -1)]
    public void CompareTo_SemVerEquivalentButDifferentRawString_ReturnsNonZero(string a, string b, int expected)
    {
        var viA = new VersionIdentifier(a);
        var viB = new VersionIdentifier(b);
        int result = Math.Sign(viA.CompareTo(viB));
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies the <see cref="IComparable{T}"/> contract invariant:
    /// <c>CompareTo == 0</c> must imply <c>Equals == true</c>.
    /// For every pair where <c>Equals</c> returns <c>false</c>, <c>CompareTo</c> must not return 0.
    /// </summary>
    [Theory]
    [InlineData("1.0.0",  "v1.0.0")]
    [InlineData("v1.0.0", "1.0.0")]
    [InlineData("1.0.0+build.1", "1.0.0+build.2")]
    [InlineData("1.0.0+build.2", "1.0.0+build.1")]
    [InlineData("1.0",  "v1.0.0")]
    [InlineData("v1.0.0", "1.0")]
    [InlineData("1.0.0-alpha",  "v1.0.0-alpha")]
    [InlineData("v1.0.0-alpha", "1.0.0-alpha")]
    [InlineData("1.0.0", "2.0.0")]
    public void CompareTo_Contract_EqualsFalseImpliesCompareToNotZero(string a, string b)
    {
        var viA = new VersionIdentifier(a);
        var viB = new VersionIdentifier(b);
        Assert.False(viA.Equals(viB), "Precondition: Equals must return false for this test to be meaningful.");
        Assert.NotEqual(0, viA.CompareTo(viB));
        Assert.NotEqual(0, viB.CompareTo(viA));
    }

    /// <summary>
    /// Verifies the converse: when <c>Equals</c> returns <c>true</c>, <c>CompareTo</c> must return 0.
    /// </summary>
    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("v1.0.0", "v1.0.0")]
    [InlineData("25w14a", "25w14a")]
    [InlineData("random", "random")]
    public void CompareTo_Contract_EqualsTrueImpliesCompareToZero(string a, string b)
    {
        var viA = new VersionIdentifier(a);
        var viB = new VersionIdentifier(b);
        Assert.True(viA.Equals(viB), "Precondition: Equals must return true for this test to be meaningful.");
        Assert.Equal(0, viA.CompareTo(viB));
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
        var a = new VersionIdentifier("23w33a");
        var b = new VersionIdentifier("23w33a");
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

    // ── Equality — raw string identity (new behavior) ────────────────────

    [Fact]
    public void Equals_SemanticallyEquivalentButDifferentRawString_ReturnsFalse()
    {
        // "1.0.0" and "v1.0.0" are semantically equivalent per SemVer,
        // but have different raw strings — Equals/== must return false.
        var a = new VersionIdentifier("1.0.0");
        var b = new VersionIdentifier("v1.0.0");
        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equals_SameVersionWithBuildMetadataDifferentRawString_ReturnsFalse()
    {
        // Build metadata does not affect SemVer precedence, but different
        // raw strings must make Equals return false.
        var a = new VersionIdentifier("1.0.0+build.1");
        var b = new VersionIdentifier("1.0.0+build.2");
        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equals_SameVersionWithAndWithoutVPrefix_ReturnsFalse()
    {
        var a = new VersionIdentifier("1.2.3");
        var b = new VersionIdentifier("v1.2.3");
        Assert.False(a.Equals(b));
    }

    // ── IsEquivalentTo (SemVer-compatible equality) ─────────────────────

    [Fact]
    public void IsEquivalentTo_SameRawString_ReturnsTrue()
    {
        var a = new VersionIdentifier("1.2.3");
        var b = new VersionIdentifier("1.2.3");
        Assert.True(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_SemVerWithVPrefix_ReturnsTrue()
    {
        var a = new VersionIdentifier("1.0.0");
        var b = new VersionIdentifier("v1.0.0");
        Assert.True(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_SemVerWithBuildMetadata_ReturnsTrue()
    {
        // Build metadata does not affect SemVer precedence,
        // so these are considered equivalent.
        var a = new VersionIdentifier("1.0.0+build.1");
        var b = new VersionIdentifier("1.0.0+build.2");
        Assert.True(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_SemVerWithVPrefixAndBuildMetadata_ReturnsTrue()
    {
        var a = new VersionIdentifier("1.0");
        var b = new VersionIdentifier("v1.0.0+xyz");
        Assert.True(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_SamePrerelease_ReturnsTrue()
    {
        var a = new VersionIdentifier("1.0.0-alpha.1");
        var b = new VersionIdentifier("1.0.0-alpha.1");
        Assert.True(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_DifferentSemVer_ReturnsFalse()
    {
        var a = new VersionIdentifier("1.0.0");
        var b = new VersionIdentifier("2.0.0");
        Assert.False(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_DifferentPrerelease_ReturnsFalse()
    {
        var a = new VersionIdentifier("1.0.0-alpha");
        var b = new VersionIdentifier("1.0.0-beta");
        Assert.False(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_SameNonSemVer_ReturnsTrue()
    {
        var a = new VersionIdentifier("25w14a");
        var b = new VersionIdentifier("25w14a");
        Assert.True(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_DifferentNonSemVer_ReturnsFalse()
    {
        var a = new VersionIdentifier("25w14a");
        var b = new VersionIdentifier("25w15a");
        Assert.False(a.IsEquivalentTo(b));
    }

    [Fact]
    public void IsEquivalentTo_MixedSemVerAndNonSemVer_FallsBackToRawString()
    {
        // When one side is not valid SemVer, falls back to raw string equality.
        var semver = new VersionIdentifier("1.0.0");
        var nonSemver = new VersionIdentifier("25w14a");
        Assert.False(semver.IsEquivalentTo(nonSemver));
    }

    [Fact]
    public void IsEquivalentTo_NullOther_ReturnsFalse()
    {
        var a = new VersionIdentifier("1.0.0");
        Assert.False(a.IsEquivalentTo(null));
    }

    // ── GetHashCode ─────────────────────────────────────────────────────

    [Fact]
    public void GetHashCode_SameVersion_ReturnsSameHash()
    {
        var a = new VersionIdentifier("1.2.3");
        var b = new VersionIdentifier("1.2.3");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentEquivalants_ReturnsDifferentHash()
    {
        var a = new VersionIdentifier("1.2.3+build.1");
        var b = new VersionIdentifier("1.2.3+build.2");
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
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

    // ── Immutability ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ParsesRawAndSemverCorrectly()
    {
        var vi = new VersionIdentifier("1.0.0");
        Assert.NotNull(vi.SemVersion);
        Assert.Equal("1.0.0", vi.RawString);
    }

    [Fact]
    public void Constructor_NonSemver_ResultsInNullSemver()
    {
        var vi = new VersionIdentifier("not-semver");
        Assert.Null(vi.SemVersion);
        Assert.Equal("not-semver", vi.RawString);
    }
}
