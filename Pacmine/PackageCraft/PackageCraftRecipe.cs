using Lua;
using Pacmine.Models;

namespace Pacmine.PackageCraft;

[LuaObject]
public partial class PackageCraftRecipe
{
    [LuaMember("protocol")]
    public string Protocol;

    [LuaMember("meta")]
    public PackageMeta Meta;

    [LuaMember("sources")]
    public List<string> Sources;

    [LuaMember("checksums")]
    public List<string> SourceChecksums;

    [LuaMember("prepare")]
    public LuaFunction? Prepare;

    [LuaMember("get_version")]
    public LuaFunction? GetVersion;

    [LuaMember("build")]
    public LuaFunction? Build;

    [LuaMember("check")]
    public LuaFunction? Check;

    [LuaMember("package")]
    public LuaFunction? Package;
}