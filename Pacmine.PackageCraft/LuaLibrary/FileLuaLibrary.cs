using Lua;

namespace Pacmine.PackageCraft.LuaLibrary;

[LuaObject]
public partial class FileLuaLibrary
{
    private PackageBuilder builderContext;

    public FileLuaLibrary(PackageBuilder context)
    {
        builderContext = context;
    }

    private string ReplacePathVariables(string path)
    {
        return path.Replace("${SRCDIR}", builderContext.SourceDirectory.FullName)
                   .Replace("${PKGDIR}", builderContext.PackageDirectory.FullName);
    }

    [LuaMember("move")]
    public void Move(string source, string dest)
    {
        source = Path.GetFullPath(ReplacePathVariables(source));
        dest = Path.GetFullPath(ReplacePathVariables(dest));
        // check that source and dest are in builderContext.SourceDirectory
        // or builderContext.PackageDirectory
        if (!builderContext.AllowArbitaryFileOperation)
        {
            if (!source.StartsWith(builderContext.SourceDirectory.FullName)
            || !source.StartsWith(builderContext.PackageDirectory.FullName)
            || !dest.StartsWith(builderContext.SourceDirectory.FullName)
            || !dest.StartsWith(builderContext.PackageDirectory.FullName))
            {
                throw new Exception("Disallowed file operation");
            }
        }
        File.Move(source, dest);
    }

    [LuaMember("copy")]
    public void Copy(string source, string dest)
    {
        source = Path.GetFullPath(ReplacePathVariables(source));
        dest = Path.GetFullPath(ReplacePathVariables(dest));
        // check that source and dest are in builderContext.SourceDirectory
        // or builderContext.PackageDirectory
        if (!builderContext.AllowArbitaryFileOperation)
        {
            if (!source.StartsWith(builderContext.SourceDirectory.FullName)
            || !source.StartsWith(builderContext.PackageDirectory.FullName)
            || !dest.StartsWith(builderContext.SourceDirectory.FullName)
            || !dest.StartsWith(builderContext.PackageDirectory.FullName))
            {
                throw new Exception("Disallowed file operation");
            }
        }
        File.Copy(source, dest);
    }

    [LuaMember("delete")]
    public void Delete(string file)
    {
        file = Path.GetFullPath(ReplacePathVariables(file));
        if (!builderContext.AllowArbitaryFileOperation)
        {
            if (!file.StartsWith(builderContext.SourceDirectory.FullName)
            || !file.StartsWith(builderContext.PackageDirectory.FullName))
            {
                throw new Exception("Disallowed file operation");
            }
        }
        File.Delete(file);
    }

    [LuaMember("mkdir")]
    public void CreateDirectory(string path)
    {
        path = Path.GetFullPath(ReplacePathVariables(path));
        if (!builderContext.AllowArbitaryFileOperation)
        {
            if (!path.StartsWith(builderContext.SourceDirectory.FullName)
            || !path.StartsWith(builderContext.PackageDirectory.FullName))
            {
                throw new Exception("Disallowed file operation");
            }
        }
        Directory.CreateDirectory(path);
    }
}