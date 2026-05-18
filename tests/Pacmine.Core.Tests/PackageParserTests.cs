using Xunit;
using System.IO.Compression;
using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Core.Tests;

public class PackageParserTests : IDisposable
{
    private readonly string _tempDir;

    public PackageParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PacmineTest_" + Guid.NewGuid().ToString()[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── GetMeta ──────────────────────────────────────────────────────────

    [Fact]
    public void GetMeta_ValidMetaFile_ReturnsPackageMeta()
    {
        // Arrange
        string zipPath = CreateZipWithMeta(new PackageMeta
        {
            Name = "test-package",
            Description = "A test package",
            UpstreamUrl = "https://example.com",
            Category = "mod",
            License = "MIT",
            Version = new VersionIdentifier("1.2.3")
        });

        using var archive = ZipFile.OpenRead(zipPath);

        // Act
        var meta = PackageParser.GetMeta(archive);

        // Assert
        Assert.NotNull(meta);
        Assert.Equal("test-package", meta.Name);
        Assert.Equal("A test package", meta.Description);
        Assert.Equal("1.2.3", meta.Version.RawString);
    }

    [Fact]
    public void GetMeta_MissingMetaFile_ReturnsNull()
    {
        // Arrange
        string zipPath = Path.Combine(_tempDir, "empty.zip");
        using (var emptyArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            // no entries
        }

        using var archive = ZipFile.OpenRead(zipPath);

        // Act
        var meta = PackageParser.GetMeta(archive);

        // Assert
        Assert.Null(meta);
    }

    [Fact]
    public void GetMeta_CorruptMetaFile_ThrowsJsonException()
    {
        // Arrange
        string zipPath = Path.Combine(_tempDir, "corrupt.zip");
        using (var zipStream = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zipStream.CreateEntry(PackageParser.META_FILE_NAME);
            using var streamWriter = new StreamWriter(entry.Open());
            streamWriter.Write("this is not valid json");
        }

        using var archive = ZipFile.OpenRead(zipPath);

        // Act & Assert
        // PackageParser.GetMeta does not catch JSON exceptions internally,
        // so corrupt JSON propagates as a JsonException
        Assert.Throws<System.Text.Json.JsonException>(() => PackageParser.GetMeta(archive));
    }

    // ── GetPackagedTime ──────────────────────────────────────────────────

    [Fact]
    public void GetPackagedTime_MetaFileExists_ReturnsDateTime()
    {
        // Arrange
        string zipPath = CreateZipWithMeta(new PackageMeta
        {
            Name = "test-pkg",
            Description = "test",
            UpstreamUrl = "https://example.com",
            Category = "mod",
            License = "MIT",
            Version = new VersionIdentifier("1.0.0")
        });

        using var archive = ZipFile.OpenRead(zipPath);

        // Act
        var time = PackageParser.GetPackagedTime(archive);

        // Assert
        Assert.NotNull(time);
        Assert.IsType<DateTime>(time);
        Assert.True(time.Value.Year >= 2020); // sanity check
    }

    [Fact]
    public void GetPackagedTime_MissingMetaFile_ReturnsNull()
    {
        // Arrange
        string zipPath = Path.Combine(_tempDir, "empty2.zip");
        using (var emptyArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create)) { }

        using var archive = ZipFile.OpenRead(zipPath);

        // Act
        var time = PackageParser.GetPackagedTime(archive);

        // Assert
        Assert.Null(time);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private string CreateZipWithMeta(PackageMeta meta)
    {
        string zipPath = Path.Combine(_tempDir, "package.pacminepack.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(PackageParser.META_FILE_NAME);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(JsonSerializer.Serialize(meta));
        return zipPath;
    }
}
