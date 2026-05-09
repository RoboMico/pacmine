using Lua;
using Pacmine.Models;

namespace Pacmine.PackageCraft;

/// <summary>
/// A Lua-compatible wrapper around <see cref="PackageMeta"/> that exposes package metadata
/// properties with Lua attribute mappings for use in PackageCraft build scripts.
/// </summary>
[LuaObject]
public partial class PackageMetaLuaObject : PackageMeta
{
    /// <summary>
    /// Gets or sets the package name, mapped to the Lua field <c>name</c>.
    /// </summary>
    [LuaMember("name")]
    public string LuaI_Name
    {
        get => Name;
        set => Name = value;
    }

    /// <summary>
    /// Gets or sets the package description, mapped to the Lua field <c>description</c>.
    /// </summary>
    [LuaMember("description")]
    public string LuaI_Description
    {
        get => Description;
        set => Description = value;
    }

    /// <summary>
    /// Gets or sets the upstream URL, mapped to the Lua field <c>upstream_url</c>.
    /// </summary>
    [LuaMember("upstream_url")]
    public string LuaI_UpstreamUrl
    {
        get => UpstreamUrl;
        set => UpstreamUrl = value;
    }

    /// <summary>
    /// Gets or sets the package category, mapped to the Lua field <c>category</c>.
    /// </summary>
    [LuaMember("category")]
    public string LuaI_Category
    {
        get => Category;
        set => Category = value;
    }

    /// <summary>
    /// Gets or sets the license identifier, mapped to the Lua field <c>license</c>.
    /// </summary>
    [LuaMember("license")]
    public string LuaI_License
    {
        get => License;
        set => License = value;
    }

    /// <summary>
    /// Gets or sets the version as a string, mapped to the Lua field <c>version</c>.
    /// </summary>
    [LuaMember("version")]
    public string LuaI_Version
    {
        get => Version.ToString();
        set => Version = new(value);
    }

    /// <summary>
    /// Gets or sets the release number, mapped to the Lua field <c>release</c>.
    /// </summary>
    [LuaMember("release")]
    public int LuaI_Release
    {
        get => Release;
        set => Release = value;
    }

    /// <summary>
    /// Gets or sets the epoch number, mapped to the Lua field <c>epoch</c>.
    /// </summary>
    [LuaMember("epoch")]
    public int LuaI_Epoch
    {
        get => Epoch;
        set => Epoch = value;
    }

    /// <summary>
    /// Gets or sets the list of groups as a Lua table, mapped to the Lua field <c>groups</c>.
    /// </summary>
    [LuaMember("groups")]
    public LuaTable LuaI_Groups
    {
        get
        {
            LuaTable table = [];
            for (int i = 0; i < Groups.Count; i++)
            {
                table[i + 1] = Groups[i].ToString();
            }
            return table;
        }
        set
        {
            Groups = [];
            foreach (var item in value)
            {
                Groups.Add(item.Key.Read<string>());
            }
        }
    }

    /// <summary>
    /// Gets or sets the provides dictionary as a Lua table, mapped to the Lua field <c>provides</c>.
    /// </summary>
    [LuaMember("provides")]
    public LuaTable LuaI_Provides
    {
        get
        {
            LuaTable table = [];
            foreach (var item in Provides)
            {
                table[item.Key] = item.Value.ToString();
            }
            return table;
        }
        set
        {
            Provides = [];
            foreach (var item in value)
            {
                Provides.Add(item.Key.Read<string>(), new(item.Value.Read<string>()));
            }
        }
    }

    /// <summary>
    /// Gets or sets the dependencies dictionary as a Lua table, mapped to the Lua field <c>depends</c>.
    /// </summary>
    [LuaMember("depends")]
    public LuaTable LuaI_Depends
    {
        get
        {
            LuaTable table = [];
            foreach (var item in Depends)
            {
                table[item.Key] = item.Value.ToString();
            }
            return table;
        }
        set
        {
            Depends = [];
            foreach (var item in value)
            {
                Depends.Add(item.Key.Read<string>(), new(item.Value.Read<string>()));
            }
        }
    }

    /// <summary>
    /// Gets or sets the conflicts dictionary as a Lua table, mapped to the Lua field <c>conflicts</c>.
    /// </summary>
    [LuaMember("conflicts")]
    public LuaTable LuaI_Conflicts
    {
        get
        {
            LuaTable table = [];
            foreach (var item in Conflicts)
            {
                table[item.Key] = item.Value.ToString();
            }
            return table;
        }
        set
        {
            Conflicts = [];
            foreach (var item in value)
            {
                Conflicts.Add(item.Key.Read<string>(), new(item.Value.Read<string>()));
            }
        }
    }

    /// <summary>
    /// Gets or sets the replaces dictionary as a Lua table, mapped to the Lua field <c>replaces</c>.
    /// </summary>
    [LuaMember("replaces")]
    public LuaTable LuaI_Replaces
    {
        get
        {
            LuaTable table = [];
            foreach (var item in Replaces)
            {
                table[item.Key] = item.Value.ToString();
            }
            return table;
        }
        set
        {
            Replaces = [];
            foreach (var item in value)
            {
                Replaces.Add(item.Key.Read<string>(), new(item.Value.Read<string>()));
            }
        }
    }

    /// <summary>
    /// Gets or sets the recommendations dictionary as a Lua table, mapped to the Lua field <c>recommends</c>.
    /// </summary>
    [LuaMember("recommends")]
    public LuaTable LuaI_Recommends
    {
        get
        {
            LuaTable table = [];
            foreach (var item in Recommends)
            {
                table[item.Key] = item.Value;
            }
            return table;
        }
        set
        {
            Recommends = [];
            foreach (var item in value)
            {
                Recommends.Add(item.Key.Read<string>(), item.Value.Read<string>());
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="PackageMetaLuaObject"/> from a Lua table by reading each known field.
    /// </summary>
    /// <param name="table">The Lua table containing package metadata.</param>
    /// <returns>A populated <see cref="PackageMetaLuaObject"/> instance.</returns>
    public static PackageMetaLuaObject FromLuaTable(LuaTable table)
    {
        return new()
        {
            Name = table["name"].Read<string>(),
            Description = table["description"].Read<string>(),
            UpstreamUrl = table["upstream_url"].Read<string>(),
            Category = table["category"].Read<string>(),
            License = table["license"].Read<string>(),
            Version = new(table["version"].Read<string>()),
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
            ["name"] = Name,
            ["description"] = Description,
            ["upstream_url"] = UpstreamUrl,
            ["category"] = Category,
            ["license"] = License,
            ["version"] = Version.ToString(),
            ["release"] = Release,
            ["epoch"] = Epoch,
            ["groups"] = LuaI_Groups,
            ["provides"] = LuaI_Provides,
            ["depends"] = LuaI_Depends,
            ["conflicts"] = LuaI_Conflicts,
            ["replaces"] = LuaI_Replaces,
            ["recommends"] = LuaI_Recommends
        };
    }
}
