using Pacmine.Core;
using Pacmine.Environment;
using System.Text.Json;

using PacmineEnvironment env = PacmineEnvironment.Access(args[0]);

env.WriteRegistry(new PackageRegistry
{
    Meta = new PackageMeta
    {
        Name = "minecraft",
        Description = "environment package minecraft",
        UpstreamUrl = "N/A",
        Category = "env",
        License = "N/A",
        Version = new VersionIdentifier("26.1.2")
    },
    FileList = [],
    InstallReason = InstallReasons.Explicit,
    InstalledTime = DateTime.Now
});