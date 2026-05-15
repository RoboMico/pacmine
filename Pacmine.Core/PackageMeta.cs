using System.Text.RegularExpressions;

namespace Pacmine.Core;

/// <summary>
/// Represents metadata for a package, including its identity, relationships, and version information.
/// </summary>
public partial class PackageMeta
{
    /// <summary>
    /// Gets a regex pattern for validating package names.
    /// </summary>
    /// <remarks>
    /// Package names may only consist of lowercase letters (a-z), digits (0-9), hyphens (-),
    /// underscores (_), and periods (.), and must start with a letter or digit.
    /// </remarks>
    /// <returns>A compiled regex for package name validation.</returns>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-_\.]*$")]
    public static partial Regex PackageNameRegex();

    private string _name = null!;

    /// <summary>
    /// Gets or sets the name of the package.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value does not match <see cref="PackageNameRegex()"/>.</exception>
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
    /// Gets or sets the category of the package (mod, resourcepack, shaderpack, etc).
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
    /// Incrementing this signals a new build of the same upstream version.
    /// </summary>
    public int Release { get; set; } = 1;

    /// <summary>
    /// Gets or sets the epoch number of the package. Defaults to 0.
    /// Incrementing this resets version comparison semantics (higher epoch always wins).
    /// </summary>
    public int Epoch { get; set; } = 0;

    /// <summary>
    /// Gets or sets the list of groups the package belongs to.
    /// </summary>
    public List<string> Groups { get; set; } = [];

    /// <summary>
    /// Gets or sets the virtual packages that this package provides.
    /// Each virtual package is declared with its own <see cref="VersionIdentifier"/>,
    /// which is NOT inherited from the provider.
    /// </summary>
    public Dictionary<string, VersionIdentifier> Provides { get; set; } = [];

    /// <summary>
    /// Gets or sets the packages that this package depends on, mapped to their required version ranges.
    /// </summary>
    public Dictionary<string, VersionRange> Depends { get; set; } = [];

    /// <summary>
    /// Gets or sets the packages that this package conflicts with, mapped to their conflicting version ranges.
    /// </summary>
    public Dictionary<string, VersionRange> Conflicts { get; set; } = [];

    /// <summary>
    /// Gets or sets the packages that this package replaces, mapped to the version ranges being replaced.
    /// </summary>
    public Dictionary<string, VersionRange> Replaces { get; set; } = [];

    /// <summary>
    /// Gets or sets the packages that this package recommends, mapped to their descriptions.
    /// Recommendations are informational only and do not affect installation acceptance.
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

    /// <summary>
    /// Determines whether this package conflicts with the specified package.
    /// Checks both direct package-to-package conflict declarations and virtual packages
    /// (i.e. packages provided via the <see cref="Provides"/> property).
    /// Virtual packages have their own explicit versions (from <see cref="Provides"/>),
    /// which are used when checking version ranges — they are NOT inherited from the provider.
    /// </summary>
    /// <param name="other">The other package to check against.</param>
    /// <returns><c>true</c> if this package conflicts with the other package; otherwise, <c>false</c>.</returns>
    public bool IsConflictingWith(PackageMeta other)
    {
        // Check if this package's Conflicts covers the other package (by its own name)
        if (Conflicts.TryGetValue(other.Name, out var range))
        {
            if (range.Contains(other.Version))
                return true;
        }

        // Check if this package's Conflicts covers any virtual package that the other provides
        foreach (var (virtualName, virtualVersion) in other.Provides)
        {
            if (Conflicts.TryGetValue(virtualName, out var virtualRange))
            {
                if (virtualRange.Contains(virtualVersion))
                    return true;
            }
        }

        // Check if the other package's Conflicts covers this package (by its own name)
        if (other.Conflicts.TryGetValue(Name, out var otherRange))
        {
            if (otherRange.Contains(Version))
                return true;
        }

        // Check if the other package's Conflicts covers any virtual package that this provides
        foreach (var (virtualName, virtualVersion) in Provides)
        {
            if (other.Conflicts.TryGetValue(virtualName, out var otherVirtualRange))
            {
                if (otherVirtualRange.Contains(virtualVersion))
                    return true;
            }
        }

        return false;
    }
}
