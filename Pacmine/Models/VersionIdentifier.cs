namespace Pacmine.Models;

/// <summary>
/// Represents a version identifier that supports comparison and equality operations.
/// Version strings are split into segments by '.' and '-' delimiters.
/// </summary>
public class VersionIdentifier : IComparable<VersionIdentifier>, IEquatable<VersionIdentifier>
{
    private string _raw = "";

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionIdentifier"/> class with the specified version string.
    /// </summary>
    /// <param name="version">The raw version string.</param>
    public VersionIdentifier(string version)
    {
        RawString = version;
    }

    /// <summary>
    /// Gets or sets the raw version string. Setting this property re-parses the segments.
    /// </summary>
    public string RawString
    {
        get => _raw;
        set
        {
            _raw = value;
            Segments = value.Split(['.', '-']);
        }
    }

    /// <summary>
    /// Gets the segments of the version string, split by '.' and '-' characters.
    /// </summary>
    public string[] Segments { get; private set; } = [];

    /// <summary>
    /// Compares this instance to another <see cref="VersionIdentifier"/>.
    /// Segments are compared numerically if both are integers; otherwise lexicographically.
    /// If all segments are identical, the raw string is compared.
    /// </summary>
    /// <param name="other">The other version identifier to compare to.</param>
    /// <returns>A value indicating the relative order.</returns>
    public int CompareTo(VersionIdentifier? other)
    {
        if (other is null)
            return 1;
        if (Segments.Length != other.Segments.Length)
            return Segments.Length.CompareTo(other.Segments.Length);
        for (int i = 0; i < Segments.Length; i++)
        {
            var segA = Segments[i];
            var segB = other.Segments[i];
            // if the two segments are both number, compare them as numbers
            if (int.TryParse(segA, out var numA) && int.TryParse(segB, out var numB))
            {
                if (numA != numB)
                    return numA.CompareTo(numB);
            }
            // otherwise, compare them as strings
            else if (segA != segB)
                return segA.CompareTo(segB);
        }
        return RawString.CompareTo(other.RawString);
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
        if (obj is not VersionIdentifier)
            return false;
        var other = (VersionIdentifier)obj;
        return RawString == other.RawString;
    }

    /// <summary>
    /// Determines whether the specified <see cref="VersionIdentifier"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The version identifier to compare with.</param>
    /// <returns><c>true</c> if equal; otherwise, <c>false</c>.</returns>
    public bool Equals(VersionIdentifier? other)
    {
        return Equals(other);
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
