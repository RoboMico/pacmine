using Lua;

namespace Pacmine.PackageCraft.LuaLibrary;

/// <summary>
/// Provides file system operations (move, copy, delete, create directory) to Lua build scripts.
/// Operations are restricted to <see cref="PackageBuilder.SourceDirectory"/> and
/// <see cref="PackageBuilder.PackageDirectory"/> only.
/// </summary>
[LuaObject]
public partial class RestrictedFilesysLuaLibrary : AbstractFilesysLuaLibrary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RestrictedFilesysLuaLibrary"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The allowed source directory.</param>
    /// <param name="packageDirectory">The allowed package staging directory.</param>
    public RestrictedFilesysLuaLibrary(DirectoryInfo sourceDirectory, DirectoryInfo packageDirectory)
        : base(sourceDirectory, packageDirectory)
    {
    }

    private static StringComparison GetPathStringComparison()
    {
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static bool IsSubPath(string resolvedPath, DirectoryInfo baseDirectory)
    {
        var comparison = GetPathStringComparison();
        var relative = Path.GetRelativePath(baseDirectory.FullName, resolvedPath);
        return !relative.StartsWith("..", comparison) && !Path.IsPathRooted(relative);
    }

    private void AssertPathAllowed(string resolvedPath)
    {
        bool inSource = IsSubPath(resolvedPath, sourceDirectory);
        bool inPackage = IsSubPath(resolvedPath, packageDirectory);

        if (!inSource && !inPackage)
        {
            throw new UnauthorizedAccessException(
                $"File system operation denied: path '{resolvedPath}' is outside the allowed directories.");
        }
    }

    private void AssertPathNotRoot(string resolvedPath)
    {
        var comparison = GetPathStringComparison();
        var trimmedPath = resolvedPath.TrimEnd(Path.DirectorySeparatorChar);
        if (trimmedPath.Equals(sourceDirectory.FullName, comparison) ||
            trimmedPath.Equals(packageDirectory.FullName, comparison))
        {
            throw new UnauthorizedAccessException(
                $"File system operation denied: path '{resolvedPath}' is a root directory.");
        }
    }

    /// <summary>
    /// Moves a file from the source path to the destination path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// Both paths must reside within the source or package directories.
    /// </summary>
    /// <param name="source">The source file path.</param>
    /// <param name="dest">The destination file path.</param>
    [LuaMember("move")]
    public void Move(string source, string dest)
    {
        var resolvedSource = ResolvePath(source);
        var resolvedDest = ResolvePath(dest);
        AssertPathAllowed(resolvedSource);
        AssertPathAllowed(resolvedDest);
        File.Move(resolvedSource, resolvedDest);
    }

    /// <summary>
    /// Copies a file from the source path to the destination path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// Both paths must reside within the source or package directories.
    /// </summary>
    /// <param name="source">The source file path.</param>
    /// <param name="dest">The destination file path.</param>
    [LuaMember("copy")]
    public void Copy(string source, string dest)
    {
        var resolvedSource = ResolvePath(source);
        var resolvedDest = ResolvePath(dest);
        AssertPathAllowed(resolvedSource);
        AssertPathAllowed(resolvedDest);
        File.Copy(resolvedSource, resolvedDest);
    }

    /// <summary>
    /// Deletes the specified file. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// The file must reside within the source or package directories.
    /// Attempting to delete folders with this method will throw an exception.
    /// Use <see cref="DeleteDirectory"/> instead.
    /// </summary>
    /// <param name="file">The path of the file to delete.</param>
    [LuaMember("delete")]
    public void Delete(string file)
    {
        var resolvedFile = ResolvePath(file);
        AssertPathAllowed(resolvedFile);
        File.Delete(resolvedFile);
    }

    /// <summary>
    /// Deletes the specified directory and all its contents. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// The directory must reside within the source or package directories and must not be the source or package directory itself.
    /// </summary>
    /// <param name="path">The path of the directory to delete.</param>
    [LuaMember("deletedir")]
    public void DeleteDirectory(string path)
    {
        var resolvedPath = ResolvePath(path);
        AssertPathAllowed(resolvedPath);
        AssertPathNotRoot(resolvedPath);
        Directory.Delete(resolvedPath, true);
    }

    /// <summary>
    /// Creates a directory at the specified path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// The path must reside within the source or package directories.
    /// </summary>
    /// <param name="path">The path of the directory to create.</param>
    [LuaMember("mkdir")]
    public void CreateDirectory(string path)
    {
        var resolvedPath = ResolvePath(path);
        AssertPathAllowed(resolvedPath);
        Directory.CreateDirectory(resolvedPath);
    }
}
