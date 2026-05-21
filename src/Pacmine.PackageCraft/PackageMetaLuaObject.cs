using Lua;
using Pacmine.Core;

namespace Pacmine.PackageCraft;

/// <summary>
/// A Lua-compatible wrapper around <see cref="PackageMeta"/> that exposes package metadata
/// properties with Lua attribute mappings for use in PackageCraft build scripts.
/// </summary>
[LuaObject]
public partial class PackageMetaLuaObject
{
    /// <summary>
    /// Gets the underlying <see cref="PackageMeta"/> model instance wrapped by this object.
    /// </summary>
    public PackageMeta Meta { get; }

    /// <summary>
    /// Initializes a new <see cref="PackageMetaLuaObject"/> that wraps the specified <see cref="PackageMeta"/>.
    /// </summary>
    /// <param name="meta">The package metadata model to wrap.</param>
    public PackageMetaLuaObject(PackageMeta meta)
    {
        Meta = meta;
    }

    /// <summary>
    /// Gets or sets the package name, mapped to the Lua field <c>name</c>.
    /// </summary>
    [LuaMember("name")]
    public string LuaI_Name
    {
        get => Meta.Name;
        set => Meta.Name = value;
    }

    /// <summary>
    /// Gets or sets the version as a string, mapped to the Lua field <c>version</c>.
    /// </summary>
    [LuaMember("version")]
    public string LuaI_Version
    {
        get => Meta.Version.ToString();
        set => Meta.Version = new(value);
    }

    /// <summary>
    /// Gets or sets the package description, mapped to the Lua field <c>description</c>.
    /// </summary>
    [LuaMember("description")]
    public string LuaI_Description
    {
        get => Meta.Description;
        set => Meta.Description = value;
    }

    /// <summary>
    /// Gets or sets the upstream URL, mapped to the Lua field <c>upstream_url</c>.
    /// </summary>
    [LuaMember("upstream_url")]
    public string LuaI_UpstreamUrl
    {
        get => Meta.UpstreamUrl;
        set => Meta.UpstreamUrl = value;
    }

    /// <summary>
    /// Gets or sets the package category, mapped to the Lua field <c>category</c>.
    /// </summary>
    [LuaMember("category")]
    public string LuaI_Category
    {
        get => Meta.Category;
        set => Meta.Category = value;
    }

    /// <summary>
    /// Gets or sets the license identifier, mapped to the Lua field <c>license</c>.
    /// </summary>
    [LuaMember("license")]
    public string LuaI_License
    {
        get => Meta.License;
        set => Meta.License = value;
    }

    /// <summary>
    /// Gets or sets the release number, mapped to the Lua field <c>release</c>.
    /// </summary>
    [LuaMember("release")]
    public int LuaI_Release
    {
        get => Meta.Release;
        set => Meta.Release = value;
    }

    /// <summary>
    /// Gets or sets the epoch number, mapped to the Lua field <c>epoch</c>.
    /// </summary>
    [LuaMember("epoch")]
    public int LuaI_Epoch
    {
        get => Meta.Epoch;
        set => Meta.Epoch = value;
    }

    /// <summary>
    /// Gets or sets the list of groups as a Lua table, mapped to the Lua field <c>groups</c>.
    /// </summary>
    [LuaMember("groups")]
    public LuaTable LuaI_Groups
    {
        get => LuaTableHelper.ListToLuaArray(Meta.Groups);
        set => Meta.Groups = LuaTableHelper.LuaArrayToList<string>(value);
    }

    /// <summary>
    /// Gets or sets the provides dictionary as a Lua table, mapped to the Lua field <c>provides</c>.
    /// </summary>
    [LuaMember("provides")]
    public LuaTable LuaI_Provides
    {
        get => LuaTableHelper.DictionaryToLuaTable(Meta.Provides, v => v.ToString());
        set => Meta.Provides = LuaTableHelper.LuaTableToDictionary(value, s => new VersionIdentifier(s));
    }

