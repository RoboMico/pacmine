using System.Text.RegularExpressions;

namespace Pacmine.Core;

/// <summary>
/// Represents metadata for a package, including its identity, relationships, and version information.
/// </summary>
public partial class PackageMeta
{
    /// <summary>
    /// Get a regex pattern for validating package names.
    /// Package name can only consist of lowercase letters(a-z), digits(0-9), hyphens(-), underscores(_), and periods(.),
    /// and must start with a letter or digit.
    /// </summary>
    /// <returns>A regex pattern for validating package names.</returns>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-_\.]*$")]
    public static partial Regex PackageNameRegex();

    private string _name = null!;

    /// <summary>
    /// Gets or sets the name of the package.
    /// </summary>
    public required string Name
    {
        get => _name;
        set
        {
            if (!PackageNameRegex().IsMatch(value))
                throw new ArgumentException("Invalid package name");
            _name = value;
        }
    }

    /// <summary>
    /// Gets or sets the description of the package.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the upstream URL of the package, typically the project homepage or repository.
    /// </summary>
    public required string UpstreamUrl { get; set; }

    /// <summary>
    /// Gets or sets the category of the package(mod, resourcepack, shaderpack, etc).
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Gets or sets the license identifier for the package.
    /// </summary>
    public required string License { get; set; }

    /// <summary>
    /// Gets or sets the version identifier for the package.
    /// </summary>
    public required VersionIdentifier Version { get; set; }

    /// <summary>
    /// Gets or sets the release number of the package. Defaults to 1.
    /// </summary>
    public int Release { get; set; } = 1;

    /// <summary>
    /// Gets or sets the epoch number of the package. Defaults to 0.
    /// </summary>
    public int Epoch { get; set; } = 0;

    /// <summary>
    /// Gets or sets the list of groups the package belongs to.
    /// </summary>
    public List<string> Groups { get; set; } = [];

    /// <summary>
    /// Gets or sets the virtual packages that this package provides.
    /// </summary>
    public Dictionary<string, VersionIdentifier> Provides { get; set; } = [];

    /// <summary>
    /// Gets or sets the packages that this package depends on.
    /// </summary>
    public Dictionary<string, VersionRange> Depends { get; set; } = [];

    /// <summary>
    /// Gets or sets the packages that this package conflicts with.
    /// </summary>
    public Dictionary<string, VersionRange> Conflicts { get; set; } = [];

    /// <summary>
    /// Gets or sets the packages that this package replaces.
    /// </summary>
    public Dictionary<string, VersionRange> Replaces { get; set; } = [];

    /// <summary>
    /// Gets or sets the packages and their descriptions that this package recommends.
    /// </summary>
    public Dictionary<string, string> Recommends { get; set; } = [];

    /// <summary>
    /// Gets the full version string, including epoch (if non-zero), version, and release number.
    /// </summary>
    /// <returns>A formatted version string in the form <c>epoch:version-release</c>.</returns>
    public string GetFullVersionString()
    {
        return $"{((Epoch != 0) ? $"{Epoch}:" : "")}{Version}-{Release}";
    }

    /// <summary>
    /// Determines whether this package is newer than the specified package by comparing epoch, version, and release.
    /// </summary>
    /// <param name="other">The other package to compare against.</param>
    /// <returns><c>true</c> if this package is newer; otherwise, <c>false</c>.</returns>
    public bool IsNewerThan(PackageMeta other)
    {
        if (Epoch != other.Epoch)
            return Epoch > other.Epoch;
        if (Version != other.Version)
            return Version > other.Version;
        return Release > other.Release;
    }
}
