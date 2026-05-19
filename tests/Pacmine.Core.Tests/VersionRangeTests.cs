using Xunit;
using Pacmine.Core;

namespace Pacmine.Core.Tests;

public class VersionRangeTests
{
    // ── Parsing: valid ranges (SemVer mode) ────────────────────────────────

    [Theory]
    [InlineData("^1.2.3")]
    [InlineData("~1.2")]
    [InlineData(">=1.0.0")]
    [InlineData(">1.0.0")]
    [InlineData("<=1.0.0")]
    [InlineData("<1.0.0")]
    [InlineData("1.0.0")]
    [InlineData("1.x")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3 - 2.0.0")]
    [InlineData("1.0.0 || 2.0.0")]
    public void Parse_ValidRange_DoesNotThrow(string rangeExpr)
    {
        var exception = Record.Exception(() => new VersionRange(rangeExpr));
        Assert.Null(exception);
    }

    // ── Parsing: previously-invalid strings now become literal mode ────────

    [Theory]
    [InlineData("not-a-range")]
    [InlineData("^")]
    [InlineData(">=")]
    public void Parse_InvalidString_BecomesLiteralMode(string rangeExpr)
    {
        var range = new VersionRange(rangeExpr);
        // Literal-mode ranges can only match an identical raw string
        Assert.True(range.Contains(new VersionIdentifier(rangeExpr)));
        Assert.False(range.Contains(new VersionIdentifier("something-else")));
    }

    // ── Contains: version within range (SemVer mode) ──────────────────────

    [Theory]
    [InlineData("^1.0.0", "1.5.0")]
    [InlineData("^1.0.0", "1.9.9")]
    [InlineData("~1.2.0", "1.2.5")]
    [InlineData(">=1.0.0", "2.0.0")]
    [InlineData(">1.0.0", "1.0.1")]
    [InlineData("<=2.0.0", "1.0.0")]
    [InlineData("<2.0.0", "1.9.9")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.x", "1.5.0")]
    [InlineData("1.2.3 - 2.0.0", "1.5.0")]
    [InlineData("1.0.0 || 2.0.0", "1.0.0")]
    [InlineData("1.0.0 || 2.0.0", "2.0.0")]
    public void Contains_VersionWithinRange_ReturnsTrue(string rangeExpr, string versionStr)
    {
        var range = new VersionRange(rangeExpr);
        var version = new VersionIdentifier(versionStr);
        Assert.True(range.Contains(version));
    }

    // ── Contains: version outside range (SemVer mode) ──────────────────────

    [Theory]
    [InlineData("^1.0.0", "2.0.0")]
    [InlineData("^1.0.0", "0.9.0")]
    [InlineData("~1.2.0", "1.3.0")]
    [InlineData(">=2.0.0", "1.0.0")]
    [InlineData(">2.0.0", "2.0.0")]
    [InlineData("<=1.0.0", "2.0.0")]
    [InlineData("<1.0.0", "1.0.0")]
    [InlineData("1.2.3", "1.2.4")]
    [InlineData("1.2.3 - 2.0.0", "0.5.0")]
    [InlineData("1.0.0 || 2.0.0", "1.5.0")]
    public void Contains_VersionOutsideRange_ReturnsFalse(string rangeExpr, string versionStr)
    {
        var range = new VersionRange(rangeExpr);
        var version = new VersionIdentifier(versionStr);
        Assert.False(range.Contains(version));
    }

    // ── Contains: non-SemVer version never matches SemVer mode ────────────

    [Theory]
    [InlineData(">=1.0.0")]
    [InlineData("^0.0.0")]
    public void Contains_NonSemVerVersion_SemVerMode_ReturnsFalse(string rangeExpr)
    {
        var range = new VersionRange(rangeExpr);
        var version = new VersionIdentifier("25w14a"); // non-SemVer
        Assert.False(range.Contains(version));
    }

    // ── Contains: non-SemVer version with literal mode ────────────────────

    [Fact]
    public void Contains_NonSemVerVersion_LiteralMode_ExactMatch()
    {
        var range = new VersionRange("25w14a");
        var matchingVersion = new VersionIdentifier("25w14a");
        var nonMatchingVersion = new VersionIdentifier("other-version");
        Assert.True(range.Contains(matchingVersion));
        Assert.False(range.Contains(nonMatchingVersion));
    }

    // ── Any range: matches everything ─────────────────────────────────────

    [Fact]
    public void AnyRange_MatchesAllVersions()
    {
        Assert.True(VersionRange.Any.Contains(new VersionIdentifier("0.0.1")));
        Assert.True(VersionRange.Any.Contains(new VersionIdentifier("999.999.999")));
        Assert.True(VersionRange.Any.Contains(new VersionIdentifier("25w14a")));
        Assert.True(VersionRange.Any.Contains(new VersionIdentifier("anything-at-all")));
    }

    // ── Literal mode: exact match only ────────────────────────────────────

    [Theory]
    [InlineData("nightly")]
    [InlineData("latest")]
    [InlineData("25w14a")]
    [InlineData("v1.0.0-beta+xyz")] // non-standard SemVer that isn't an npm range
    public void LiteralMode_ExactMatchOnly(string literal)
    {
        var range = new VersionRange(literal);
        Assert.True(range.Contains(new VersionIdentifier(literal)));
        Assert.False(range.Contains(new VersionIdentifier("different")));
    }

    [Fact]
    public void LiteralMode_DoesNotMatchPartial()
    {
        var range = new VersionRange("nightly");
        Assert.False(range.Contains(new VersionIdentifier("nightly-1")));
        Assert.False(range.Contains(new VersionIdentifier("nightly-build")));
    }

    // ── Literal mode: version-like strings that are not valid npm ranges ──

    [Fact]
    public void LiteralMode_WithVersionTag()
    {
        // "1.0.0.0" is not a valid npm-style range, so it becomes literal mode
        var range = new VersionRange("1.0.0.0");
        Assert.True(range.Contains(new VersionIdentifier("1.0.0.0")));
        Assert.False(range.Contains(new VersionIdentifier("1.0.0"))); // not exact match
    }

    // ── ToString ─────────────────────────────────────────────────────────

    [Fact]
    public void ToString_ReturnsNormalizedRange_SemVerMode()
    {
        var range = new VersionRange(">=1.0.0");
        Assert.NotNull(range.ToString());
        Assert.NotEmpty(range.ToString());
    }

    [Fact]
    public void ToString_ReturnsAsterisk_AnyMode()
    {
        var range = new VersionRange("*");
        Assert.Equal("*", range.ToString());
    }

    [Fact]
    public void ToString_ReturnsLiteral_LiteralMode()
    {
        var range = new VersionRange("some-tag");
        Assert.Equal("some-tag", range.ToString());
    }
}
