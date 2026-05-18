using Xunit;
using Pacmine.Environment.Indexing;

namespace Pacmine.Environment.Tests;

public class ManagedFileListHandlerTests : IndexHandlerTestBase
{
    [Fact]
    public void Constructor_ContentIsEmpty()
    {
        var handler = new ManagedFileListHandler(IndexDirectory);
        Assert.Empty(handler.Content);
    }

    [Fact]
    public void OnRebuild_BuildsFileToOwnerMapping()
    {
        var handler = new ManagedFileListHandler(IndexDirectory);
        var registries = new[]
        {
            CreateRegistry("pkg-a", "1.0.0",
                fileList: new() { { "mods/foo.jar", "abc123" }, { "config/foo.cfg", "def456" } }),
            CreateRegistry("pkg-b", "2.0.0",
                fileList: new() { { "mods/bar.jar", "789abc" } })
        };

        handler.OnRebuild(registries);

        Assert.Equal(3, handler.Content.Count);
        Assert.Equal("pkg-a", handler.Content["mods/foo.jar"].Owner);
        Assert.Equal("abc123", handler.Content["mods/foo.jar"].SHA256);
        Assert.Equal("pkg-a", handler.Content["config/foo.cfg"].Owner);
        Assert.Equal("pkg-b", handler.Content["mods/bar.jar"].Owner);
    }

    [Fact]
    public void OnWriteRegistry_AddsFileEntries()
    {
        var handler = new ManagedFileListHandler(IndexDirectory);
        var registry = CreateRegistry("pkg-a", "1.0.0",
            fileList: new() { { "mods/foo.jar", "abc123" } });

        handler.OnWriteRegistry(registry);

        Assert.Single(handler.Content);
        Assert.Equal("pkg-a", handler.Content["mods/foo.jar"].Owner);
    }

    [Fact]
    public void OnWriteRegistry_RemovesStaleFiles()
    {
        var handler = new ManagedFileListHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "1.0.0",
            fileList: new() { { "mods/old.jar", "abc123" } }));

        // Rewrite with new files — old files should be removed
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "2.0.0",
            fileList: new() { { "mods/new.jar", "def456" } }));

        Assert.Single(handler.Content);
        Assert.True(handler.Content.ContainsKey("mods/new.jar"));
        Assert.False(handler.Content.ContainsKey("mods/old.jar"));
    }

    [Fact]
    public void OnWriteRegistry_DifferentPackageSameFile_LastWriteWins()
    {
        var handler = new ManagedFileListHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "1.0.0",
            fileList: new() { { "shared/file.txt", "aaa" } }));
        handler.OnWriteRegistry(CreateRegistry("pkg-b", "1.0.0",
            fileList: new() { { "shared/file.txt", "bbb" } }));

        Assert.Equal("pkg-b", handler.Content["shared/file.txt"].Owner);
        Assert.Equal("bbb", handler.Content["shared/file.txt"].SHA256);
    }

    [Fact]
    public void OnRemoveRegistry_RemovesAllPackageFiles()
    {
        var handler = new ManagedFileListHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "1.0.0",
            fileList: new() { { "mods/a.jar", "1" }, { "config/a.cfg", "2" } }));
        handler.OnWriteRegistry(CreateRegistry("pkg-b", "1.0.0",
            fileList: new() { { "mods/b.jar", "3" } }));

        handler.OnRemoveRegistry(CreateRegistry("pkg-a", "1.0.0"));

        Assert.Single(handler.Content);
        Assert.True(handler.Content.ContainsKey("mods/b.jar"));
        Assert.False(handler.Content.ContainsKey("mods/a.jar"));
    }

    [Fact]
    public void ContentSetter_PersistsToDisk()
    {
        var handler = new ManagedFileListHandler(IndexDirectory);
        handler.Content = new Dictionary<string, ManagedFileRecord>
        {
            ["mods/persisted.jar"] = new ManagedFileRecord("pkg-a", "checksum123")
        };

        var loader = new ManagedFileListHandler(IndexDirectory);
        loader.OnLoad();

        Assert.Single(loader.Content);
        Assert.Equal("pkg-a", loader.Content["mods/persisted.jar"].Owner);
        Assert.Equal("checksum123", loader.Content["mods/persisted.jar"].SHA256);
    }
}
