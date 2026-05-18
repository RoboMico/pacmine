using Xunit;
using Pacmine.Core;
using Lua;

namespace Pacmine.PackageCraft.Tests;

public class LuaObjectConversionTests
{
    // ── PackageMetaLuaObject ─────────────────────────────────────────────

    [Fact]
    public void PackageMetaLuaObject_ToLuaTable_ContainsAllFields()
    {
        var pmo = new PackageMetaLuaObject
        {
            Name = "test-pkg",
            Description = "A test",
            UpstreamUrl = "https://example.com",
            Category = "mod",
            License = "MIT",
            Version = new VersionIdentifier("1.2.3"),
            Release = 2,
            Epoch = 1
        };

        var table = pmo.ToLuaTable();

        Assert.Equal("test-pkg", table["name"].Read<string>());
        Assert.Equal("A test", table["description"].Read<string>());
        Assert.Equal("https://example.com", table["upstream_url"].Read<string>());
        Assert.Equal("mod", table["category"].Read<string>());
        Assert.Equal("MIT", table["license"].Read<string>());
        Assert.Equal("1.2.3", table["version"].Read<string>());
        Assert.Equal(2, table["release"].Read<int>());
        Assert.Equal(1, table["epoch"].Read<int>());
    }

    [Fact]
    public void PackageMetaLuaObject_FromLuaTable_Roundtrips()
    {
        var original = new PackageMetaLuaObject
        {
            Name = "test-pkg",
            Description = "A test package",
            UpstreamUrl = "https://example.com/pkg",
            Category = "mod",
            License = "MIT",
            Version = new VersionIdentifier("2.0.0"),
            Release = 3,
            Epoch = 0
        };

        var table = original.ToLuaTable();
        var restored = PackageMetaLuaObject.FromLuaTable(table);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.UpstreamUrl, restored.UpstreamUrl);
        Assert.Equal(original.Category, restored.Category);
        Assert.Equal(original.License, restored.License);
        Assert.Equal(original.Version.RawString, restored.Version.RawString);
        Assert.Equal(original.Release, restored.Release);
        Assert.Equal(original.Epoch, restored.Epoch);
    }

    [Fact]
    public void PackageMetaLuaObject_WithDepends_Roundtrips()
    {
        var original = new PackageMetaLuaObject
        {
            Name = "with-deps",
            Description = "Has dependencies",
            UpstreamUrl = "https://example.com",
            Category = "mod",
            License = "MIT",
            Version = new VersionIdentifier("1.0.0"),
            Depends = new()
            {
                { "dep-a", new VersionRange("^1.0.0") },
                { "dep-b", new VersionRange("~2.0.0") }
            }
        };

        var table = original.ToLuaTable();
        var restored = PackageMetaLuaObject.FromLuaTable(table);

        Assert.Equal(2, restored.Depends.Count);
        Assert.True(restored.Depends.ContainsKey("dep-a"));
        Assert.True(restored.Depends.ContainsKey("dep-b"));
    }

    [Fact]
    public void PackageMetaLuaObject_WithConflicts_Roundtrips()
    {
        var original = new PackageMetaLuaObject
        {
            Name = "conflicting",
            Description = "Has conflicts",
            UpstreamUrl = "https://example.com",
            Category = "mod",
            License = "MIT",
            Version = new VersionIdentifier("1.0.0"),
            Conflicts = new()
            {
                { "other-pkg", new VersionRange("^1.0.0") }
            }
        };

        var table = original.ToLuaTable();
        var restored = PackageMetaLuaObject.FromLuaTable(table);

        Assert.Single(restored.Conflicts);
        Assert.True(restored.Conflicts.ContainsKey("other-pkg"));
    }

    // ── PackageCraftRecipeLuaObject ──────────────────────────────────────

    [Fact]
    public void PackageCraftRecipeLuaObject_ToLuaTable_ContainsRecipeFields()
    {
        var recipe = new PackageCraftRecipeLuaObject
        {
            Protocol = "1.0",
            Meta = new PackageMetaLuaObject
            {
                Name = "recipe-pkg",
                Description = "Recipe test",
                UpstreamUrl = "https://example.com",
                Category = "mod",
                License = "MIT",
                Version = new VersionIdentifier("1.0.0")
            },
            Sources = ["src1.tar.gz", "src2.patch"],
            SourceChecksums = ["SKIP", "sha256:abc123"]
        };

        var table = recipe.ToLuaTable();

        Assert.Equal("1.0", table["protocol"].Read<string>());
        Assert.NotNull(table["meta"].Read<LuaTable>());
        Assert.Equal("recipe-pkg", table["meta"].Read<LuaTable>()["name"].Read<string>());
    }

    [Fact]
    public void PackageCraftRecipeLuaObject_FromLuaTable_Roundtrips()
    {
        var original = new PackageCraftRecipeLuaObject
        {
            Protocol = "2.0",
            Meta = new PackageMetaLuaObject
            {
                Name = "test-recipe",
                Description = "Test",
                UpstreamUrl = "https://example.com",
                Category = "resourcepack",
                License = "MIT",
                Version = new VersionIdentifier("3.0.0"),
                Release = 1
            },
            Sources = ["source.zip"],
            SourceChecksums = ["sha256:def456"],
            Prepare = null,
            Build = null
        };

        var table = original.ToLuaTable();
        var restored = PackageCraftRecipeLuaObject.FromLuaTable(table);

        Assert.Equal(original.Protocol, restored.Protocol);
        Assert.Equal(original.Meta.Name, restored.Meta.Name);
        Assert.Equal(original.Sources.Count, restored.Sources.Count);
        Assert.Equal(original.SourceChecksums.Count, restored.SourceChecksums.Count);
    }

    [Fact]
    public void PackageCraftRecipeLuaObject_NullLuaFunctions_AreNil()
    {
        var recipe = new PackageCraftRecipeLuaObject
        {
            Protocol = "1.0",
            Meta = new PackageMetaLuaObject
            {
                Name = "no-funcs",
                Description = "No Lua functions",
                UpstreamUrl = "https://example.com",
                Category = "mod",
                License = "MIT",
                Version = new VersionIdentifier("1.0.0")
            }
        };

        var table = recipe.ToLuaTable();

        Assert.Equal(LuaValueType.Nil, table["prepare"].Type);
        Assert.Equal(LuaValueType.Nil, table["build"].Type);
        Assert.Equal(LuaValueType.Nil, table["check"].Type);
        Assert.Equal(LuaValueType.Nil, table["package"].Type);
        Assert.Equal(LuaValueType.Nil, table["get_version"].Type);
    }
}
