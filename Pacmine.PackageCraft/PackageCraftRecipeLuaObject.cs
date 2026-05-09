using Lua;

namespace Pacmine.PackageCraft;

/// <summary>
/// A Lua-compatible wrapper around <see cref="PackageCraftRecipe"/> that exposes recipe properties
/// with Lua attribute mappings for use in PackageCraft build scripts.
/// </summary>
[LuaObject]
public partial class PackageCraftRecipeLuaObject : PackageCraftRecipe
{
    /// <summary>
    /// Gets or sets the protocol version, mapped to the Lua field <c>protocol</c>.
    /// </summary>
    [LuaMember("protocol")]
    public string LuaI_Protocol
    {
        get => Protocol;
        set => Protocol = value;
    }

    /// <summary>
    /// Gets or sets the package metadata as a Lua table, mapped to the Lua field <c>meta</c>.
    /// </summary>
    [LuaMember("meta")]
    public LuaTable LuaI_Meta
    {
        get => ((PackageMetaLuaObject)Meta).ToLuaTable();
        set => Meta = PackageMetaLuaObject.FromLuaTable(value);
    }

    /// <summary>
    /// Gets or sets the source list as a Lua table (1-indexed array), mapped to the Lua field <c>sources</c>.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the source checksums as a Lua table (1-indexed array), mapped to the Lua field <c>source_checksums</c>.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the variable files list as a Lua table (1-indexed array), mapped to the Lua field <c>variable_files</c>.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the prepare Lua function, mapped to the Lua field <c>prepare</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("prepare")]
    public LuaValue LuaI_Prepare
    {
        get => Prepare ?? LuaValue.Nil;
        set => Prepare = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();

    }

    /// <summary>
    /// Gets or sets the get-version Lua function, mapped to the Lua field <c>get_version</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("get_version")]
    public LuaValue LuaI_GetVersion
    {
        get => GetVersion ?? LuaValue.Nil;
        set => GetVersion = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    /// <summary>
    /// Gets or sets the build Lua function, mapped to the Lua field <c>build</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("build")]
    public LuaValue LuaI_Build
    {
        get => Build ?? LuaValue.Nil;
        set => Build = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    /// <summary>
    /// Gets or sets the check Lua function, mapped to the Lua field <c>check</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("check")]
    public LuaValue LuaI_Check
    {
        get => Check ?? LuaValue.Nil;
        set => Check = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    /// <summary>
    /// Gets or sets the package Lua function, mapped to the Lua field <c>package</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("package")]
    public LuaValue LuaI_Package
    {
        get => Package ?? LuaValue.Nil;
        set => Package = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    /// <summary>
    /// Creates a new <see cref="PackageCraftRecipeLuaObject"/> from a Lua table by reading each known field.
    /// </summary>
    /// <param name="table">The Lua table containing recipe data.</param>
    /// <returns>A populated <see cref="PackageCraftRecipeLuaObject"/> instance.</returns>
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

    /// <summary>
    /// Converts this instance to a Lua table with all recipe fields.
    /// </summary>
    /// <returns>A Lua table representing the recipe.</returns>
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
