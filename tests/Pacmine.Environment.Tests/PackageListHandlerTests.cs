using Xunit;
using Pacmine.Core;
using Pacmine.Environment.Indexing;

namespace Pacmine.Environment.Tests;

public class PackageListHandlerTests : IndexHandlerTestBase
{
    [Fact]
    public void Constructor_ContentIsEmpty()
    {
        var handler = new PackageListHandler(IndexDirectory);
        Assert.Empty(handler.Content);
    }

    [Fact]
    public void OnLoad_FileExists_LoadsContent()
    {
        // Arrange
        var dict = new Dictionary<string, VersionIdentifier>
        {
            { "pkg-a", new VersionIdentifier("1.0.0") },
            { "pkg-b", new VersionIdentifier("2.0.0") }
        };
        File.WriteAllText(
            Path.Combine(IndexDirectory.FullName, PackageListHandler.FILE_NAME),
            System.Text.Json.JsonSerializer.Serialize(dict));

        var handler = new PackageListHandler(IndexDirectory);

        // Act
        handler.OnLoad();

        // Assert
        Assert.Equal(2, handler.Content.Count);
        Assert.Equal("1.0.0", handler.Content["pkg-a"].RawString);
        Assert.Equal("2.0.0", handler.Content["pkg-b"].RawString);
    }

    [Fact]
    public void OnLoad_FileMissing_ContentStaysEmpty()
    {
        var handler = new PackageListHandler(IndexDirectory);
        handler.OnLoad();
        Assert.Empty(handler.Content);
    }

    [Fact]
    public void OnLoad_CorruptFile_ContentStaysEmpty()
    {
        File.WriteAllText(
            Path.Combine(IndexDirectory.FullName, PackageListHandler.FILE_NAME),
            "not valid json");

        var handler = new PackageListHandler(IndexDirectory);
        handler.OnLoad();
        Assert.Empty(handler.Content);
    }

    [Fact]
    public void OnRebuild_BuildsCorrectPackageList()
    {
        var handler = new PackageListHandler(IndexDirectory);
        var registries = new[]
        {
            CreateRegistry("pkg-a", "1.0.0"),
            CreateRegistry("pkg-b", "2.0.0")
        };

        var altered = handler.OnRebuild(registries);

        Assert.True(altered);
        Assert.Equal(2, handler.Content.Count);
        Assert.Equal("1.0.0", handler.Content["pkg-a"].RawString);
        Assert.Equal("2.0.0", handler.Content["pkg-b"].RawString);
    }

    [Fact]
    public void OnRebuild_NoChange_ReturnsFalse()
    {
        var handler = new PackageListHandler(IndexDirectory);
        var registries = new[] { CreateRegistry("pkg-a", "1.0.0") };
        handler.OnRebuild(registries);

        // Rebuild with same data
        var altered = handler.OnRebuild(registries);
        Assert.False(altered);
    }

    [Fact]
    public void OnWriteRegistry_AddsPackageEntry()
    {
        var handler = new PackageListHandler(IndexDirectory);
        var registry = CreateRegistry("new-pkg", "1.0.0");

        handler.OnWriteRegistry(registry);

        Assert.Single(handler.Content);
        Assert.Equal("1.0.0", handler.Content["new-pkg"].RawString);
    }

    [Fact]
    public void OnWriteRegistry_UpdatesExistingEntry()
    {
        var handler = new PackageListHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "1.0.0"));
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "2.0.0"));

        Assert.Single(handler.Content);
        Assert.Equal("2.0.0", handler.Content["pkg-a"].RawString);
    }

    [Fact]
    public void OnRemoveRegistry_RemovesPackageEntry()
    {
        var handler = new PackageListHandler(IndexDirectory);
        handler.OnWriteRegistry(CreateRegistry("pkg-a", "1.0.0"));
        handler.OnWriteRegistry(CreateRegistry("pkg-b", "2.0.0"));

        handler.OnRemoveRegistry(CreateRegistry("pkg-a", "1.0.0"));

        Assert.Single(handler.Content);
        Assert.True(handler.Content.ContainsKey("pkg-b"));
        Assert.False(handler.Content.ContainsKey("pkg-a"));
    }

    [Fact]
    public void ContentSetter_PersistsToDisk()
    {
        var handler = new PackageListHandler(IndexDirectory);
        handler.Content = new Dictionary<string, VersionIdentifier>
        {
            { "persisted", new VersionIdentifier("1.0.0") }
        };

        // Create a new handler and load from disk
        var loader = new PackageListHandler(IndexDirectory);
        loader.OnLoad();

        Assert.Single(loader.Content);
        Assert.Equal("1.0.0", loader.Content["persisted"].RawString);
    }
}
