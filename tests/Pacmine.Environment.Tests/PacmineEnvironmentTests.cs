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

    // ── RegistryStore.Write ──────────────────────────────────────────────

    [Fact]
    public void RegistryWrite_WritesToDiskAndMemory()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        var registry = CreateTestRegistry("test-pkg", "1.0.0");

        Assert.True(env.Registry.Write(registry));

        // Verify in-memory
        Assert.True(env.Registry.Contains("test-pkg"));
        Assert.Equal("1.0.0", env.Registry.TryGet("test-pkg")!.Meta.Version.RawString);

        // Verify on-disk file
        var expectedPath = Path.Combine(
            env.RegistryFolder.FullName, "t", "test-pkg.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public void RegistryWrite_UpdatesExistingEntry()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("test-pkg", "1.0.0"));
        env.Registry.Write(CreateTestRegistry("test-pkg", "2.0.0"));

        Assert.Equal("2.0.0", env.Registry.TryGet("test-pkg")!.Meta.Version.RawString);
    }

    // ── RegistryStore.Remove ─────────────────────────────────────────────

    [Fact]
    public void RegistryRemove_RemovesFromDiskAndMemory()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("test-pkg", "1.0.0"));

        Assert.True(env.Registry.Remove("test-pkg"));
        Assert.False(env.Registry.Contains("test-pkg"));

        var expectedPath = Path.Combine(
            env.RegistryFolder.FullName, "t", "test-pkg.json");
        Assert.False(File.Exists(expectedPath));
    }

    [Fact]
    public void RegistryRemove_NonExistentPackage_ReturnsFalse()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        Assert.False(env.Registry.Remove("nonexistent"));
    }

    // ── RegistryStore.Scan ───────────────────────────────────────────────

    [Fact]
    public void RegistryScan_LoadsRegistryFromDisk()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("pkg-a", "1.0.0"));
        env.Registry.Write(CreateTestRegistry("pkg-b", "2.0.0"));

        // Re-scan (simulates reloading from disk)
        env.Registry.Scan();

        Assert.Equal(2, env.Registry.Count);
        Assert.True(env.Registry.Contains("pkg-a"));
        Assert.True(env.Registry.Contains("pkg-b"));
    }

    [Fact]
    public void RegistryScan_RewritesPackageList()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("pkg-x", "1.0.0"));

        // Re-scan
        env.Registry.Scan();

        Assert.Equal(1, env.Registry.Count);
        Assert.True(env.Registry.Contains("pkg-x"));
        Assert.Equal("1.0.0", env.Registry.TryGet("pkg-x")!.Meta.Version.RawString);

        // Verify package_list was rewritten to disk
        var lines = File.ReadAllLines(env.PackageListFile.FullName);
        Assert.Contains("pkg-x", lines);
    }

    // ── FileManager.CheckConflictFiles ───────────────────────────────────

    [Fact]
    public void CheckConflictFiles_FileManagedByOtherPackage_ReturnsConflict()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("existing", "1.0.0",
            fileList: new() { { "mods/foo.jar", "abc123" } }));

        var managedFiles = BuildManagedFileMap(env.Registry.GetAll());
        var conflicts = FileManager.CheckConflictFiles(env.RootPath, ["mods/foo.jar"], managedFiles, []);

        Assert.NotEmpty(conflicts);
        Assert.Equal("existing", conflicts["mods/foo.jar"]);
    }

    [Fact]
    public void CheckConflictFiles_FileManagedByIgnoredPackage_ReturnsNoConflict()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("existing", "1.0.0",
            fileList: new() { { "mods/foo.jar", "abc123" } }));

        var managedFiles = BuildManagedFileMap(env.Registry.GetAll());
        var conflicts = FileManager.CheckConflictFiles(env.RootPath, ["mods/foo.jar"], managedFiles, ["existing"]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void CheckConflictFiles_OrphanFileOnDisk_ReturnsConflictWithEmptyOwner()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        var orphanPath = Path.Combine(_tempDir.Path, "orphan.txt");
        File.WriteAllText(orphanPath, "I'm an orphan!");

        var managedFiles = BuildManagedFileMap(env.Registry.GetAll());
        var conflicts = FileManager.CheckConflictFiles(env.RootPath, ["orphan.txt"], managedFiles, []);

        Assert.NotEmpty(conflicts);
        Assert.Equal(string.Empty, conflicts["orphan.txt"]);
    }

    [Fact]
    public void CheckConflictFiles_NoConflicts_ReturnsEmpty()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);

        var managedFiles = BuildManagedFileMap(env.Registry.GetAll());
        var conflicts = FileManager.CheckConflictFiles(env.RootPath, ["mods/foo.jar"], managedFiles, []);

        Assert.Empty(conflicts);
    }

    // ── FileManager.UpdateFiles ──────────────────────────────────────────

    [Fact]
    public void UpdateFiles_CopiesFilesAndReturnsHashList()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("owner-pkg", "1.0.0"));

        // Create a source directory with files
        var srcDir = Path.Combine(_tempDir.Path, "source");
        var modsDir = Path.Combine(srcDir, "mods");
        Directory.CreateDirectory(modsDir);
        File.WriteAllText(Path.Combine(modsDir, "mod.jar"), "mod content");

        var fileList = FileManager.UpdateFiles(env.RootPath, new DirectoryInfo(srcDir), []);

        Assert.NotEmpty(fileList);
        Assert.True(fileList.ContainsKey(Path.Combine("mods", "mod.jar")));
        Assert.True(File.Exists(Path.Combine(_tempDir.Path, "mods", "mod.jar")));
    }

    [Fact]
    public void UpdateFiles_PrunesStaleFiles()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);

        // First: install files
        env.Registry.Write(CreateTestRegistry("owner-pkg", "1.0.0"));
        var srcDir1 = Path.Combine(_tempDir.Path, "source1");
        Directory.CreateDirectory(Path.Combine(srcDir1, "mods"));
        File.WriteAllText(Path.Combine(srcDir1, "mods", "old.jar"), "old");
        FileManager.UpdateFiles(env.RootPath, new DirectoryInfo(srcDir1), []);

        Assert.True(File.Exists(Path.Combine(_tempDir.Path, "mods", "old.jar")));

        // Write the registry so the old file list is persisted
        var reg = env.Registry.TryGet("owner-pkg")!;
        reg.FileList = new() { { Path.Combine("mods", "old.jar"), "dummy" } };
        env.Registry.Write(reg);

        // Second: update with new files only
        var srcDir2 = Path.Combine(_tempDir.Path, "source2");
        Directory.CreateDirectory(Path.Combine(srcDir2, "mods"));
        File.WriteAllText(Path.Combine(srcDir2, "mods", "new.jar"), "new");
        FileManager.UpdateFiles(env.RootPath, new DirectoryInfo(srcDir2),
            new HashSet<string> { Path.Combine("mods", "old.jar") });

        Assert.False(File.Exists(Path.Combine(_tempDir.Path, "mods", "old.jar")));
        Assert.True(File.Exists(Path.Combine(_tempDir.Path, "mods", "new.jar")));
    }

    // ── FileManager.RemoveFiles ──────────────────────────────────────────

    [Fact]
    public void RemoveFiles_DeletesOwnedFiles()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("owner-pkg", "1.0.0"));

        // Create a source directory and install files
        var srcDir = Path.Combine(_tempDir.Path, "source");
        Directory.CreateDirectory(Path.Combine(srcDir, "mods"));
        File.WriteAllText(Path.Combine(srcDir, "mods", "mod.jar"), "content");
        FileManager.UpdateFiles(env.RootPath, new DirectoryInfo(srcDir), []);

        // Persist the file list in registry
        var reg = env.Registry.TryGet("owner-pkg")!;
        reg.FileList = new() { { Path.Combine("mods", "mod.jar"), "dummy" } };
        env.Registry.Write(reg);

        Assert.True(File.Exists(Path.Combine(_tempDir.Path, "mods", "mod.jar")));

        FileManager.RemoveFiles(env.RootPath, [Path.Combine("mods", "mod.jar")]);

        Assert.False(File.Exists(Path.Combine(_tempDir.Path, "mods", "mod.jar")));
    }

    [Fact]
    public void RemoveFiles_NonExistentPackage_DoesNotThrow()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        var exception = Record.Exception(() => FileManager.RemoveFiles(env.RootPath, []));
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
        var pid = EnvironmentLock.GetLockerPid(_tempDir.Path);
        Assert.Equal(-1, pid);
    }

    [Fact]
    public void GetLockerPid_ActiveLock_ReturnsValidPid()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        var pid = EnvironmentLock.GetLockerPid(_tempDir.Path);
        Assert.True(pid > 0);
    }

    // ── RegistryStore state ──────────────────────────────────────────────

    [Fact]
    public void Registry_InitiallyEmpty()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        Assert.Equal(0, env.Registry.Count);
    }

    [Fact]
    public void Registry_PopulatedAfterAccess()
    {
        // Write packages, dispose, then Access — packages should be reloaded from disk
        using (var env = PacmineEnvironment.Create(_tempDir.Path))
        {
            env.Registry.Write(CreateTestRegistry("pkg1", "1.0.0"));
            env.Registry.Write(CreateTestRegistry("pkg2", "2.0.0"));
        }

        using var env2 = PacmineEnvironment.Access(_tempDir.Path);
        Assert.Equal(2, env2.Registry.Count);
        Assert.True(env2.Registry.Contains("pkg1"));
        Assert.True(env2.Registry.Contains("pkg2"));
        Assert.Equal("1.0.0", env2.Registry.TryGet("pkg1")!.Meta.Version.RawString);
        Assert.Equal("2.0.0", env2.Registry.TryGet("pkg2")!.Meta.Version.RawString);
    }

    [Fact]
    public void Registry_Scanned_Repopulates()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        env.Registry.Write(CreateTestRegistry("pkg-repop", "1.0.0"));

        // Re-scan should still find the package
        env.Registry.Scan();
        Assert.Equal(1, env.Registry.Count);
        Assert.True(env.Registry.Contains("pkg-repop"));
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

    // ── FileManager.UpdateFiles source dir validation ────────────────────

    [Fact]
    public void UpdateFiles_MissingSourceDir_ThrowsDirectoryNotFoundException()
    {
        using var env = PacmineEnvironment.Create(_tempDir.Path);
        Assert.Throws<DirectoryNotFoundException>(() =>
        {
            FileManager.UpdateFiles(env.RootPath, new DirectoryInfo(Path.Combine(_tempDir.Path, "nonexistent")), []);
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

    private static Dictionary<string, string> BuildManagedFileMap(
        IReadOnlyDictionary<string, PackageRegistry> allRegistries)
    {
        var mngFiles = new Dictionary<string, string>();
        foreach (var (pkgName, pkgReg) in allRegistries)
        {
            foreach (var filePath in pkgReg.FileList.Keys)
            {
                mngFiles[filePath] = pkgName;
            }
        }
        return mngFiles;
    }
}
