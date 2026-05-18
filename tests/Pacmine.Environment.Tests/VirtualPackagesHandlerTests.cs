using Xunit;
using Pacmine.Core;
using Pacmine.Environment.Indexing;

namespace Pacmine.Environment.Tests;

public class VirtualPackagesHandlerTests : IndexHandlerTestBase
{
    [Fact]
    public void Constructor_ContentIsEmpty()
    {
        var handler = new VirtualPackagesHandler(IndexDirectory);
        Assert.Empty(handler.Content);
    }

    [Fact]
    public void OnRebuild_BuildsVirtualPackageIndex()
    {
        var handler = new VirtualPackagesHandler(IndexDirectory);
        var registries = new[]
        {
            CreateRegistry("provider-a", "1.0.0",
                provides: new() { { "virtual-foo", new VersionIdentifier("2.0.0") } }),
            CreateRegistry("provider-b", "1.0.0",
                provides: new() { { "virtual-foo", new VersionIdentifier("2.0.0") } })
        };

        handler.OnRebuild(registries);

        Assert.True(handler.Content.ContainsKey("virtual-foo"));
        Assert.True(handler.Content["virtual-foo"].ContainsKey(new VersionIdentifier("2.0.0")));
        Assert.Equal(2, handler.Content["virtual-foo"][new VersionIdentifier("2.0.0")].Count);
        Assert.Contains("provider-a", handler.Content["virtual-foo"][new VersionIdentifier("2.0.0")]);
        Assert.Contains("provider-b", handler.Content["virtual-foo"][new VersionIdentifier("2.0.0")]);
    }

    [Fact]
    public void OnWriteRegistry_AddsVirtualPackageEntries()
    {
        var handler = new VirtualPackagesHandler(IndexDirectory);
        var registry = CreateRegistry("provider", "1.0.0",
            provides: new() { { "virtual-foo", new VersionIdentifier("1.0.0") } });

        handler.OnWriteRegistry(registry);

        Assert.True(handler.Content.ContainsKey("virtual-foo"));
        Assert.Contains("provider", handler.Content["virtual-foo"][new VersionIdentifier("1.0.0")]);
    }

    [Fact]
    public void OnWriteRegistry_RemovesStaleProviderEntries()
    {
        var handler = new VirtualPackagesHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("provider", "1.0.0",
            provides: new() { { "virtual-foo", new VersionIdentifier("1.0.0") } }));

        // Re-write with no provides — should remove previous entries
        handler.OnWriteRegistry(CreateRegistry("provider", "2.0.0"));

        Assert.False(handler.Content.ContainsKey("virtual-foo"));
    }

    [Fact]
    public void OnRemoveRegistry_RemovesProviderEntries()
    {
        var handler = new VirtualPackagesHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("provider", "1.0.0",
            provides: new() { { "virtual-foo", new VersionIdentifier("1.0.0") } }));

        handler.OnRemoveRegistry(CreateRegistry("provider", "1.0.0",
            provides: new() { { "virtual-foo", new VersionIdentifier("1.0.0") } }));

        Assert.False(handler.Content.ContainsKey("virtual-foo"));
    }

    [Fact]
    public void OnRemoveRegistry_MultipleProviders_OnlyRemovesOne()
    {
        var handler = new VirtualPackagesHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("provider-a", "1.0.0",
            provides: new() { { "virtual-foo", new VersionIdentifier("1.0.0") } }));
        handler.OnWriteRegistry(CreateRegistry("provider-b", "1.0.0",
            provides: new() { { "virtual-foo", new VersionIdentifier("1.0.0") } }));

        handler.OnRemoveRegistry(CreateRegistry("provider-a", "1.0.0",
            provides: new() { { "virtual-foo", new VersionIdentifier("1.0.0") } }));

        Assert.True(handler.Content.ContainsKey("virtual-foo"));
        Assert.Single(handler.Content["virtual-foo"][new VersionIdentifier("1.0.0")]);
        Assert.Contains("provider-b", handler.Content["virtual-foo"][new VersionIdentifier("1.0.0")]);
    }

    [Fact]
    public void ContentSetter_PersistsToDisk()
    {
        var handler = new VirtualPackagesHandler(IndexDirectory);
        handler.Content = new Dictionary<string, Dictionary<VersionIdentifier, List<string>>>
        {
            ["virtual-foo"] = new()
            {
                [new VersionIdentifier("1.0.0")] = ["provider-a"]
            }
        };

        var loader = new VirtualPackagesHandler(IndexDirectory);
        loader.OnLoad();

        Assert.True(loader.Content.ContainsKey("virtual-foo"));
    }
}
