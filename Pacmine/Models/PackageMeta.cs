using Lua;

namespace Pacmine.Models;

[LuaObject]
public partial class PackageMeta
{
    [LuaMember("name")]
    public required string Name { get; set; }

    [LuaMember("description")]
    public required string Description { get; set; }

    [LuaMember("upstream_url")]
    public required string UpstreamUrl { get; set; }

    [LuaMember("category")]
    public required string Category { get; set; }

    [LuaMember("license")]
    public required string License { get; set; }

    [LuaMember("version")]
    public string VersionString
    {
        get => Version.ToString();
        set => Version = new VersionIdentifier(value);
    }

    public required VersionIdentifier Version { get; set; }

    [LuaMember("release")]
    public int Release { get; set; } = 1;

    [LuaMember("epoch")]
    public int Epoch { get; set; } = 0;

    [LuaMember("provides")]
    public Dictionary<string, string> ProvidesString
    {
        get => Provides.ToDictionary(x => x.Key, x => x.Value.ToString());
        set => Provides = value.ToDictionary(x => x.Key, x => new VersionIdentifier(x.Value));
    }

    public Dictionary<string, VersionIdentifier> Provides { get; set; } = [];

    [LuaMember("depends")]
    public Dictionary<string, string> DependsString
    {
        get => Depends.ToDictionary(x => x.Key, x => x.Value.ToString());
        set => Depends = value.ToDictionary(x => x.Key, x => new VersionRange(x.Value));
    }

    public Dictionary<string, VersionRange> Depends { get; set; } = [];

    [LuaMember("conflicts")]
    public Dictionary<string, string> ConflictsString
    {
        get => Conflicts.ToDictionary(x => x.Key, x => x.Value.ToString());
        set => Conflicts = value.ToDictionary(x => x.Key, x => new VersionRange(x.Value));
    }

    public Dictionary<string, VersionRange> Conflicts { get; set; } = [];
}