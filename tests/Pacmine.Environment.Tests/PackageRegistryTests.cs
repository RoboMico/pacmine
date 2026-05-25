using Xunit;
using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment.Tests;

public class PackageRegistryTests
{
    // ── AreFilesConflictingWith ──────────────────────────────────────────

    [Fact]
    public void AreFilesConflictingWith_OverlappingFiles_ReturnsTrue()
    {
        var regA = CreateRegistry("pkg-a", "1.0.0",
            fileList: new() { { "mods/shared.jar", "abc" }, { "mods/a.jar", "def" } });
        var regB = CreateRegistry("pkg-b", "1.0.0",
            fileList: new() { { "mods/shared.jar", "abc" }, { "mods/b.jar", "ghi" } });

        Assert.True(regA.AreFilesConflictingWith(regB));
        Assert.True(regB.AreFilesConflictingWith(regA));
    }

    [Fact]
    public void AreFilesConflictingWith_DisjointFiles_ReturnsFalse()
    {
        var regA = CreateRegistry("pkg-a", "1.0.0",
            fileList: new() { { "mods/a.jar", "abc" } });
        var regB = CreateRegistry("pkg-b", "1.0.0",
            fileList: new() { { "mods/b.jar", "def" } });

        Assert.False(regA.AreFilesConflictingWith(regB));
    }

    [Fact]
    public void AreFilesConflictingWith_BothEmpty_ReturnsFalse()
    {
        var regA = CreateRegistry("pkg-a", "1.0.0");
        var regB = CreateRegistry("pkg-b", "1.0.0");

        Assert.False(regA.AreFilesConflictingWith(regB));
    }

    [Fact]
    public void AreFilesConflictingWith_OneEmpty_ReturnsFalse()
    {
        var regA = CreateRegistry("pkg-a", "1.0.0",
            fileList: new() { { "mods/a.jar", "abc" } });
        var regB = CreateRegistry("pkg-b", "1.0.0");

        Assert.False(regA.AreFilesConflictingWith(regB));
    }

    // ── JSON Serialization Roundtrip ─────────────────────────────────────

    [Fact]
    public void SerializeDeserialize_PreservesAllFields()
    {
        var original = new PackageRegistry
        {
            Meta = new PackageMeta
            {
                Name = "test-pkg",
                Version = new VersionIdentifier("1.2.3"),
                Description = "A test package",
                Category = "mod",
                License = "MIT",
                Release = 2,
                Epoch = 1,
                Depends = new()
                {
                    { "dep-a", new VersionRange("^1.0.0") }
                },
                Conflicts = new()
                {
                    { "conflict-pkg", new VersionRange("^2.0.0") }
                }
            },
            FileList = new()
            {
                { "mods/test.jar", "abcdef123456" },
                { "config/test.cfg", "fedcba654321" }
            },
            InstallReason = InstallReasons.AsDependency,
            PackagedTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            InstalledTime = new DateTime(2025, 6, 16, 8, 30, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PackageRegistry>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("test-pkg", deserialized!.Meta.Name);
        Assert.Equal("1.2.3", deserialized.Meta.Version.RawString);
        Assert.Equal("A test package", deserialized.Meta.Description);
        Assert.Equal("mod", deserialized.Meta.Category);
        Assert.Equal("MIT", deserialized.Meta.License);
        Assert.Equal(2, deserialized.Meta.Release);
        Assert.Equal(1, deserialized.Meta.Epoch);
        Assert.Equal(InstallReasons.AsDependency, deserialized.InstallReason);
        Assert.Equal(2, deserialized.FileList.Count);
        Assert.Equal("abcdef123456", deserialized.FileList["mods/test.jar"]);
        Assert.Single(deserialized.Meta.Depends);
        Assert.Single(deserialized.Meta.Conflicts);
    }

    [Fact]
    public void SerializeDeserialize_MinimalRegistry_PreservesDefaults()
    {
        var original = new PackageRegistry
        {
            Meta = new PackageMeta
            {
                Name = "minimal",
                Version = new VersionIdentifier("1.0.0")
            },
            InstallReason = InstallReasons.Explicit,
            InstalledTime = DateTime.Now
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PackageRegistry>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("minimal", deserialized!.Meta.Name);
        Assert.Equal("1.0.0", deserialized.Meta.Version.RawString);
        Assert.Equal(InstallReasons.Explicit, deserialized.InstallReason);
        Assert.Empty(deserialized.FileList);
        Assert.Empty(deserialized.Meta.Depends);
        Assert.Empty(deserialized.Meta.Conflicts);
        Assert.Equal(1, deserialized.Meta.Release); // default
        Assert.Equal(0, deserialized.Meta.Epoch); // default
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static PackageRegistry CreateRegistry(
        string name,
        string version,
        Dictionary<string, string>? fileList = null)
    {
        return new PackageRegistry
        {
            Meta = new PackageMeta
            {
                Name = name,
                Version = new VersionIdentifier(version)
            },
            FileList = fileList ?? [],
            InstallReason = InstallReasons.Explicit,
            InstalledTime = DateTime.Now
        };
    }
}
