using Pacmine.Models;

namespace Pacmine.Database;

public class PackageRegistry
{
    public required PackageMeta Meta { get; set; }

    public string[] FileList { get; set; } = [];

    public string[] FileSHA256Sums { get; set; } = [];

    public InstallReasons InstallReason { get; set; }

    public DateTime PackagedTime { get; set; }

    public DateTime InstalledTime { get; set; }
}