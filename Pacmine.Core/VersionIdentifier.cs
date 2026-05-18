using System.Text.Json.Serialization;
using Semver;

namespace Pacmine.Core;

/// <summary>
/// Represents a version identifier compliant with Semantic Versioning 2.0.
/// Wraps <see cref="SemVersion"/> internally for parsing and comparison,
/// with a fallback to raw string comparison for non-SemVer strings.
/// Comparison uses <see cref="SemVersion.ComparePrecedenceTo(SemVersion)"/>
/// when both sides are valid <see cref="SemVersion"/>; otherwise falls back
/// to ordinal <see cref="string.Compare(string, string, StringComparison)"/>.
/// </summary>
[JsonConverter(typeof(VersionIdentifierJsonConverter))]
public class VersionIdentifier : IComparable<VersionIdentifier>, IEquatable<VersionIdentifier>
{
    private static readonly SemVersionStyles ParseStyles =
        SemVersionStyles.OptionalPatch | SemVersionStyles.AllowV;

    private string _raw = "";
    private SemVersion? _semver;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionIdentifier"/> class with the specified version string.
    /// </summary>
    /// <param name="version">The raw version string.</param>
    public VersionIdentifier(string version)
    {
        RawString = version;
    }

    /// <summary>
    /// Gets or sets the raw version string. Setting this property re-parses the version.
    /// </summary>
    public string RawString
    {
        get => _raw;
        set
        {
            _raw = value;
            SemVersion.TryParse(value, ParseStyles, out _semver);
        }
    }

    /// <summary>
    /// Gets the parsed <see cref="SemVersion"/> if the version string is valid SemVer 2.0;
    /// otherwise, <c>null</c>.
    /// </summary>
    public SemVersion? SemVersion => _semver;

    /// <summary>
    /// Gets the segments of the version string. When parsed as SemVer, this returns
    /// [Major, Minor, Patch, ...PrereleaseIdentifiers]; otherwise returns the raw string as a single segment.
    /// </summary>
    public string[] Segments
    {
        get
        {
            if (_semver == null)
                return [_raw];

            var list = new List<string>
            {
                _semver.Major.ToString(),
                _semver.Minor.ToString(),
                _semver.Patch.ToString()
            };
            foreach (var id in _semver.PrereleaseIdentifiers)
                list.Add(id.Value);
            return list.ToArray();
        }
    }

    /// <summary>
    /// Compares this instance to another <see cref="VersionIdentifier"/> using <see cref="SemVersion.ComparePrecedenceTo(SemVersion)"/>
    /// when both are valid <see cref="SemVersion"/>; falls back to ordinal <see cref="string.Compare(string, string, StringComparison)"/> otherwise.
    /// </summary>
    /// <param name="other">The other version identifier to compare to.</param>
    /// <returns>A value indicating the relative order.</returns>
    public int CompareTo(VersionIdentifier? other)
    {
        if (other is null)
            return 1;
        if (_semver != null && other._semver != null)
            return _semver.ComparePrecedenceTo(other._semver);
        return string.Compare(_raw, other._raw, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether two <see cref="VersionIdentifier"/> instances are equal.
    /// </summary>
    public static bool operator ==(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.Equals(b) ?? b is null;
    }

    /// <summary>
    /// Determines whether two <see cref="VersionIdentifier"/> instances are not equal.
    /// </summary>
    public static bool operator !=(VersionIdentifier? a, VersionIdentifier? b)
    {
        return !(a == b);
    }

    /// <summary>
    /// Determines whether one <see cref="VersionIdentifier"/> is less than another.
    /// </summary>
    public static bool operator <(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.CompareTo(b) < 0;
    }

    /// <summary>
    /// Determines whether one <see cref="VersionIdentifier"/> is greater than another.
    /// </summary>
    public static bool operator >(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.CompareTo(b) > 0;
    }

    /// <summary>
    /// Determines whether one <see cref="VersionIdentifier"/> is less than or equal to another.
    /// </summary>
    public static bool operator <=(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.CompareTo(b) <= 0;
    }

    /// <summary>
    /// Determines whether one <see cref="VersionIdentifier"/> is greater than or equal to another.
    /// </summary>
    public static bool operator >=(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.CompareTo(b) >= 0;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> if the objects are equal; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not VersionIdentifier other)
            return false;
        return Equals(other);
    }

    /// <summary>
    /// Determines whether the specified <see cref="VersionIdentifier"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The version identifier to compare with.</param>
    /// <returns><c>true</c> if equal; otherwise, <c>false</c>.</returns>
    public bool Equals(VersionIdentifier? other)
    {
        if (other is null)
            return false;
        if (_semver != null && other._semver != null)
            return _semver.ComparePrecedenceTo(other._semver) == 0;
        return _raw == other._raw;
    }

    /// <summary>
    /// Returns the hash code for the current instance based on the raw string.
    /// </summary>
    public override int GetHashCode()
    {
        return RawString.GetHashCode();
    }

    /// <summary>
    /// Returns the raw version string representation.
    /// </summary>
    public override string ToString()
    {
        return RawString;
    }
}
