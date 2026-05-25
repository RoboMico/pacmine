using Xunit;
using Pacmine.Core;
using Pacmine.TestUtils;

namespace Pacmine.Environment.Tests;

public class PacmineEnvironmentTests : IDisposable
{
    private readonly TempDirectory _tempDir;

    public PacmineEnvironmentTests()
    {
        _tempDir = new TempDirectory();
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    // ── Create ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_CreatesDirectoryStructure()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);

        Assert.True(Directory.Exists(_tempDir.Path));
        Assert.True(Directory.Exists(Path.Combine(_tempDir.Path, PacmineEnvironment.SPECIAL_FOLDER_NAME)));
        Assert.True(Directory.Exists(Path.Combine(_tempDir.Path, PacmineEnvironment.SPECIAL_FOLDER_NAME, PacmineEnvironment.REGISTRY_FOLDER_NAME)));
        Assert.True(File.Exists(Path.Combine(_tempDir.Path, PacmineEnvironment.SPECIAL_FOLDER_NAME, PacmineEnvironment.LOCKFILE_NAME)));
        Assert.True(File.Exists(Path.Combine(_tempDir.Path, PacmineEnvironment.SPECIAL_FOLDER_NAME, PacmineEnvironment.PACKAGE_LIST_FILE_NAME)));
    }

