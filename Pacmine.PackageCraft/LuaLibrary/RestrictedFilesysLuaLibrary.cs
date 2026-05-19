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
    private readonly DirectoryInfo sourceDirectory;
    private readonly DirectoryInfo packageDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestrictedFilesysLuaLibrary"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The allowed source directory.</param>
    /// <param name="packageDirectory">The allowed package staging directory.</param>
    public RestrictedFilesysLuaLibrary(DirectoryInfo sourceDirectory, DirectoryInfo packageDirectory)
        : base(sourceDirectory, packageDirectory)
    {
        this.sourceDirectory = sourceDirectory;
        this.packageDirectory = packageDirectory;
    }

    private void AssertPathAllowed(string resolvedPath)
    {
        if (!resolvedPath.StartsWith(sourceDirectory.FullName, StringComparison.Ordinal) &&
            !resolvedPath.StartsWith(packageDirectory.FullName, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                $"File system operation denied: path '{resolvedPath}' is outside the allowed directories.");
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
