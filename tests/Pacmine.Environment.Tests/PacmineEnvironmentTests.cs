using Xunit;
using Pacmine.Core;
using Pacmine.Environment.Indexing;

namespace Pacmine.Environment.Tests;

public class PacmineEnvironmentTests : IDisposable
{
    private readonly string _tempDir;

    public PacmineEnvironmentTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PacmineEnv_Test_" + Guid.NewGuid().ToString()[..8]);
    }

    public void Dispose()
    {
        // Best-effort cleanup — may fail if locks are still held
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { }
    }

    // ── Create ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_CreatesDirectoryStructure()
    {
        using var env = PacmineEnvironment.Create(_tempDir);

        Assert.True(Directory.Exists(_tempDir));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, PacmineEnvironment.SPECIAL_FOLDER_NAME)));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, PacmineEnvironment.SPECIAL_FOLDER_NAME, PacmineEnvironment.REGISTRY_FOLDER_NAME)));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, PacmineEnvironment.SPECIAL_FOLDER_NAME, PacmineEnvironment.INDEX_FOLDER_NAME)));
    }

    [Fact]
    public void Create_ThrowsWhenAlreadyExists()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.Throws<Exception>(() => PacmineEnvironment.Create(_tempDir));
    }

    // ── Access ───────────────────────────────────────────────────────────

    [Fact]
    public void Access_OpensExistingEnvironment()
    {
        // Create first and release the lock, then access
        PacmineEnvironment.Create(_tempDir).Dispose();
        // Should not throw
        using var env = PacmineEnvironment.Access(_tempDir);
        Assert.NotNull(env);
        Assert.Equal(_tempDir, env.RootPath);
    }

    [Fact]
    public void Access_ThrowsOnMissingEnvironment()
    {
        Assert.Throws<Exception>(() => PacmineEnvironment.Access("/nonexistent/path"));
    }

    // ── TryWriteRegistry / GetRegistry / TryRemoveRegistry ───────────────

    [Fact]
    public void TryWriteRegistry_ThenGetRegistry_ReturnsCorrectData()
    {
        using var env = PacmineEnvironment.Create(_tempDir);

        var registry = CreateTestRegistry("test-pkg", "1.0.0");
        Assert.True(env.TryWriteRegistry(registry));

        var loaded = env.GetRegistry("test-pkg");
        Assert.NotNull(loaded);
        Assert.Equal("test-pkg", loaded.Meta.Name);
        Assert.Equal("1.0.0", loaded.Meta.Version.RawString);
    }

    [Fact]
    public void TryRemoveRegistry_RemovesPackage()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("test-pkg", "1.0.0")));

        Assert.True(env.TryRemoveRegistry("test-pkg"));

        Assert.Null(env.GetRegistry("test-pkg"));
    }

    [Fact]
    public void TryRemoveRegistry_NonExistentPackage_ReturnsFalse()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.False(env.TryRemoveRegistry("nonexistent"));
    }

    [Fact]
    public void GetRegistry_NonExistentPackage_ReturnsNull()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.Null(env.GetRegistry("nonexistent"));
    }

    // ── CheckAcceptance ──────────────────────────────────────────────────

    [Fact]
    public void CheckAcceptance_NoConflictsOrDeps_ReturnsEmpty()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("installed-pkg", "1.0.0")));

        var reasons = env.CheckAcceptance([
            CreateTestMeta("new-pkg", "1.0.0")
        ]);

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckAcceptance_ConflictWithInstalled_ReturnsConflictReason()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("installed-pkg", "1.0.0")));

        var newPkg = CreateTestMeta("new-pkg", "1.0.0",
            conflicts: new() { { "installed-pkg", new VersionRange("^1.0.0") } });

        var reasons = env.CheckAcceptance([newPkg]);

        Assert.NotEmpty(reasons);
        Assert.IsType<ConflictUnacceptReason>(reasons[0]);
    }

    [Fact]
    public void CheckAcceptance_MissingDependency_ReturnsMissingDepReason()
    {
        using var env = PacmineEnvironment.Create(_tempDir);

        var newPkg = CreateTestMeta("new-pkg", "1.0.0",
            depends: new() { { "missing-dep", new VersionRange("^1.0.0") } });

        var reasons = env.CheckAcceptance([newPkg]);

        Assert.NotEmpty(reasons);
        Assert.IsType<MissingDependsUnacceptReason>(reasons[0]);
    }

    [Fact]
    public void CheckAcceptance_SatisfiedDependencyViaVirtualPackage_ReturnsEmpty()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        // Installed package provides a virtual package
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("provider", "2.0.0",
            provides: new() { { "virtual-lib", new VersionIdentifier("1.0.0") } })));

        // New package depends on the virtual package
        var newPkg = CreateTestMeta("consumer", "1.0.0",
            depends: new() { { "virtual-lib", new VersionRange("^1.0.0") } });

        var reasons = env.CheckAcceptance([newPkg]);

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckAcceptance_ReplacesInstalledPackage_ReturnsReplaceReason()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("old-pkg", "1.0.0")));

        var newPkg = CreateTestMeta("new-pkg", "2.0.0",
            replaces: new() { { "old-pkg", new VersionRange("*") } });

        var reasons = env.CheckAcceptance([newPkg]);

        Assert.NotEmpty(reasons);
        Assert.IsType<PackageReplacedUnacceptReason>(reasons[0]);
    }

    // ── CheckCanUninstall ────────────────────────────────────────────────

    [Fact]
    public void CheckCanUninstall_NonExistentPackage_ReturnsNotExistReason()
    {
        using var env = PacmineEnvironment.Create(_tempDir);

        var reasons = env.CheckCanUninstall(["nonexistent"]);

        Assert.NotEmpty(reasons);
        Assert.IsType<NotExistDenyReason>(reasons[0]);
    }

    [Fact]
    public void CheckCanUninstall_NoDependents_ReturnsEmpty()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("standalone", "1.0.0")));

        var reasons = env.CheckCanUninstall(["standalone"]);

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckCanUninstall_HasDependents_ReturnsBreakDependReason()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("dependency", "1.0.0")));
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("dependent", "2.0.0",
            depends: new() { { "dependency", new VersionRange("^1.0.0") } })));

        var reasons = env.CheckCanUninstall(["dependency"]);

        Assert.NotEmpty(reasons);
        Assert.IsType<BreakDependDenyReason>(reasons[0]);
        Assert.Equal("dependent", ((BreakDependDenyReason)reasons[0]).DependedBy);
    }

    // ── CheckConflictFiles ───────────────────────────────────────────────

    [Fact]
    public void CheckConflictFiles_NoConflicts_ReturnsEmpty()
    {
        using var env = PacmineEnvironment.Create(_tempDir);

        var conflicts = env.CheckConflictFiles(["mods/foo.jar"], []);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void CheckConflictFiles_FileManagedByOtherPackage_ReturnsConflict()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("existing", "1.0.0",
            fileList: new() { { "mods/foo.jar", "abc123" } })));

        var conflicts = env.CheckConflictFiles(["mods/foo.jar"], []);

        Assert.NotEmpty(conflicts);
        Assert.Equal("existing", conflicts["mods/foo.jar"]);
    }

    [Fact]
    public void CheckConflictFiles_FileManagedByIgnoredPackage_ReturnsNoConflict()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        Assert.True(env.TryWriteRegistry(CreateTestRegistry("existing", "1.0.0",
            fileList: new() { { "mods/foo.jar", "abc123" } })));

        var conflicts = env.CheckConflictFiles(["mods/foo.jar"], ["existing"]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void CheckConflictFiles_OrphanFileOnDisk_ReturnsConflictWithEmptyOwner()
    {
        using var env = PacmineEnvironment.Create(_tempDir);
        // Create an unmanaged file on disk
        var orphanPath = Path.Combine(_tempDir, "orphan.txt");
        File.WriteAllText(orphanPath, "I'm an orphan!");

        var conflicts = env.CheckConflictFiles(["orphan.txt"], []);

        Assert.NotEmpty(conflicts);
        Assert.Equal(string.Empty, conflicts["orphan.txt"]);
    }

    // ── Helper methods ───────────────────────────────────────────────────

    private static PackageRegistry CreateTestRegistry(
        string name,
        string version,
        Dictionary<string, VersionRange>? depends = null,
        Dictionary<string, VersionRange>? conflicts = null,
        Dictionary<string, VersionIdentifier>? provides = null,
        Dictionary<string, string>? fileList = null)
    {
        return new PackageRegistry
        {
            Meta = CreateTestMeta(name, version, depends, conflicts, provides),
            FileList = fileList ?? [],
            InstallReason = InstallReasons.Explicit,
            InstalledTime = DateTime.Now
        };
    }

    private static PackageMeta CreateTestMeta(
        string name,
        string version,
        Dictionary<string, VersionRange>? depends = null,
        Dictionary<string, VersionRange>? conflicts = null,
        Dictionary<string, VersionIdentifier>? provides = null,
        Dictionary<string, VersionRange>? replaces = null)
    {
        return new PackageMeta
        {
            Name = name,
            Version = new VersionIdentifier(version),
            Depends = depends ?? [],
            Conflicts = conflicts ?? [],
            Provides = provides ?? [],
            Replaces = replaces ?? []
        };
    }
}
