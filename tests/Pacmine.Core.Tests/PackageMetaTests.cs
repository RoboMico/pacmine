using Xunit;
using Pacmine.Core;

namespace Pacmine.Core.Tests;

public class PackageMetaTests
{
    // ── Name validation: valid names ─────────────────────────────────────

    [Theory]
    [InlineData("my-pkg")]
    [InlineData("pkg123")]
    [InlineData("a.b_c")]
    [InlineData("hello")]
    [InlineData("123abc")]
    [InlineData("a")]
    public void Name_ValidName_DoesNotThrow(string name)
    {
        var meta = CreateMinimalMeta();
        var exception = Record.Exception(() => meta.Name = name);
        Assert.Null(exception);
    }

    // ── Name validation: invalid names ───────────────────────────────────

    [Theory]
    [InlineData("-pkg")]
    [InlineData("_pkg")]
    [InlineData(".pkg")]
    [InlineData("MyPkg")]
    [InlineData("UPPERCASE")]
    [InlineData("has space")]
    [InlineData("has@symbol")]
    [InlineData("")]
    public void Name_InvalidName_ThrowsArgumentException(string name)
    {
        var meta = CreateMinimalMeta();
        Assert.Throws<ArgumentException>(() => meta.Name = name);
    }

    // ── Constructor validation ──────────────────────────────────────────

