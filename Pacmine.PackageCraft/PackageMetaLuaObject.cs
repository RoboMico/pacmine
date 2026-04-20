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
}