using Pacmine.Core;

namespace Pacmine.Environment;

/// <summary>
/// Represents an abstract reason why a package is not acceptable for installation.
/// </summary>
/// <param name="RefusedPackageName">The name of the package that was refused.</param>
public abstract record UnacceptReason(string RefusedPackageName);

/// <summary>
/// Indicates that the package conflicts with another installed package.
/// </summary>
/// <param name="RefusedPackageName">The name of the package being refused.</param>
/// <param name="ConflictingPackageName">The name of the installed package that conflicts.</param>
/// <param name="ConflictingVersions">The version range that caused the conflict.</param>
public record ConflictUnacceptReason
(
    string RefusedPackageName,
    string ConflictingPackageName,
    VersionRange ConflictingVersions
) : UnacceptReason(RefusedPackageName);

/// <summary>
/// Indicates that a dependency required by the package is missing or has an unsatisfied version.
/// </summary>
/// <param name="RefusedPackageName">The name of the package being refused.</param>
/// <param name="MissingDependName">The name of the missing or unsatisfied dependency.</param>
/// <param name="DesiredVersions">The version range required for the dependency.</param>
public record MissingDependsUnacceptReason
(
    string RefusedPackageName,
    string MissingDependName,
    VersionRange DesiredVersions
) : UnacceptReason(RefusedPackageName);

/// <summary>
/// Indicates that the package would replace an already-installed package.
/// </summary>
/// <param name="RefusedPackageName">The name of the package being refused.</param>
/// <param name="ReplacedPackageName">The name of the installed package that would be replaced.</param>
public record PackageReplacedUnacceptReason
(
    string RefusedPackageName,
    string ReplacedPackageName
) : UnacceptReason(RefusedPackageName);