    /// <summary>
    /// Gets or sets the dependencies dictionary as a Lua table, mapped to the Lua field <c>depends</c>.
    /// </summary>
    [LuaMember("depends")]
    public LuaTable LuaI_Depends
    {
        get => LuaTableHelper.DictionaryToLuaTable(Meta.Depends, v => v.ToString());
        set => Meta.Depends = LuaTableHelper.LuaTableToDictionary(value, s => new VersionRange(s));
    }

    /// <summary>
    /// Gets or sets the conflicts dictionary as a Lua table, mapped to the Lua field <c>conflicts</c>.
    /// </summary>
    [LuaMember("conflicts")]
    public LuaTable LuaI_Conflicts
    {
        get => LuaTableHelper.DictionaryToLuaTable(Meta.Conflicts, v => v.ToString());
        set => Meta.Conflicts = LuaTableHelper.LuaTableToDictionary(value, s => new VersionRange(s));
    }

    /// <summary>
    /// Gets or sets the replaces dictionary as a Lua table, mapped to the Lua field <c>replaces</c>.
    /// </summary>
    [LuaMember("replaces")]
    public LuaTable LuaI_Replaces
    {
        get => LuaTableHelper.DictionaryToLuaTable(Meta.Replaces, v => v.ToString());
        set => Meta.Replaces = LuaTableHelper.LuaTableToDictionary(value, s => new VersionRange(s));
    }

    /// <summary>
    /// Gets or sets the recommendations dictionary as a Lua table, mapped to the Lua field <c>recommends</c>.
    /// </summary>
    [LuaMember("recommends")]
    public LuaTable LuaI_Recommends
    {
        get => LuaTableHelper.DictionaryToLuaTable(Meta.Recommends, v => v);
        set => Meta.Recommends = LuaTableHelper.LuaTableToDictionary(value, s => s);
    }

    /// <summary>
    /// Creates a new <see cref="PackageMetaLuaObject"/> from a Lua table by reading each known field.
    /// </summary>
    /// <param name="table">The Lua table containing package metadata.</param>
    /// <returns>A populated <see cref="PackageMetaLuaObject"/> instance wrapping the parsed <see cref="PackageMeta"/>.</returns>
    public static PackageMetaLuaObject FromLuaTable(LuaTable table)
    {
        PackageMeta meta = new()    // placeholder values
        {
            Name = "a",
            Version = new("1")
        };
        return new PackageMetaLuaObject(meta)
        {
            LuaI_Name = table["name"].Read<string>(),
            LuaI_Version = table["version"].Read<string>(),
            LuaI_Description = table["description"].Read<string>(),
            LuaI_UpstreamUrl = table["upstream_url"].Read<string>(),
            LuaI_Category = table["category"].Read<string>(),
            LuaI_License = table["license"].Read<string>(),
            LuaI_Release = table["release"].Read<int>(),
            LuaI_Epoch = table["epoch"].Read<int>(),
            LuaI_Groups = table["groups"].Read<LuaTable>(),
            LuaI_Provides = table["provides"].Read<LuaTable>(),
            LuaI_Depends = table["depends"].Read<LuaTable>(),
            LuaI_Conflicts = table["conflicts"].Read<LuaTable>(),
            LuaI_Replaces = table["replaces"].Read<LuaTable>(),
            LuaI_Recommends = table["recommends"].Read<LuaTable>()
        };
    }

    /// <summary>
    /// Converts this instance to a Lua table with all package metadata fields.
    /// </summary>
    /// <returns>A Lua table representing the package metadata.</returns>
    public LuaTable ToLuaTable()
    {
        return new()
        {
            ["name"] = LuaI_Name,
            ["version"] = LuaI_Version,
            ["description"] = LuaI_Description,
            ["upstream_url"] = LuaI_UpstreamUrl,
            ["category"] = LuaI_Category,
            ["license"] = LuaI_License,
            ["release"] = LuaI_Release,
            ["epoch"] = LuaI_Epoch,
            ["groups"] = LuaI_Groups,
            ["provides"] = LuaI_Provides,
            ["depends"] = LuaI_Depends,
            ["conflicts"] = LuaI_Conflicts,
            ["replaces"] = LuaI_Replaces,
            ["recommends"] = LuaI_Recommends
        };
    }
}
