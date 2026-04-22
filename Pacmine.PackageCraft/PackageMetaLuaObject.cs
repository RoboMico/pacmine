using Lua;
using Pacmine.Models;

namespace Pacmine.PackageCraft;

[LuaObject]
public partial class PackageMetaLuaObject : PackageMeta
{
    [LuaMember("name")]
    public string LuaI_Name
    {
        get => Name;
        set => Name = value;
    }

    [LuaMember("description")]
    public string LuaI_Description
    {
        get => Description;
        set => Description = value;
    }

    [LuaMember("upstream_url")]
    public string LuaI_UpstreamUrl
    {
        get => UpstreamUrl;
        set => UpstreamUrl = value;
    }

    [LuaMember("category")]
    public string LuaI_Category
    {
        get => Category;
        set => Category = value;
    }

    [LuaMember("license")]
    public string LuaI_License
    {
        get => License;
        set => License = value;
    }

    [LuaMember("version")]
    public string LuaI_Version
    {
        get => Version.ToString();
        set => Version = new(value);
    }

    [LuaMember("release")]
    public int LuaI_Release
    {
        get => Release;
        set => Release = value;
    }

    [LuaMember("epoch")]
    public int LuaI_Epoch
    {
        get => Epoch;
        set => Epoch = value;
    }

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
            LuaI_Provides = table["provides"].Read<LuaTable>(),
            LuaI_Depends = table["depends"].Read<LuaTable>(),
            LuaI_Conflicts = table["conflicts"].Read<LuaTable>(),
            LuaI_Replaces = table["replaces"].Read<LuaTable>(),
            LuaI_Recommends = table["recommends"].Read<LuaTable>()
        };
    }

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
            ["provides"] = LuaI_Provides,
            ["depends"] = LuaI_Depends,
            ["conflicts"] = LuaI_Conflicts,
            ["replaces"] = LuaI_Replaces,
            ["recommends"] = LuaI_Recommends
        };
    }
}