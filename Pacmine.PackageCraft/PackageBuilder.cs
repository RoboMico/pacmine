using Lua;

namespace Pacmine.PackageCraft;

public class PackageBuilder
{
    private DirectoryInfo _workingDir;
    private DirectoryInfo _srcDir;
    private DirectoryInfo _pkgDir;
    private DirectoryInfo _outDir;

    private PackageBuilder()
    {
    }

    public PackageCraftRecipe Recipe { get; private set; }

    public static async Task<PackageBuilder> CreateAsync(string script)
    {
        PackageBuilder builder = new();
        var luaState = LuaState.Create();
        var result = (await luaState.DoStringAsync(script)).First().Read<LuaTable>();
        builder.Recipe = PackageCraftRecipeLuaObject.FromLuaTable(result);
        return builder;
    }

    public PackageBuilder ConfigureWorkingDirector(string workingDir)
    {
        _workingDir = new DirectoryInfo(workingDir);
        _srcDir = new DirectoryInfo(Path.Combine(workingDir, "src"));
        _pkgDir = new DirectoryInfo(Path.Combine(workingDir, "pkg"));
        _outDir = new DirectoryInfo(workingDir);
        return this;
    }

    public PackageBuilder ConfigureSourceDirectory(string srcDir)
    {
        _srcDir = new DirectoryInfo(srcDir);
        return this;
    }

    public PackageBuilder ConfigurePackageDirectory(string pkgDir)
    {
        _pkgDir = new DirectoryInfo(pkgDir);
        return this;
    }

    public PackageBuilder ConfigureOutputDirectory(string outDir)
    {
        _outDir = new DirectoryInfo(outDir);
        return this;
    }

    public async Task<FileInfo> BuildAsync()
    {
        // step 1: initialize lua state
        LuaState luaState = LuaState.Create();

        // step 2: prepare sources

        // step 3: invoke script

        // step 4: compress package

        // step 5: clean up
    }
}