    [Fact]
    public void Constructor_InvalidName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PackageMeta
        {
            Name = "Invalid-Name", // uppercase I
            Version = new VersionIdentifier("1.0.0")
        });
    }

    [Fact]
    public void Constructor_ValidName_Succeeds()
    {
        var meta = new PackageMeta
        {
            Name = "valid-name",
            Version = new VersionIdentifier("1.0.0")
        };
        Assert.Equal("valid-name", meta.Name);
    }

    // ── Default values ──────────────────────────────────────────────────

    [Fact]
    public void DefaultValues_ReleaseIs1_EpochIs0()
    {
        var meta = CreateMinimalMeta();
        Assert.Equal(1, meta.Release);
        Assert.Equal(0, meta.Epoch);
        Assert.Empty(meta.Groups);
        Assert.Empty(meta.Provides);
        Assert.Empty(meta.Depends);
        Assert.Empty(meta.Conflicts);
        Assert.Empty(meta.Replaces);
        Assert.Empty(meta.Recommends);
    }

    // ── Recommends ────────────────────────────────────────────────────────

    [Fact]
    public void Recommends_DefaultIsEmpty()
    {
        var meta = CreateMinimalMeta();
        Assert.Empty(meta.Recommends);
    }

    [Fact]
    public void Recommends_CanAddRecommendations()
    {
        var meta = CreateMinimalMeta();
        meta.Recommends = new()
        {
            { "helper-mod", "Provides additional QoL features" },
            { "shader-pack", "Enhances visual quality" }
        };

        Assert.Equal(2, meta.Recommends.Count);
        Assert.Equal("Provides additional QoL features", meta.Recommends["helper-mod"]);
        Assert.Equal("Enhances visual quality", meta.Recommends["shader-pack"]);
    }

    [Fact]
    public void Recommends_SerializesAndDeserializes()
    {
        var meta = CreateMinimalMeta(name: "recommend-test", version: "1.0.0");
        meta.Recommends = new()
        {
            { "opt-dep", "Optional dependency" }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(meta);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<PackageMeta>(json);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized!.Recommends);
        Assert.Equal("Optional dependency", deserialized.Recommends["opt-dep"]);
    }

    // ── GetFullVersionString ─────────────────────────────────────────────

    [Fact]
    public void GetFullVersionString_NoEpoch_ReturnsVersionRelease()
    {
        var meta = CreateMinimalMeta(version: "1.0.0", release: 1, epoch: 0);
        Assert.Equal("1.0.0#1", meta.GetFullVersionString());
    }

    [Fact]
    public void GetFullVersionString_WithEpoch_IncludesEpochPrefix()
    {
        var meta = CreateMinimalMeta(version: "1.0.0", release: 2, epoch: 1);
        Assert.Equal("1:1.0.0#2", meta.GetFullVersionString());
    }

    // ── IsNewerThan ──────────────────────────────────────────────────────

    [Fact]
    public void IsNewerThan_HigherEpochWins_ReturnsTrue()
    {
        var older = CreateMinimalMeta(version: "2.0.0", epoch: 0);
        var newer = CreateMinimalMeta(version: "1.0.0", epoch: 1);
        Assert.True(newer.IsNewerThan(older));
        Assert.False(older.IsNewerThan(newer));
    }

    [Fact]
    public void IsNewerThan_SameEpochHigherVersionWins_ReturnsTrue()
    {
        var older = CreateMinimalMeta(version: "1.0.0");
        var newer = CreateMinimalMeta(version: "2.0.0");
        Assert.True(newer.IsNewerThan(older));
        Assert.False(older.IsNewerThan(newer));
    }

    [Fact]
    public void IsNewerThan_SameVersionHigherReleaseWins_ReturnsTrue()
    {
        var older = CreateMinimalMeta(version: "1.0.0", release: 1);
        var newer = CreateMinimalMeta(version: "1.0.0", release: 2);
        Assert.True(newer.IsNewerThan(older));
        Assert.False(older.IsNewerThan(newer));
    }

    [Fact]
    public void IsNewerThan_SameEverything_ReturnsFalse()
    {
        var a = CreateMinimalMeta(version: "1.0.0", release: 1, epoch: 0);
        var b = CreateMinimalMeta(version: "1.0.0", release: 1, epoch: 0);
        Assert.False(a.IsNewerThan(b));
        Assert.False(b.IsNewerThan(a));
    }

    // ── IsConflictingWith: direct conflicts ──────────────────────────────

    [Fact]
    public void IsConflictingWith_DirectConflictMatch_ReturnsTrue()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "1.0.0");
        var b = CreateMinimalMeta(name: "pkg-b", version: "1.0.0",
            conflicts: new() { { "pkg-a", new VersionRange("^1.0.0") } });

        Assert.True(b.IsConflictingWith(a));
    }

    [Fact]
    public void IsConflictingWith_VersionOutsideRange_ReturnsFalse()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "2.0.0");
        var b = CreateMinimalMeta(name: "pkg-b", version: "1.0.0",
            conflicts: new() { { "pkg-a", new VersionRange("^1.0.0") } });

        Assert.False(b.IsConflictingWith(a));
    }

    // ── IsConflictingWith: virtual package conflicts ─────────────────────

    [Fact]
    public void IsConflictingWith_VirtualPackageConflict_ReturnsTrue()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "1.0.0",
            provides: new() { { "virtual-pkg", new VersionIdentifier("2.0.0") } });
        var b = CreateMinimalMeta(name: "pkg-b", version: "1.0.0",
            conflicts: new() { { "virtual-pkg", new VersionRange("^2.0.0") } });

        Assert.True(b.IsConflictingWith(a));
    }

    [Fact]
    public void IsConflictingWith_ReverseConflict_ReturnsTrue()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "1.0.0",
            conflicts: new() { { "pkg-b", new VersionRange("^1.0.0") } });
        var b = CreateMinimalMeta(name: "pkg-b", version: "1.0.0");

        Assert.True(a.IsConflictingWith(b));
    }

    [Fact]
    public void IsConflictingWith_ReverseVirtualConflict_ReturnsTrue()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "1.0.0",
            conflicts: new() { { "virtual-pkg", new VersionRange("^1.0.0") } });
        var b = CreateMinimalMeta(name: "pkg-b", version: "1.0.0",
            provides: new() { { "virtual-pkg", new VersionIdentifier("1.5.0") } });

        Assert.True(a.IsConflictingWith(b));
    }

    // ── IsConflictingWith: virtual package with non-SemVer version ────────

    [Fact]
    public void IsConflictingWith_VirtualPackageNonSemVer_AnyRange_ReturnsTrue()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "1.0.0",
            provides: new() { { "virtual-pkg", new VersionIdentifier("25w14a") } });
        var b = CreateMinimalMeta(name: "pkg-b", version: "1.0.0",
            conflicts: new() { { "virtual-pkg", new VersionRange("*") } });

        Assert.True(b.IsConflictingWith(a));
    }

    [Fact]
    public void IsConflictingWith_VirtualPackageNonSemVer_ExactLiteralRange_ReturnsTrue()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "1.0.0",
            provides: new() { { "virtual-pkg", new VersionIdentifier("25w14a") } });
        var b = CreateMinimalMeta(name: "pkg-b", version: "1.0.0",
            conflicts: new() { { "virtual-pkg", new VersionRange("25w14a") } });

        Assert.True(b.IsConflictingWith(a));
    }

    [Fact]
    public void IsConflictingWith_VirtualPackageNonSemVer_ExactLiteralRange_ReturnsFalse()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "1.0.0",
            provides: new() { { "virtual-pkg", new VersionIdentifier("25w14a") } });
        var b = CreateMinimalMeta(name: "pkg-b", version: "1.0.0",
            conflicts: new() { { "virtual-pkg", new VersionRange("25w14b") } });

        Assert.False(b.IsConflictingWith(a));
    }

    [Fact]
    public void IsConflictingWith_NoConflicts_ReturnsFalse()
    {
        var a = CreateMinimalMeta(name: "pkg-a", version: "1.0.0");
        var b = CreateMinimalMeta(name: "pkg-b", version: "2.0.0");

        Assert.False(a.IsConflictingWith(b));
        Assert.False(b.IsConflictingWith(a));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static PackageMeta CreateMinimalMeta(
        string name = "test-pkg",
        string version = "1.0.0",
        int release = 1,
        int epoch = 0,
        Dictionary<string, VersionRange>? depends = null,
        Dictionary<string, VersionRange>? conflicts = null,
        Dictionary<string, VersionIdentifier>? provides = null)
    {
        return new PackageMeta
        {
            Name = name,
            Version = new VersionIdentifier(version),
            Release = release,
            Epoch = epoch,
            Depends = depends ?? [],
            Conflicts = conflicts ?? [],
            Provides = provides ?? []
        };
    }
}
