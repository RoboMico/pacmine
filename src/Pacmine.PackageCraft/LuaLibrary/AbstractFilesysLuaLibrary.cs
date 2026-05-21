namespace Pacmine.PackageCraft.LuaLibrary;

/// <summary>
/// Provides shared utilities (path variable substitution, path resolution) for filesystem Lua libraries.
/// Concrete subclasses decide whether to enforce path restrictions.
/// </summary>
public abstract class AbstractFilesysLuaLibrary
{
    protected readonly DirectoryInfo sourceDirectory;
    protected readonly DirectoryInfo packageDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractFilesysLuaLibrary"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The source directory for <c>${SRCDIR}</c> substitution.</param>
    /// <param name="packageDirectory">The package directory for <c>${PKGDIR}</c> substitution.</param>
    protected AbstractFilesysLuaLibrary(DirectoryInfo sourceDirectory, DirectoryInfo packageDirectory)
    {
        this.sourceDirectory = sourceDirectory;
        this.packageDirectory = packageDirectory;
    }

    /// <summary>
    /// Replaces <c>${SRCDIR}</c> and <c>${PKGDIR}</c> variables with their absolute directory paths.
    /// </summary>
    protected string ReplacePathVariables(string path)
    {
        return path.Replace("${SRCDIR}", sourceDirectory.FullName)
                   .Replace("${PKGDIR}", packageDirectory.FullName);
    }

    /// <summary>
    /// Resolves a path by substituting variables and converting to an absolute path.
    /// </summary>
    protected string ResolvePath(string path)
    {
        return Path.GetFullPath(ReplacePathVariables(path));
    }
}
