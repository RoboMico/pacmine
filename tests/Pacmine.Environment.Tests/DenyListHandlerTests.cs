using Xunit;
using Pacmine.Core;
using Pacmine.Environment.Indexing;

namespace Pacmine.Environment.Tests;

public class DenyListHandlerTests : IndexHandlerTestBase
{
    [Fact]
    public void Constructor_ContentIsEmpty()
    {
        var handler = new DenyListHandler(IndexDirectory);
        Assert.Empty(handler.Content);
    }

    [Fact]
    public void OnRebuild_BuildsConflictIndex()
    {
        var handler = new DenyListHandler(IndexDirectory);
        var registries = new[]
        {
            CreateRegistry("pkg-a", "1.0.0",
                conflicts: new() { { "pkg-b", new VersionRange("^1.0.0") } }),
            CreateRegistry("pkg-c", "2.0.0",
                conflicts: new() { { "pkg-d", new VersionRange("^2.0.0") } })
        };

        handler.OnRebuild(registries);

        Assert.Equal(2, handler.Content.Count);
        Assert.True(handler.Content.ContainsKey("pkg-a"));
        Assert.True(handler.Content.ContainsKey("pkg-c"));
        Assert.True(handler.Content["pkg-a"]["pkg-b"].Contains(new VersionIdentifier("1.5.0")));
    }

    [Fact]
    public void OnRebuild_PackageWithoutConflicts_NotInDenyList()
    {
        var handler = new DenyListHandler(IndexDirectory);
        var registries = new[]
        {
            CreateRegistry("peaceful-pkg", "1.0.0") // no conflicts
        };

        handler.OnRebuild(registries);

        Assert.Empty(handler.Content);
    }

    [Fact]
    public void OnWriteRegistry_AddsConflictEntry()
    {
        var handler = new DenyListHandler(IndexDirectory);
        var registry = CreateRegistry("pkg-a", "1.0.0",
            conflicts: new() { { "pkg-b", new VersionRange("^1.0.0") } });

        handler.OnWriteRegistry(registry);

        Assert.Single(handler.Content);
        Assert.True(handler.Content.ContainsKey("pkg-a"));
    }

    [Fact]
    public void OnWriteRegistry_RemovesEntryWhenNoConflicts()
    {
        var handler = new DenyListHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "1.0.0",
            conflicts: new() { { "pkg-b", new VersionRange("^1.0.0") } }));

        // Re-write with no conflicts
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "2.0.0"));

        Assert.False(handler.Content.ContainsKey("pkg-a"));
    }

    [Fact]
    public void OnRemoveRegistry_RemovesConflictEntry()
    {
        var handler = new DenyListHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "1.0.0",
            conflicts: new() { { "pkg-b", new VersionRange("^1.0.0") } }));

        handler.OnRemoveRegistry(CreateRegistry("pkg-a", "1.0.0",
            conflicts: new() { { "pkg-b", new VersionRange("^1.0.0") } }));

        Assert.False(handler.Content.ContainsKey("pkg-a"));
    }

    [Fact]
    public void ContentSetter_PersistsToDisk()
    {
        var handler = new DenyListHandler(IndexDirectory);
        handler.Content = new Dictionary<string, Dictionary<string, VersionRange>>
        {
            ["pkg-a"] = new() { { "pkg-b", new VersionRange("^1.0.0") } }
        };

        var loader = new DenyListHandler(IndexDirectory);
        loader.OnLoad();

        Assert.Single(loader.Content);
        Assert.True(loader.Content.ContainsKey("pkg-a"));
    }
}
