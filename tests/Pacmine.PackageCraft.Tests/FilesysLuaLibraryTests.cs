using Xunit;
using Pacmine.PackageCraft.LuaLibrary;
using Pacmine.TestUtils;

namespace Pacmine.PackageCraft.Tests;

/// <summary>
/// Tests for <see cref="RestrictedFilesysLuaLibrary"/> and <see cref="UnsafeFilesysLuaLibrary"/>.
/// Both can be constructed directly without a <see cref="PackageBuilder"/> instance,
/// making them fully unit-testable.
/// </summary>
public class FilesysLuaLibraryTests
{
    // ── RestrictedFilesysLuaLibrary ──────────────────────────────────────

    [Fact]
    public void Restricted_Copy_OutsideAllowedDirectory_Throws()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        using var outsideDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var sourceFile = Path.Combine(srcDir.Path, "test.txt");
        File.WriteAllText(sourceFile, "content");
        var destFile = Path.Combine(outsideDir.Path, "test.txt");

        Assert.Throws<UnauthorizedAccessException>(() => restricted.Copy(sourceFile, destFile));
    }

    [Fact]
    public void Restricted_Copy_WithinAllowedDirectory_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var sourceFile = Path.Combine(srcDir.Path, "source.txt");
        File.WriteAllText(sourceFile, "hello");
        var destFile = Path.Combine(srcDir.Path, "dest.txt");

        restricted.Copy(sourceFile, destFile);

        Assert.True(File.Exists(destFile));
        Assert.Equal("hello", File.ReadAllText(destFile));
    }

    [Fact]
    public void Restricted_Move_OutsideAllowedDirectory_Throws()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        using var outsideDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var sourceFile = Path.Combine(srcDir.Path, "test.txt");
        File.WriteAllText(sourceFile, "content");
        var destFile = Path.Combine(outsideDir.Path, "test.txt");

        Assert.Throws<UnauthorizedAccessException>(() => restricted.Move(sourceFile, destFile));
    }

    [Fact]
    public void Restricted_Move_WithinPkgDirectory_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var sourceFile = Path.Combine(pkgDir.Path, "source.txt");
        File.WriteAllText(sourceFile, "move me");
        var destFile = Path.Combine(pkgDir.Path, "moved.txt");

        restricted.Move(sourceFile, destFile);

        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public void Restricted_Delete_OutsideAllowedDirectory_Throws()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        using var outsideDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var outsideFile = Path.Combine(outsideDir.Path, "test.txt");
        File.WriteAllText(outsideFile, "content");

        Assert.Throws<UnauthorizedAccessException>(() => restricted.Delete(outsideFile));
    }

    [Fact]
    public void Restricted_Delete_WithinAllowedDirectory_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var file = Path.Combine(srcDir.Path, "delete_me.txt");
        File.WriteAllText(file, "delete me");

        restricted.Delete(file);

        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Restricted_Mkdir_OutsideAllowedDirectory_Throws()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        using var outsideDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var outsidePath = Path.Combine(outsideDir.Path, "newdir");

        Assert.Throws<UnauthorizedAccessException>(() => restricted.CreateDirectory(outsidePath));
    }

    [Fact]
    public void Restricted_Mkdir_WithinPkgDirectory_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var newDir = Path.Combine(pkgDir.Path, "subdir");

        restricted.CreateDirectory(newDir);

        Assert.True(Directory.Exists(newDir));
    }

    [Fact]
    public void Restricted_UsesSrdcirVariable()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var file = Path.Combine(srcDir.Path, "from_src.txt");
        File.WriteAllText(file, "src");
        var dest = Path.Combine(pkgDir.Path, "from_src.txt");

        restricted.Copy("${SRCDIR}/from_src.txt", "${PKGDIR}/from_src.txt");

        Assert.True(File.Exists(dest));
    }

    [Fact]
    public void Restricted_UsesPkgdirVariable()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var file = Path.Combine(pkgDir.Path, "from_pkg.txt");
        File.WriteAllText(file, "pkg");
        var dest = Path.Combine(srcDir.Path, "from_pkg.txt");

        restricted.Move("${PKGDIR}/from_pkg.txt", "${SRCDIR}/from_pkg.txt");

        Assert.False(File.Exists(file));
        Assert.True(File.Exists(dest));
    }

    [Fact]
    public void Restricted_AssertsPath_PrefixTraversal_Throws()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        // If srcDir is /tmp/abc, a path under /tmp/abc-other should NOT be allowed
        using var prefixTraversalDir = new TempDirectory();
        var traversalPath = prefixTraversalDir.Path + "-evil";
        Directory.CreateDirectory(traversalPath);
        var fileInTraversalPath = Path.Combine(traversalPath, "file.txt");
        File.WriteAllText(fileInTraversalPath, "content");

        Assert.Throws<UnauthorizedAccessException>(() => restricted.Delete(fileInTraversalPath));
    }

    [Fact]
    public void Restricted_AssertsPath_EqualsDirectoryItself_Throws()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        // Deleting the source directory itself is not a child operation
        Assert.Throws<UnauthorizedAccessException>(() => { restricted.Delete(srcDir.Path); });
    }

    [Fact]
    public void Restricted_Mkdir_CreatesSubdirectoryInsideAllowedDir_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var subDir = Path.Combine(srcDir.Path, "nested", "deep", "dir");

        restricted.CreateDirectory(subDir);

        Assert.True(Directory.Exists(subDir));
    }

    [Fact]
    public void Restricted_Delete_NonExistentFile_DoesNotThrow()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var nonExistent = Path.Combine(srcDir.Path, "does_not_exist.txt");

        // File.Delete does not throw if file doesn't exist
        restricted.Delete(nonExistent);
    }

    [Fact]
    public void Restricted_Copy_SourceEqualsDestination_ThrowsOrSucceeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        var restricted = new RestrictedFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var file = Path.Combine(srcDir.Path, "same.txt");
        File.WriteAllText(file, "same");

        // File.Copy with source==dest throws IOException on .NET
        Assert.Throws<IOException>(() => restricted.Copy(file, file));
    }

    // ── UnsafeFilesysLuaLibrary ──────────────────────────────────────────

    [Fact]
    public void Unsafe_Copy_ToOutsideDirectory_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        using var outsideDir = new TempDirectory();
        var unsafeLib = new UnsafeFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var sourceFile = Path.Combine(srcDir.Path, "test.txt");
        File.WriteAllText(sourceFile, "content");
        var destFile = Path.Combine(outsideDir.Path, "test.txt");

        unsafeLib.Copy(sourceFile, destFile);

        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public void Unsafe_Move_ToOutsideDirectory_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        using var outsideDir = new TempDirectory();
        var unsafeLib = new UnsafeFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var sourceFile = Path.Combine(srcDir.Path, "test.txt");
        File.WriteAllText(sourceFile, "content");
        var destFile = Path.Combine(outsideDir.Path, "test.txt");

        unsafeLib.Move(sourceFile, destFile);

        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public void Unsafe_Delete_OutsideAllowedDirectory_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        using var outsideDir = new TempDirectory();
        var unsafeLib = new UnsafeFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var outsideFile = Path.Combine(outsideDir.Path, "test.txt");
        File.WriteAllText(outsideFile, "content");

        unsafeLib.Delete(outsideFile);

        Assert.False(File.Exists(outsideFile));
    }

    [Fact]
    public void Unsafe_Mkdir_OutsideAllowedDirectory_Succeeds()
    {
        using var srcDir = new TempDirectory();
        using var pkgDir = new TempDirectory();
        using var outsideDir = new TempDirectory();
        var unsafeLib = new UnsafeFilesysLuaLibrary(srcDir.DirInfo, pkgDir.DirInfo);

        var outsidePath = Path.Combine(outsideDir.Path, "newdir");

        unsafeLib.CreateDirectory(outsidePath);

        Assert.True(Directory.Exists(outsidePath));
    }
}
