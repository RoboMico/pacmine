namespace Pacmine.Models;

public class VersionIdentifier : IComparable<VersionIdentifier>, IEquatable<VersionIdentifier>
{
    private string _raw = "";

    public VersionIdentifier(string version)
    {
        RawString = version;
    }

    public string RawString
    {
        get => _raw;
        set
        {
            _raw = value;
            Segments = value.Split(['.', '-']);
        }
    }

    public string[] Segments { get; private set; } = [];

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

    public static bool operator ==(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.Equals(b) ?? b is null;
    }

    public static bool operator !=(VersionIdentifier? a, VersionIdentifier? b)
    {
        return !(a == b);
    }

    public static bool operator <(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.CompareTo(b) < 0;
    }

    public static bool operator >(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.CompareTo(b) > 0;
    }

    public static bool operator <=(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.CompareTo(b) <= 0;
    }

    public static bool operator >=(VersionIdentifier? a, VersionIdentifier? b)
    {
        return a?.CompareTo(b) >= 0;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not VersionIdentifier)
            return false;
        var other = (VersionIdentifier)obj;
        return RawString == other.RawString;
    }

    public bool Equals(VersionIdentifier? other)
    {
        return Equals(other);
    }

    public override int GetHashCode()
    {
        return RawString.GetHashCode();
    }

    public override string ToString()
    {
        return RawString;
    }
}