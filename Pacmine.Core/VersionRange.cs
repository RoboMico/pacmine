using System.Text.Json.Serialization;
using Semver;

namespace Pacmine.Core;

/// <summary>
/// Represents a version range expression operating in one of three modes:
/// <list type="bullet">
///   <item><description><b>SemVer compatible</b> — If the provided string is a valid npm-style
///   version range (supporting <c>^</c>, <c>~</c>, <c>&gt;=</c>, <c>&gt;</c>, <c>&lt;=</c>,
///   <c>&lt;</c>, exact match, <c>x</c>, hyphen ranges, and union <c>||</c>), the range is
///   evaluated using semver rules via <see cref="SemVersionRange"/>.</description></item>
///   <item><description><b>Any</b> — The string <c>"*"</c> matches any version, regardless of
///   whether the <see cref="VersionIdentifier"/> is semver-compatible or not.</description></item>
///   <item><description><b>Literal match</b> — If the string is not a valid npm-style version range
///   (and is not <c>"*"</c>), the range performs an exact ordinal match against the raw string
///   of the <see cref="VersionIdentifier"/>.</description></item>
/// </list>
/// </summary>
[JsonConverter(typeof(VersionRangeJsonConverter))]
public class VersionRange
{
    private readonly SemVersionRange? _range;
    private readonly string? _literal;
    private readonly bool _isAny;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionRange"/> class.
    /// Automatically selects the appropriate mode based on the input string:
    /// <c>"*"</c> becomes Any mode; valid npm-style ranges become SemVer mode;
    /// everything else becomes literal-match mode.
    /// </summary>
    /// <param name="range">The version range expression string.</param>
    public VersionRange(string range)
    {
        if (range == "*")
        {
            _isAny = true;
        }
        else if (SemVersionRange.TryParseNpm(range, false, out var parsed))
        {
            _range = parsed;
        }
        else
        {
            _literal = range;
        }
    }

    /// <summary>
    /// A version range that matches any version (<c>*</c>).
    /// </summary>
    public static readonly VersionRange Any = new("*");

    /// <summary>
    /// Determines whether the specified version falls within this range.
    /// Behaviour depends on the active mode:
    /// <list type="bullet">
    ///   <item><description>Any mode (<c>*</c>): always returns <c>true</c>.</description></item>
    ///   <item><description>SemVer mode: returns <c>true</c> only when the version is a valid
    ///   <see cref="SemVersion"/> and satisfies the npm-style range.</description></item>
    ///   <item><description>Literal mode: returns <c>true</c> only when the version's
    ///   <see cref="VersionIdentifier.RawString"/> is an exact ordinal match of the stored
    ///   literal string.</description></item>
    /// </list>
    /// </summary>
    /// <param name="version">The version to check.</param>
    /// <returns><c>true</c> if the version is contained within the range; otherwise, <c>false</c>.</returns>
    public bool Contains(VersionIdentifier version)
    {
        if (_isAny)
            return true;

        if (_range != null)
            return version.SemVersion != null && _range.Contains(version.SemVersion);

        // Literal mode — exact ordinal match against the raw version string
        return version.RawString == _literal;
    }

    /// <summary>
    /// Returns the normalized string representation of the version range.
    /// </summary>
    public override string ToString()
    {
        if (_isAny)
            return "*";

        if (_range != null)
            return _range.ToString();

        return _literal!;
    }
}
