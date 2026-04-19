namespace Pacmine.Models;

public class VersionIdentifier : IComparable<VersionIdentifier>, IEquatable<VersionIdentifier>
{
    public VersionIdentifier(string version)
    {
        Segments = version.Split(['.', '-']).ToList();
    }
    public VersionIdentifier(List<string> segments)
    {
        Segments = segments;
    }

    List<string> Segments { get; set; } = [];

    public int CompareTo(VersionIdentifier? other)
    {
        if (other is null)
            return 1;
        if (Segments.Count != other.Segments.Count)
            return Segments.Count.CompareTo(other.Segments.Count);
        for (int i = 0; i < Segments.Count; i++)
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
        return 0;
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
        if (Segments.Count != other.Segments.Count)
            return false;
        for (int i = 0; i < Segments.Count; i++)
        {
            if (Segments[i] != other.Segments[i])
                return false;
        }
        return true;
    }

    public bool Equals(VersionIdentifier? other)
    {
        return Equals(other);
    }

    public override int GetHashCode()
    {
        return Segments.GetHashCode();
    }

    public override string ToString()
    {
        return string.Join('.', Segments);
    }
}