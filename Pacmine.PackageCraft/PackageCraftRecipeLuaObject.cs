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
    public LuaTable LuaI_Meta
    {
        get => ((PackageMetaLuaObject)Meta).ToLuaTable();
        set => Meta = PackageMetaLuaObject.FromLuaTable(value);
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

    [LuaMember("source_checksums")]
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

    [LuaMember("variable_files")]
    public LuaTable LuaI_VariableFiles
    {
        get
        {
            LuaTable table = [];
            for (int i = 0; i < VariableFiles.Count; i++)
            {
                table[i + 1] = VariableFiles[i];
            }
            return table;
        }
        set
        {
            VariableFiles = [];
            foreach (var pair in value)
            {
                VariableFiles.Add(pair.Value.Read<string>());
            }
        }
    }

    [LuaMember("prepare")]
    public LuaValue LuaI_Prepare
    {
        get => Prepare ?? LuaValue.Nil;
        set => Prepare = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();

    }

    [LuaMember("get_version")]
    public LuaValue LuaI_GetVersion
    {
        get => GetVersion ?? LuaValue.Nil;
        set => GetVersion = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    [LuaMember("build")]
    public LuaValue LuaI_Build
    {
        get => Build ?? LuaValue.Nil;
        set => Build = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    [LuaMember("check")]
    public LuaValue LuaI_Check
    {
        get => Check ?? LuaValue.Nil;
        set => Check = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    [LuaMember("package")]
    public LuaValue LuaI_Package
    {
        get => Package ?? LuaValue.Nil;
        set => Package = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    public static PackageCraftRecipeLuaObject FromLuaTable(LuaTable table)
    {
        return new()
        {
            Protocol = table["protocol"].Read<string>(),
            Meta = PackageMetaLuaObject.FromLuaTable(table["meta"].Read<LuaTable>()),
            LuaI_Sources = table["sources"].Read<LuaTable>(),
            LuaI_SourceChecksums = table["source_checksums"].Read<LuaTable>(),
            LuaI_VariableFiles = table["variable_files"].Read<LuaTable>(),
            LuaI_Prepare = table["prepare"],
            LuaI_GetVersion = table["get_version"],
            LuaI_Build = table["build"],
            LuaI_Check = table["check"],
            LuaI_Package = table["package"]
        };
    }

    public LuaTable ToLuaTable()
    {
        return new()
        {
            ["protocol"] = Protocol,
            ["meta"] = ((PackageMetaLuaObject)Meta).ToLuaTable(),
            ["sources"] = LuaI_Sources,
            ["source_checksums"] = LuaI_SourceChecksums,
            ["variable_files"] = LuaI_VariableFiles,
            ["prepare"] = LuaI_Prepare,
            ["get_version"] = LuaI_GetVersion,
            ["build"] = LuaI_Build,
            ["check"] = LuaI_Check,
            ["package"] = LuaI_Package
        };
    }
}