using Lua;

namespace Pacmine.PackageCraft.LuaLibrary;

/// <summary>
/// Provides file system operations (move, copy, delete, create directory) to Lua build scripts.
/// This variant performs <b>no</b> path restriction checks — all file system operations are allowed.
/// Use only when the recipe requires arbitrary file access (opt-in via <see cref="PackageBuilderFactory"/>).
/// </summary>
[LuaObject]
public partial class UnsafeFilesysLuaLibrary : AbstractFilesysLuaLibrary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsafeFilesysLuaLibrary"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The source directory (used for <c>${SRCDIR}</c> variable substitution only).</param>
    /// <param name="packageDirectory">The package directory (used for <c>${PKGDIR}</c> variable substitution only).</param>
    public UnsafeFilesysLuaLibrary(DirectoryInfo sourceDirectory, DirectoryInfo packageDirectory)
        : base(sourceDirectory, packageDirectory)
    {
    }

    /// <summary>
    /// Moves a file from the source path to the destination path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// </summary>
    /// <param name="source">The source file path.</param>
    /// <param name="dest">The destination file path.</param>
    [LuaMember("move")]
    public void Move(string source, string dest)
    {
        File.Move(ResolvePath(source), ResolvePath(dest));
    }

    /// <summary>
    /// Copies a file from the source path to the destination path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// </summary>
    /// <param name="source">The source file path.</param>
    /// <param name="dest">The destination file path.</param>
    [LuaMember("copy")]
    public void Copy(string source, string dest)
    {
        File.Copy(ResolvePath(source), ResolvePath(dest), true);
    }

    /// <summary>
    /// Deletes the specified file. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// Attempting to delete folders with this method will throw an exception.
    /// Use <see cref="DeleteDirectory"/> instead.
    /// </summary>
    /// <param name="file">The path of the file to delete.</param>
    [LuaMember("delete")]
    public void Delete(string file)
    {
        File.Delete(ResolvePath(file));
    }

    /// <summary>
    /// Deletes the specified directory and all its contents. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// </summary>
    /// <param name="path">The path of the directory to delete.</param>
    [LuaMember("deletedir")]
    public void DeleteDirectory(string path)
    {
        Directory.Delete(ResolvePath(path), true);
    }

    /// <summary>
    /// Creates a directory at the specified path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// </summary>
    /// <param name="path">The path of the directory to create.</param>
    [LuaMember("mkdir")]
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(ResolvePath(path));
    }
}
