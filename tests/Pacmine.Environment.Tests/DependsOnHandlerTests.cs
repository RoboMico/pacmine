using Xunit;
using Pacmine.Core;
using Pacmine.Environment.Indexing;

namespace Pacmine.Environment.Tests;

public class DependsOnHandlerTests : IndexHandlerTestBase
{
    [Fact]
    public void Constructor_ContentIsEmpty()
    {
        var handler = new DependsOnHandler(IndexDirectory);
        Assert.Empty(handler.Content);
    }

    [Fact]
    public void OnRebuild_BuildsReverseDependencyMap()
    {
        var handler = new DependsOnHandler(IndexDirectory);
        var registries = new[]
        {
            CreateRegistry("dependent-a", "1.0.0",
                depends: new() { { "dependency-x", new VersionRange("^1.0.0") } }),
            CreateRegistry("dependent-b", "1.0.0",
                depends: new() { { "dependency-x", new VersionRange("^1.0.0") } })
        };

        handler.OnRebuild(registries);

        Assert.True(handler.Content.ContainsKey("dependency-x"));
        Assert.Equal(2, handler.Content["dependency-x"].Count);
        Assert.Contains("dependent-a", handler.Content["dependency-x"]);
        Assert.Contains("dependent-b", handler.Content["dependency-x"]);
    }

    [Fact]
    public void OnRebuild_MultipleDependenciesPerPackage()
    {
        var handler = new DependsOnHandler(IndexDirectory);
        var registries = new[]
        {
            CreateRegistry("dependent", "1.0.0",
                depends: new()
                {
                    { "dep-a", new VersionRange("^1.0.0") },
                    { "dep-b", new VersionRange("^2.0.0") }
                })
        };

        handler.OnRebuild(registries);

        Assert.Contains("dep-a", handler.Content.Keys);
        Assert.Contains("dep-b", handler.Content.Keys);
        Assert.Equal("dependent", handler.Content["dep-a"].Single());
        Assert.Equal("dependent", handler.Content["dep-b"].Single());
    }

    [Fact]
    public void OnWriteRegistry_AddsDependencyEntries()
    {
        var handler = new DependsOnHandler(IndexDirectory);
        var registry = CreateRegistry("dependent", "1.0.0",
            depends: new() { { "dep-x", new VersionRange("^1.0.0") } });

        handler.OnWriteRegistry(registry);

        Assert.True(handler.Content.ContainsKey("dep-x"));
        Assert.Contains("dependent", handler.Content["dep-x"]);
    }

    [Fact]
    public void OnWriteRegistry_ReplacesPreviousEntries()
    {
        var handler = new DependsOnHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("dependent", "1.0.0",
            depends: new() { { "dep-old", new VersionRange("^1.0.0") } }));
        handler.OnWriteRegistry(CreateRegistry("dependent", "2.0.0",
            depends: new() { { "dep-new", new VersionRange("^2.0.0") } }));

        Assert.False(handler.Content.ContainsKey("dep-old"));
        Assert.True(handler.Content.ContainsKey("dep-new"));
    }

    [Fact]
    public void OnRemoveRegistry_RemovesDependentFromAllLists()
    {
        var handler = new DependsOnHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("dependent", "1.0.0",
            depends: new() { { "dep-x", new VersionRange("^1.0.0") } }));

        handler.OnRemoveRegistry(CreateRegistry("dependent", "1.0.0",
            depends: new() { { "dep-x", new VersionRange("^1.0.0") } }));

        Assert.False(handler.Content.ContainsKey("dep-x"));
    }

    [Fact]
    public void OnRemoveRegistry_MultipleDependents_OnlyRemovesOne()
    {
        var handler = new DependsOnHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("dep-a", "1.0.0",
            depends: new() { { "shared-dep", new VersionRange("^1.0.0") } }));
        handler.OnWriteRegistry(CreateRegistry("dep-b", "1.0.0",
            depends: new() { { "shared-dep", new VersionRange("^1.0.0") } }));

        handler.OnRemoveRegistry(CreateRegistry("dep-a", "1.0.0",
            depends: new() { { "shared-dep", new VersionRange("^1.0.0") } }));

        Assert.True(handler.Content.ContainsKey("shared-dep"));
        Assert.Single(handler.Content["shared-dep"]);
        Assert.Contains("dep-b", handler.Content["shared-dep"]);
    }

    [Fact]
    public void OnWriteRegistry_PersistsToDisk()
    {
        var handler = new DependsOnHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("dependent-a", "1.0.0",
            depends: new() { { "dep-x", new VersionRange("^1.0.0") } }));

        var loader = new DependsOnHandler(IndexDirectory);
        loader.OnLoad();

        Assert.True(loader.Content.ContainsKey("dep-x"));
    }
}
