using Lua;

namespace Pacmine.PackageCraft;

[LuaObject]
public partial class PackageCraftRecipeLuaObject : PackageCraftRecipe
{
    [LuaMember("protocol")]
    public string LuaI_Protocol
    {
        get => Protocol;
        set => Protocol = value;
    }

    [LuaMember("meta")]
    public PackageMetaLuaObject LuaI_Meta
    {
        get => (PackageMetaLuaObject)Meta;
        set => Meta = value;
    }

    [LuaMember("sources")]
    public LuaTable LuaI_Sources
    {
        get
        {
            LuaTable table = [];
            for (int i = 0; i < Sources.Count; i++)
            {
                // index in lua arrays starts from 1
                table[i + 1] = Sources[i];
            }
            return table;
        }
        set
        {
            Sources = [];
            foreach (var pair in value)
            {
                Sources.Add(pair.Value.Read<string>());
            }
        }
    }

    [LuaMember("checksums")]
    public LuaTable LuaI_SourceChecksums
    {
        get
        {
            LuaTable table = [];
            for (int i = 0; i < SourceChecksums.Count; i++)
            {
                table[i + 1] = SourceChecksums[i];
            }
            return table;
        }
        set
        {
            SourceChecksums = [];
            foreach (var pair in value)
            {
                SourceChecksums.Add(pair.Value.Read<string>());
            }
        }
    }

    [LuaMember("prepare")]
    public LuaValue LuaI_Prepare
    {
        get => Prepare ?? LuaValue.Nil;
        set => Prepare = value.Read<LuaFunction>();
    }

    [LuaMember("get_version")]
    public LuaValue LuaI_GetVersion
    {
        get => GetVersion ?? LuaValue.Nil;
        set => GetVersion = value.Read<LuaFunction>();
    }

    [LuaMember("build")]
    public LuaValue LuaI_Build
    {
        get => Build ?? LuaValue.Nil;
        set => Build = value.Read<LuaFunction>();
    }

    [LuaMember("check")]
    public LuaValue LuaI_Check
    {
        get => Check ?? LuaValue.Nil;
        set => Check = value.Read<LuaFunction>();
    }

    [LuaMember("package")]
    public LuaValue LuaI_Package
    {
        get => Package ?? LuaValue.Nil;
        set => Package = value.Read<LuaFunction>();
    }
}