    [Fact]
    public void Create_ThrowsWhenAlreadyExists()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        Assert.Throws<Exception>(() => { PacmineEnvironment.Create(_tempDir.Path); });
    }

    // ── Access ───────────────────────────────────────────────────────────

    [Fact]
    public void Access_OpensExistingEnvironment()
    {
        PacmineEnvironment.Create(_tempDir.Path).Dispose();
        using var env = PacmineEnvironment.Access(_tempDir.Path);
        Assert.NotNull(env);
        Assert.Equal(_tempDir.Path, env.RootPath);
    }

    [Fact]
    public void Access_ThrowsOnMissingEnvironment()
    {
        Assert.Throws<Exception>(() => { PacmineEnvironment.Access(Path.Combine(_tempDir.Path, "nonexistent")); });
    }

    // ── TryWriteRegistry / PackageRegistry dict ───────────────────────────

    [Fact]
    public void TryWriteRegistry_WritesToDiskAndMemory()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        var registry = CreateTestRegistry("test-pkg", "1.0.0");

        Assert.True(env.TryWriteRegistry(registry));

        // Verify in-memory dictionary
        Assert.True(env.PackageRegistry.ContainsKey("test-pkg"));
        Assert.Equal("1.0.0", env.PackageRegistry["test-pkg"].Meta.Version.RawString);

        // Verify on-disk file
        var expectedPath = Path.Combine(
            env.RegistryFolder.FullName, "t", "test-pkg.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public void TryWriteRegistry_UpdatesExistingEntry()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("test-pkg", "1.0.0"));
        env.TryWriteRegistry(CreateTestRegistry("test-pkg", "2.0.0"));

        Assert.Equal("2.0.0", env.PackageRegistry["test-pkg"].Meta.Version.RawString);
    }

    // ── TryRemoveRegistry ────────────────────────────────────────────────

    [Fact]
    public void TryRemoveRegistry_RemovesFromDiskAndMemory()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("test-pkg", "1.0.0"));

        Assert.True(env.TryRemoveRegistry("test-pkg"));
        Assert.False(env.PackageRegistry.ContainsKey("test-pkg"));

        var expectedPath = Path.Combine(
            env.RegistryFolder.FullName, "t", "test-pkg.json");
        Assert.False(File.Exists(expectedPath));
    }

    [Fact]
    public void TryRemoveRegistry_NonExistentPackage_ReturnsFalse()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        Assert.False(env.TryRemoveRegistry("nonexistent"));
    }

    // ── Scan ──────────────────────────────────────────────────────────────

    [Fact]
    public void Scan_LoadsRegistryFromDisk()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("pkg-a", "1.0.0"));
        env.TryWriteRegistry(CreateTestRegistry("pkg-b", "2.0.0"));

        // Clear in-memory and reload
        env.PackageRegistry.Clear();
        env.Scan();

        Assert.Equal(2, env.PackageRegistry.Count);
        Assert.True(env.PackageRegistry.ContainsKey("pkg-a"));
        Assert.True(env.PackageRegistry.ContainsKey("pkg-b"));
    }

    [Fact]
    public void Scan_RewritesPackageList()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("pkg-x", "1.0.0"));

        // Simulate a new env instance scanning
        env.PackageRegistry.Clear();
        env.Scan();

        Assert.Single(env.PackageRegistry);
        Assert.True(env.PackageRegistry.ContainsKey("pkg-x"));
        Assert.Equal("1.0.0", env.PackageRegistry["pkg-x"].Meta.Version.RawString);

        // Verify package_list was rewritten to disk
        var lines = File.ReadAllLines(env.PackageListFile.FullName);
        Assert.Contains("pkg-x", lines);
    }

    // ── CheckConflictFiles ───────────────────────────────────────────────

    [Fact]
    public void CheckConflictFiles_FileManagedByOtherPackage_ReturnsConflict()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("existing", "1.0.0",
            fileList: new() { { "mods/foo.jar", "abc123" } }));

        var conflicts = env.CheckConflictFiles(["mods/foo.jar"], []);

        Assert.NotEmpty(conflicts);
        Assert.Equal("existing", conflicts["mods/foo.jar"]);
    }

    [Fact]
    public void CheckConflictFiles_FileManagedByIgnoredPackage_ReturnsNoConflict()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("existing", "1.0.0",
            fileList: new() { { "mods/foo.jar", "abc123" } }));

        var conflicts = env.CheckConflictFiles(["mods/foo.jar"], ["existing"]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void CheckConflictFiles_OrphanFileOnDisk_ReturnsConflictWithEmptyOwner()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        var orphanPath = Path.Combine(_tempDir.Path, "orphan.txt");
        File.WriteAllText(orphanPath, "I'm an orphan!");

        var conflicts = env.CheckConflictFiles(["orphan.txt"], []);

        Assert.NotEmpty(conflicts);
        Assert.Equal(string.Empty, conflicts["orphan.txt"]);
    }

    [Fact]
    public void CheckConflictFiles_NoConflicts_ReturnsEmpty()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);

        var conflicts = env.CheckConflictFiles(["mods/foo.jar"], []);

        Assert.Empty(conflicts);
    }

    // ── UpdateFiles ──────────────────────────────────────────────────────

    [Fact]
    public void UpdateFiles_CopiesFilesAndReturnsHashList()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("owner-pkg", "1.0.0"));

        // Create a source directory with files
        var srcDir = Path.Combine(_tempDir.Path, "source");
        var modsDir = Path.Combine(srcDir, "mods");
        Directory.CreateDirectory(modsDir);
        File.WriteAllText(Path.Combine(modsDir, "mod.jar"), "mod content");

        var fileList = env.UpdateFiles("owner-pkg", new DirectoryInfo(srcDir));

        Assert.NotEmpty(fileList);
        Assert.True(fileList.ContainsKey(Path.Combine("mods", "mod.jar")));
        Assert.True(File.Exists(Path.Combine(_tempDir.Path, "mods", "mod.jar")));
    }

    [Fact]
    public void UpdateFiles_PrunesStaleFiles()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);

        // First: install files
        env.TryWriteRegistry(CreateTestRegistry("owner-pkg", "1.0.0"));
        var srcDir1 = Path.Combine(_tempDir.Path, "source1");
        Directory.CreateDirectory(Path.Combine(srcDir1, "mods"));
        File.WriteAllText(Path.Combine(srcDir1, "mods", "old.jar"), "old");
        env.UpdateFiles("owner-pkg", new DirectoryInfo(srcDir1));

        Assert.True(File.Exists(Path.Combine(_tempDir.Path, "mods", "old.jar")));

        // Write the registry so the old file list is persisted
        var reg = env.PackageRegistry["owner-pkg"];
        reg.FileList = new() { { Path.Combine("mods", "old.jar"), "dummy" } };
        env.TryWriteRegistry(reg);

        // Second: update with new files only
        var srcDir2 = Path.Combine(_tempDir.Path, "source2");
        Directory.CreateDirectory(Path.Combine(srcDir2, "mods"));
        File.WriteAllText(Path.Combine(srcDir2, "mods", "new.jar"), "new");
        env.UpdateFiles("owner-pkg", new DirectoryInfo(srcDir2));

        Assert.False(File.Exists(Path.Combine(_tempDir.Path, "mods", "old.jar")));
        Assert.True(File.Exists(Path.Combine(_tempDir.Path, "mods", "new.jar")));
    }

    // ── RemovePackageFiles ───────────────────────────────────────────────

    [Fact]
    public void RemovePackageFiles_DeletesOwnedFiles()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("owner-pkg", "1.0.0"));

        // Create a source directory and install files
        var srcDir = Path.Combine(_tempDir.Path, "source");
        Directory.CreateDirectory(Path.Combine(srcDir, "mods"));
        File.WriteAllText(Path.Combine(srcDir, "mods", "mod.jar"), "content");
        env.UpdateFiles("owner-pkg", new DirectoryInfo(srcDir));

        // Persist the file list in registry
        var reg = env.PackageRegistry["owner-pkg"];
        reg.FileList = new() { { Path.Combine("mods", "mod.jar"), "dummy" } };
        env.TryWriteRegistry(reg);

        Assert.True(File.Exists(Path.Combine(_tempDir.Path, "mods", "mod.jar")));

        env.RemovePackageFiles("owner-pkg");

        Assert.False(File.Exists(Path.Combine(_tempDir.Path, "mods", "mod.jar")));
    }

    [Fact]
    public void RemovePackageFiles_NonExistentPackage_DoesNotThrow()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        var exception = Record.Exception(() => env.RemovePackageFiles("nonexistent"));
        Assert.Null(exception);
    }

    // ── Destroy ──────────────────────────────────────────────────────────

    [Fact]
    public void Destroy_RemovesSpecialFolder()
    {
        var env = PacmineEnvironment.Create(_tempDir.Path);
        var specialFolder = Path.Combine(_tempDir.Path, PacmineEnvironment.SPECIAL_FOLDER_NAME);
        Assert.True(Directory.Exists(specialFolder));

        env.Destroy();

        Assert.False(Directory.Exists(specialFolder));
    }

    // ── GetLockerPid ─────────────────────────────────────────────────────

    [Fact]
    public void GetLockerPid_NoLock_ReturnsNegative()
    {
        var pid = PacmineEnvironment.GetLockerPid(_tempDir.Path);
        Assert.Equal(-1, pid);
    }

    [Fact]
    public void GetLockerPid_ActiveLock_ReturnsValidPid()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        var pid = PacmineEnvironment.GetLockerPid(_tempDir.Path);
        Assert.True(pid > 0);
    }

    // ── PackageRegistry dict access ─────────────────────────────────────

    [Fact]
    public void PackageRegistry_InitiallyEmpty()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        Assert.Empty(env.PackageRegistry);
    }

    [Fact]
    public void PackageRegistry_PopulatedAfterAccess()
    {
        // Write packages, dispose, then Access — packages should be reloaded from disk
        using (var env = PacmineEnvironment.Create(_tempDir.Path))
        {
            env.TryWriteRegistry(CreateTestRegistry("pkg1", "1.0.0"));
            env.TryWriteRegistry(CreateTestRegistry("pkg2", "2.0.0"));
        }

        using var env2 = PacmineEnvironment.Access(_tempDir.Path);
        Assert.Equal(2, env2.PackageRegistry.Count);
        Assert.True(env2.PackageRegistry.ContainsKey("pkg1"));
        Assert.True(env2.PackageRegistry.ContainsKey("pkg2"));
        Assert.Equal("1.0.0", env2.PackageRegistry["pkg1"].Meta.Version.RawString);
        Assert.Equal("2.0.0", env2.PackageRegistry["pkg2"].Meta.Version.RawString);
    }

    [Fact]
    public void PackageRegistry_ClearedThenScanned_Repopulates()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.TryWriteRegistry(CreateTestRegistry("pkg-repop", "1.0.0"));

        env.PackageRegistry.Clear();
        Assert.Empty(env.PackageRegistry);

        env.Scan();
        Assert.Single(env.PackageRegistry);
        Assert.True(env.PackageRegistry.ContainsKey("pkg-repop"));
    }

    // ── Dispose releases lock ─────────────────────────────────────────────

    [Fact]
    public void Dispose_ReleasesLock()
    {
        var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Dispose();

        // Lock file should be deleted after dispose
        var lockFile = Path.Combine(
            _tempDir.Path, PacmineEnvironment.SPECIAL_FOLDER_NAME, PacmineEnvironment.LOCKFILE_NAME);
        Assert.False(File.Exists(lockFile));
    }

    // ── UpdateFiles source dir validation ────────────────────────────────

    [Fact]
    public void UpdateFiles_MissingSourceDir_ThrowsDirectoryNotFoundException()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        Assert.Throws<DirectoryNotFoundException>(() =>
        {
            env.UpdateFiles("owner", new DirectoryInfo(Path.Combine(_tempDir.Path, "nonexistent")));
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static PackageRegistry CreateTestRegistry(
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
