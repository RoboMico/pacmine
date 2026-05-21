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
        var pmo = new PackageMetaLuaObject(new PackageMeta
        {
            Name = "test-pkg",
            Version = new VersionIdentifier("1.2.3"),
            Description = "A test",
            UpstreamUrl = "https://example.com",
            Category = "mod",
            License = "MIT",
            Release = 2,
            Epoch = 1
        });

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
        var original = new PackageMetaLuaObject(new PackageMeta
        {
            Name = "test-pkg",
            Version = new VersionIdentifier("2.0.0"),
            Description = "A test package",
            UpstreamUrl = "https://example.com/pkg",
            Category = "mod",
            License = "MIT",
            Release = 3,
            Epoch = 0
        });

        var table = original.ToLuaTable();
        var restored = PackageMetaLuaObject.FromLuaTable(table);

        Assert.Equal(original.Meta.Name, restored.Meta.Name);
        Assert.Equal(original.Meta.Description, restored.Meta.Description);
        Assert.Equal(original.Meta.UpstreamUrl, restored.Meta.UpstreamUrl);
        Assert.Equal(original.Meta.Category, restored.Meta.Category);
        Assert.Equal(original.Meta.License, restored.Meta.License);
        Assert.Equal(original.Meta.Version.RawString, restored.Meta.Version.RawString);
        Assert.Equal(original.Meta.Release, restored.Meta.Release);
        Assert.Equal(original.Meta.Epoch, restored.Meta.Epoch);
    }

    [Fact]
    public void PackageMetaLuaObject_WithDepends_Roundtrips()
    {
        var original = new PackageMetaLuaObject(new PackageMeta
        {
            Name = "with-deps",
            Version = new VersionIdentifier("1.0.0"),
            Depends = new()
            {
                { "dep-a", new VersionRange("^1.0.0") },
                { "dep-b", new VersionRange("~2.0.0") }
            }
        });

        var table = original.ToLuaTable();
        var restored = PackageMetaLuaObject.FromLuaTable(table);

        Assert.Equal(2, restored.Meta.Depends.Count);
        Assert.True(restored.Meta.Depends.ContainsKey("dep-a"));
        Assert.True(restored.Meta.Depends.ContainsKey("dep-b"));
    }

    [Fact]
    public void PackageMetaLuaObject_WithConflicts_Roundtrips()
    {
        var original = new PackageMetaLuaObject(new PackageMeta
        {
            Name = "conflicting",
            Version = new VersionIdentifier("1.0.0"),
            Conflicts = new()
            {
                { "other-pkg", new VersionRange("^1.0.0") }
            }
        });

        var table = original.ToLuaTable();
        var restored = PackageMetaLuaObject.FromLuaTable(table);

        Assert.Single(restored.Meta.Conflicts);
        Assert.True(restored.Meta.Conflicts.ContainsKey("other-pkg"));
    }

    // ── PackageCraftRecipeLuaObject ──────────────────────────────────────

    [Fact]
    public void PackageCraftRecipeLuaObject_ToLuaTable_ContainsRecipeFields()
    {
        var recipe = new PackageCraftRecipeLuaObject(new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new PackageMeta
            {
                Name = "recipe-pkg",
                Version = new VersionIdentifier("1.0.0")
            },
            Sources = ["src1.tar.gz", "src2.patch"],
            SourceChecksums = ["SKIP", "sha256:abc123"]
        });

        var table = recipe.ToLuaTable();

        Assert.Equal("1.0", table["protocol"].Read<string>());
        Assert.NotNull(table["meta"].Read<LuaTable>());
        Assert.Equal("recipe-pkg", table["meta"].Read<LuaTable>()["name"].Read<string>());
    }

    [Fact]
    public void PackageCraftRecipeLuaObject_FromLuaTable_Roundtrips()
    {
        var original = new PackageCraftRecipeLuaObject(new PackageCraftRecipe
        {
            Protocol = "2.0",
            Meta = new PackageMeta
            {
                Name = "test-recipe",
                Version = new VersionIdentifier("3.0.0")
            },
            Sources = ["source.zip"],
            SourceChecksums = ["sha256:def456"],
            Prepare = null,
            Build = null
        });

        var table = original.ToLuaTable();
        var restored = PackageCraftRecipeLuaObject.FromLuaTable(table);

        Assert.Equal(original.Recipe.Protocol, restored.Recipe.Protocol);
        Assert.Equal(original.Recipe.Meta.Name, restored.Recipe.Meta.Name);
        Assert.Equal(original.Recipe.Sources.Count, restored.Recipe.Sources.Count);
        Assert.Equal(original.Recipe.SourceChecksums.Count, restored.Recipe.SourceChecksums.Count);
    }

    [Fact]
    public void PackageCraftRecipeLuaObject_NullLuaFunctions_AreNil()
    {
        var recipe = new PackageCraftRecipeLuaObject(new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new PackageMeta
            {
                Name = "no-funcs",
                Version = new VersionIdentifier("1.0.0")
            }
        });

        var table = recipe.ToLuaTable();

        Assert.Equal(LuaValueType.Nil, table["prepare"].Type);
        Assert.Equal(LuaValueType.Nil, table["build"].Type);
        Assert.Equal(LuaValueType.Nil, table["check"].Type);
        Assert.Equal(LuaValueType.Nil, table["package"].Type);
        Assert.Equal(LuaValueType.Nil, table["get_version"].Type);
    }
}
