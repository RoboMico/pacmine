using Semver;

namespace Pacmine.Core;

/// <summary>
/// Represents a version range expression supporting npm-style syntax including
/// <c>^</c>, <c>~</c>, <c>&gt;=</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&lt;</c>,
/// exact match, wildcard <c>*</c>, <c>x</c>, hyphen ranges, and union <c>||</c>.
/// Delegates to <see cref="SemVersionRange"/> internally.
/// </summary>
public class VersionRange
{
    private readonly SemVersionRange _range;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionRange"/> class by parsing the specified range expression.
    /// Supports npm-style semver ranges.
    /// </summary>
    /// <param name="range">The version range expression string.</param>
    /// <exception cref="InvalidDataException">Thrown when the range expression is invalid.</exception>
    public VersionRange(string range)
    {
        if (!SemVersionRange.TryParseNpm(range, false, out var parsed))
            throw new InvalidDataException($"Invalid version range expression: {range}");
        _range = parsed;
    }

    /// <summary>
    /// A version range that matches any version (<c>*</c>).
    /// </summary>
    public static readonly VersionRange Any = new("*");

    /// <summary>
    /// Determines whether the specified version falls within this range.
    /// Non-SemVer versions (where <see cref="VersionIdentifier.SemVersion"/> is <c>null</c>)
    /// never match.
    /// </summary>
    /// <param name="version">The version to check.</param>
    /// <returns><c>true</c> if the version is contained within the range; otherwise, <c>false</c>.</returns>
    public bool Contains(VersionIdentifier version)
    {
        return version.SemVersion != null && _range.Contains(version.SemVersion);
    }

    /// <summary>
    /// Returns the normalized string representation of the version range.
    /// </summary>
    public override string ToString()
    {
        return _range.ToString();
    }
}
