using Lua;

namespace Pacmine.PackageCraft;

public class PackageBuilder
{
    private PackageBuilder()
    {
    }

    public PackageCraftRecipe Recipe { get; set; }

    public static async Task<PackageBuilder> FromScriptAsync(string script)
    {
        PackageBuilder builder = new();
        var luaState = LuaState.Create();
        var result = (await luaState.DoStringAsync(script)).First();
        builder.Recipe = result.Read<PackageCraftRecipeLuaObject>();
        return builder;
    }
}