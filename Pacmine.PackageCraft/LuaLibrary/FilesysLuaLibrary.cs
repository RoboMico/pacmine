using Lua;

namespace Pacmine.PackageCraft.LuaLibrary;

/// <summary>
/// Provides file system operations (move, copy, delete, create directory) to Lua build scripts.
/// Operations are restricted to the source and package directories unless arbitrary file operations
/// are explicitly allowed on the <see cref="PackageBuilder"/>.
/// </summary>
[LuaObject]
public partial class FilesysLuaLibrary
{
    private PackageBuilder builderContext;
    private DirectoryInfo sourceDirectory;
    private DirectoryInfo packageDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesysLuaLibrary"/> class.
    /// </summary>
    /// <param name="context">The <see cref="PackageBuilder"/> providing directory configuration and security flags.</param>
    public FilesysLuaLibrary(PackageBuilder context)
    {
        builderContext = context;
        sourceDirectory = context.SourceDirectory!;
        packageDirectory = context.PackageDirectory!;
    }

    private string ReplacePathVariables(string path)
    {
        return path.Replace("${SRCDIR}", sourceDirectory.FullName)
                   .Replace("${PKGDIR}", packageDirectory.FullName);
    }

    private bool IsPathAllowed(string path)
    {
        return path.StartsWith(sourceDirectory.FullName)
               || path.StartsWith(packageDirectory.FullName);
    }

    /// <summary>
    /// Moves a file from the source path to the destination path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// </summary>
    /// <param name="source">The source file path.</param>
    /// <param name="dest">The destination file path.</param>
    /// <exception cref="Exception">Thrown when the operation is outside allowed directories and arbitrary operations are not permitted.</exception>
    [LuaMember("move")]
    public void Move(string source, string dest)
    {
        source = Path.GetFullPath(ReplacePathVariables(source));
        dest = Path.GetFullPath(ReplacePathVariables(dest));
        // check that source and dest are in builderContext.SourceDirectory
        // or builderContext.PackageDirectory
        if (!builderContext.AllowArbitraryFileOperation)
        {
            if (!IsPathAllowed(source) || !IsPathAllowed(dest))
            {
                throw new Exception("Disallowed file system operation");
            }
        }
        File.Move(source, dest);
    }

    /// <summary>
    /// Copies a file from the source path to the destination path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// </summary>
    /// <param name="source">The source file path.</param>
    /// <param name="dest">The destination file path.</param>
    /// <exception cref="Exception">Thrown when the operation is outside allowed directories and arbitrary operations are not permitted.</exception>
    [LuaMember("copy")]
    public void Copy(string source, string dest)
    {
        source = Path.GetFullPath(ReplacePathVariables(source));
        dest = Path.GetFullPath(ReplacePathVariables(dest));
        // check that source and dest are in builderContext.SourceDirectory
        // or builderContext.PackageDirectory
        if (!builderContext.AllowArbitraryFileOperation)
        {
            if (!IsPathAllowed(source) || !IsPathAllowed(dest))
            {
                throw new Exception("Disallowed file system operation");
            }
        }
        File.Copy(source, dest);
    }

    /// <summary>
    /// Deletes the specified file. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// </summary>
    /// <param name="file">The path of the file to delete.</param>
    /// <exception cref="Exception">Thrown when the operation is outside allowed directories and arbitrary operations are not permitted.</exception>
    [LuaMember("delete")]
    public void Delete(string file)
    {
        file = Path.GetFullPath(ReplacePathVariables(file));
        if (!builderContext.AllowArbitraryFileOperation)
        {
            if (!IsPathAllowed(file))
            {
                throw new Exception("Disallowed file system operation");
            }
        }
        File.Delete(file);
    }

    /// <summary>
    /// Creates a directory at the specified path. Supports <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables.
    /// </summary>
    /// <param name="path">The path of the directory to create.</param>
    /// <exception cref="Exception">Thrown when the operation is outside allowed directories and arbitrary operations are not permitted.</exception>
    [LuaMember("mkdir")]
    public void CreateDirectory(string path)
    {
        path = Path.GetFullPath(ReplacePathVariables(path));
        if (!builderContext.AllowArbitraryFileOperation)
        {
            if (!IsPathAllowed(path))
            {
                throw new Exception("Disallowed file system operation");
            }
        }
        Directory.CreateDirectory(path);
    }
}
