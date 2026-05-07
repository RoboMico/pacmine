using Lua;
using Pacmine.Models;

namespace Pacmine.PackageCraft;

public class PackageCraftRecipe
{
    public required string Protocol { get; set; }

    public required PackageMeta Meta { get; set; }

    public List<string> Sources { get; set; } = [];

    public List<string> SourceChecksums { get; set; } = [];

    public List<string> VariableFiles { get; set; } = [];

    public LuaFunction? Prepare { get; set; }

    public LuaFunction? GetVersion { get; set; }

    public LuaFunction? Build { get; set; }

    public LuaFunction? Check { get; set; }

    public LuaFunction? Package { get; set; }
}