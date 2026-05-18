using Xunit;
using Pacmine.Core;

namespace Pacmine.Environment.Tests;

/// <summary>
/// Base class for IndexHandler tests that provides temporary directory management.
/// Each test gets a fresh temp directory, automatically cleaned up after the test.
/// </summary>
public abstract class IndexHandlerTestBase : IDisposable
{
    private readonly string _tempRoot;

    protected IndexHandlerTestBase()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PacmineEnvTest_" + Guid.NewGuid().ToString()[..8]);
        IndexDirectory = new DirectoryInfo(Path.Combine(_tempRoot, "index"));
        RegistryDirectory = new DirectoryInfo(Path.Combine(_tempRoot, "registry"));
        IndexDirectory.Create();
        RegistryDirectory.Create();
    }

    protected DirectoryInfo IndexDirectory { get; }
    protected DirectoryInfo RegistryDirectory { get; }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    /// <summary>
    /// Creates a minimal PackageRegistry for testing.
    /// </summary>
    protected static PackageRegistry CreateRegistry(
        string name,
        string version,
        Dictionary<string, VersionRange>? depends = null,
        Dictionary<string, VersionRange>? conflicts = null,
        Dictionary<string, VersionIdentifier>? provides = null,
        Dictionary<string, string>? fileList = null)
    {
        return new PackageRegistry
        {
            Meta = new Core.PackageMeta
            {
                Name = name,
                Description = $"Test package {name}",
                UpstreamUrl = "https://example.com",
                Category = "mod",
                License = "MIT",
                Version = new Core.VersionIdentifier(version),
                Depends = depends ?? [],
                Conflicts = conflicts ?? [],
                Provides = provides ?? []
            },
            FileList = fileList ?? [],
            InstallReason = InstallReasons.Explicit,
            InstalledTime = DateTime.Now
        };
    }
}
