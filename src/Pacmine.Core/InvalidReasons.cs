namespace Pacmine.Core;

/// <summary>
/// Represents an abstract reason why a package is invalid in a check.
/// </summary>
/// <param name="TargetPackageName">The name of the package that causes the invalid state.</param>
public abstract record InvalidReason(string TargetPackageName);

/// <summary>
/// Indicates that a package name appears more than once in the package list.
/// </summary>
/// <param name="TargetPackageName">The name of the duplicating package.</param>
public record DuplicateNameInvalidReason
(
    string TargetPackageName
) : InvalidReason(TargetPackageName);

/// <summary>
/// Indicates that the package conflicts with another package.
/// </summary>
/// <param name="TargetPackageName">The name of one package in the conflicting pair.</param>
/// <param name="ConflictingPackageName">The name of another package in the conflicting pair.</param>
public record ConflictInvalidReason
(
    string TargetPackageName,
    string ConflictingPackageName
) : InvalidReason(TargetPackageName);

/// <summary>
/// Indicates that a dependency required by the package is missing or has an unsatisfied version.
/// </summary>
/// <param name="TargetPackageName">The name of the depender package.</param>
/// <param name="MissingDependName">The name of the missing or unsatisfied dependency.</param>
/// <param name="DesiredVersions">The version range required for the dependency.</param>
public record MissingDependsInvalidReason
(
    string TargetPackageName,
    string MissingDependName,
    VersionRange DesiredVersions
) : InvalidReason(TargetPackageName);

/// <summary>
/// Indicates that the package would replace another package. Replacements can be considered
/// as a type of "directional" conflict.
/// </summary>
/// <param name="TargetPackageName">The name of the replacer package.</param>
/// <param name="ReplacedPackageName">The name of the package that would be replaced.</param>
public record PackageReplacedInvalidReason
(
    string TargetPackageName,
    string ReplacedPackageName
) : InvalidReason(TargetPackageName);
