namespace Pacmine.Models;

public class PackageMeta
{
    public required string Name { get; set; }

    public required string Description { get; set; }

    public required string UpstreamUrl { get; set; }

    public required string Category { get; set; }

    public required string License { get; set; }

    public required VersionIdentifier Version { get; set; }

    public int Release { get; set; } = 1;

    public int Epoch { get; set; } = 0;

    public Dictionary<string, VersionIdentifier> Provides { get; set; } = [];

    public Dictionary<string, VersionRange> Depends { get; set; } = [];

    public Dictionary<string, VersionRange> Conflicts { get; set; } = [];

    public Dictionary<string, VersionRange> Replaces { get; set; } = [];

    public Dictionary<string, string> Recommends { get; set; } = [];

    public string GetFullVersionString()
    {
        return $"{((Epoch != 0) ? $"{Epoch}:" : "")}{Version}-{Release}";
    }
}