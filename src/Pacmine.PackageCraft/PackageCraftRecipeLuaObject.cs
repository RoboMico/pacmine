using Lua;
using Pacmine.Core;

namespace Pacmine.PackageCraft;

/// <summary>
/// A Lua-compatible wrapper around <see cref="PackageCraftRecipe"/> that exposes recipe properties
/// with Lua attribute mappings for use in PackageCraft build scripts.
/// </summary>
[LuaObject]
public partial class PackageCraftRecipeLuaObject
{
    /// <summary>
    /// Gets the underlying <see cref="PackageCraftRecipe"/> model instance wrapped by this object.
    /// </summary>
    public PackageCraftRecipe Recipe { get; }

    /// <summary>
    /// Initializes a new <see cref="PackageCraftRecipeLuaObject"/> that wraps the specified <see cref="PackageCraftRecipe"/>.
    /// </summary>
    /// <param name="recipe">The package craft recipe model to wrap.</param>
    public PackageCraftRecipeLuaObject(PackageCraftRecipe recipe)
    {
        Recipe = recipe;
    }

    /// <summary>
    /// Gets or sets the protocol version, mapped to the Lua field <c>protocol</c>.
    /// </summary>
    [LuaMember("protocol")]
    public string LuaI_Protocol
    {
        get => Recipe.Protocol;
        set => Recipe.Protocol = value;
    }

    /// <summary>
    /// Gets or sets the package metadata as a Lua table, mapped to the Lua field <c>meta</c>.
    /// </summary>
    [LuaMember("meta")]
    public LuaTable LuaI_Meta
    {
        get => new PackageMetaLuaObject(Recipe.Meta).ToLuaTable();
        set => Recipe.Meta = PackageMetaLuaObject.FromLuaTable(value).Meta;
    }

    /// <summary>
    /// Gets or sets the source list as a Lua table (1-indexed array), mapped to the Lua field <c>sources</c>.
    /// </summary>
    [LuaMember("sources")]
    public LuaTable LuaI_Sources
    {
        get => LuaTableHelper.ListToLuaArray(Recipe.Sources);
        set => Recipe.Sources = LuaTableHelper.LuaArrayToList<string>(value);
    }

    /// <summary>
    /// Gets or sets the source checksums as a Lua table (1-indexed array), mapped to the Lua field <c>source_checksums</c>.
    /// </summary>
    [LuaMember("source_checksums")]
    public LuaTable LuaI_SourceChecksums
    {
        get => LuaTableHelper.ListToLuaArray(Recipe.SourceChecksums);
        set => Recipe.SourceChecksums = LuaTableHelper.LuaArrayToList<string>(value);
    }

    /// <summary>
    /// Gets or sets the prepare Lua function, mapped to the Lua field <c>prepare</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("prepare")]
    public LuaValue LuaI_Prepare
    {
        get => Recipe.LuaFuncPrepare ?? LuaValue.Nil;
        set => Recipe.LuaFuncPrepare = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();

    }

    /// <summary>
    /// Gets or sets the get-version Lua function, mapped to the Lua field <c>get_version</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("get_version")]
    public LuaValue LuaI_GetVersion
    {
        get => Recipe.LuaFuncGetVersion ?? LuaValue.Nil;
        set => Recipe.LuaFuncGetVersion = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    /// <summary>
    /// Gets or sets the build Lua function, mapped to the Lua field <c>build</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("build")]
    public LuaValue LuaI_Build
    {
        get => Recipe.LuaFuncBuild ?? LuaValue.Nil;
        set => Recipe.LuaFuncBuild = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    /// <summary>
    /// Gets or sets the check Lua function, mapped to the Lua field <c>check</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("check")]
    public LuaValue LuaI_Check
    {
        get => Recipe.LuaFuncCheck ?? LuaValue.Nil;
        set => Recipe.LuaFuncCheck = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    /// <summary>
    /// Gets or sets the package Lua function, mapped to the Lua field <c>package</c>. Returns <c>Nil</c> when not set.
    /// </summary>
    [LuaMember("package")]
    public LuaValue LuaI_Package
    {
        get => Recipe.LuaFuncPackage ?? LuaValue.Nil;
        set => Recipe.LuaFuncPackage = (value.Type == LuaValueType.Nil) ? null : value.Read<LuaFunction>();
    }

    /// <summary>
    /// Creates a new <see cref="PackageCraftRecipeLuaObject"/> from a Lua table by reading each known field.
    /// </summary>
    /// <param name="table">The Lua table containing recipe data.</param>
    /// <returns>A populated <see cref="PackageCraftRecipeLuaObject"/> instance wrapping the parsed <see cref="PackageCraftRecipe"/>.</returns>
    public static PackageCraftRecipeLuaObject FromLuaTable(LuaTable table)
    {
        var recipe = new PackageCraftRecipe // placeholder values
        {
            Protocol = "-",
            Meta = new PackageMeta() { Name = "a", Version = new("1") }
        };
        return new PackageCraftRecipeLuaObject(recipe)
        {
            LuaI_Protocol = table["protocol"].Read<string>(),
            LuaI_Meta = table["meta"].Read<LuaTable>(),
            LuaI_Sources = table["sources"].Read<LuaTable>(),
            LuaI_SourceChecksums = table["source_checksums"].Read<LuaTable>(),
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
            ["protocol"] = LuaI_Protocol,
            ["meta"] = LuaI_Meta,
            ["sources"] = LuaI_Sources,
            ["source_checksums"] = LuaI_SourceChecksums,
            ["prepare"] = LuaI_Prepare,
            ["get_version"] = LuaI_GetVersion,
            ["build"] = LuaI_Build,
            ["check"] = LuaI_Check,
            ["package"] = LuaI_Package
        };
    }
}
