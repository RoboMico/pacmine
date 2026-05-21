using Pacmine.Core;

namespace Pacmine.Environment;

/// <summary>
/// Represents the registry entry for an installed package, including its metadata,
/// file list, checksums, installation reason, and timestamps.
/// </summary>
public class PackageRegistry
{
    /// <summary>
    /// Gets or sets the package metadata.
    /// </summary>
    public required PackageMeta Meta { get; set; }

    /// <summary>
    /// Gets or sets the list of files owned by this package. Every entry is (file path, SHA256 checksum).
    /// </summary>
    public Dictionary<string, string> FileList { get; set; } = [];

    /// <summary>
    /// Gets or sets the reason why the package was installed.
    /// </summary>
    public InstallReasons InstallReason { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the package was packaged (built).
    /// </summary>
    public DateTime PackagedTime { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the package was installed.
    /// </summary>
    public DateTime InstalledTime { get; set; }
}
