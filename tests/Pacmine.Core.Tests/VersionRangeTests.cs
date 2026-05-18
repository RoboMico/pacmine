using Xunit;
using Pacmine.Core;

namespace Pacmine.Core.Tests;

public class VersionRangeTests
{
    // ── Parsing: valid ranges ────────────────────────────────────────────

    [Theory]
    [InlineData("^1.2.3")]
    [InlineData("~1.2")]
    [InlineData(">=1.0.0")]
    [InlineData(">1.0.0")]
    [InlineData("<=1.0.0")]
    [InlineData("<1.0.0")]
    [InlineData("1.0.0")]
    [InlineData("*")]
    [InlineData("1.x")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3 - 2.0.0")]
    [InlineData("1.0.0 || 2.0.0")]
    public void Parse_ValidRange_DoesNotThrow(string rangeExpr)
    {
        var exception = Record.Exception(() => new VersionRange(rangeExpr));
        Assert.Null(exception);
    }

    // ── Parsing: invalid ranges ──────────────────────────────────────────

    [Theory]
    [InlineData("not-a-range")]
    [InlineData("^")]
    [InlineData(">=")]
    public void Parse_InvalidRange_ThrowsInvalidDataException(string rangeExpr)
    {
        Assert.Throws<InvalidDataException>(() => new VersionRange(rangeExpr));
    }

    // ── Contains: version within range ───────────────────────────────────

    [Theory]
    [InlineData("^1.0.0", "1.5.0")]
    [InlineData("^1.0.0", "1.9.9")]
    [InlineData("~1.2.0", "1.2.5")]
    [InlineData(">=1.0.0", "2.0.0")]
    [InlineData(">1.0.0", "1.0.1")]
    [InlineData("<=2.0.0", "1.0.0")]
    [InlineData("<2.0.0", "1.9.9")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("*", "1.0.0")]
    [InlineData("*", "999.999.999")]
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

    // ── Contains: version outside range ──────────────────────────────────

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

    // ── Contains: non-SemVer version never matches ───────────────────────

    [Theory]
    [InlineData("*")]
    [InlineData(">=1.0.0")]
    [InlineData("^0.0.0")]
    public void Contains_NonSemVerVersion_ReturnsFalse(string rangeExpr)
    {
        var range = new VersionRange(rangeExpr);
        var version = new VersionIdentifier("25w14a"); // non-SemVer
        Assert.False(range.Contains(version));
    }

    // ── Any range ────────────────────────────────────────────────────────

    [Fact]
    public void AnyRange_MatchesAllSemVerVersions()
    {
        Assert.True(VersionRange.Any.Contains(new VersionIdentifier("0.0.1")));
        Assert.True(VersionRange.Any.Contains(new VersionIdentifier("999.999.999")));
    }

    // ── ToString ─────────────────────────────────────────────────────────

    [Fact]
    public void ToString_ReturnsNormalizedRange()
    {
        var range = new VersionRange(">=1.0.0");
        Assert.NotNull(range.ToString());
        Assert.NotEmpty(range.ToString());
    }
}
