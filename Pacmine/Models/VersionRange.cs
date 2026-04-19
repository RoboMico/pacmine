using System.Text;

namespace Pacmine.Models;

public class VersionRange
{
    public VersionRange(string range)
    {
        range = range.Trim();
        Min = null;
        Max = null;
        MinEquals = false;
        MaxEquals = false;

        if (range == "*")
            return;

        var parts = range.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith(">="))
            {
                if (Min != null)
                    throw new InvalidDataException($"Invalid version range expression: {range}");
                Min = new VersionIdentifier(part[2..]);
                MinEquals = true;
            }
            else if (part.StartsWith('>'))
            {
                if (Min != null)
                    throw new InvalidDataException($"Invalid version range expression: {range}");
                Min = new VersionIdentifier(part[1..]);
                MinEquals = false;
            }
            else if (part.StartsWith("<="))
            {
                if (Max != null)
                    throw new InvalidDataException($"Invalid version range expression: {range}");
                Max = new VersionIdentifier(part[2..]);
                MaxEquals = true;
            }
            else if (part.StartsWith('<'))
            {
                if (Max != null)
                    throw new InvalidDataException($"Invalid version range expression: {range}");
                Max = new VersionIdentifier(part[1..]);
                MaxEquals = false;
            }
            else
            {
                if (parts.Length == 1)
                {
                    Min = new VersionIdentifier(part);
                    Max = new VersionIdentifier(part);
                    MinEquals = true;
                    MaxEquals = true;
                }
                else
                    throw new InvalidDataException($"Invalid version range expression: {range}");
            }
        }
    }

    public VersionIdentifier? Min { get; set; } // null = -infinity
    public VersionIdentifier? Max { get; set; } // null = +infinity
    public bool MinEquals { get; set; }
    public bool MaxEquals { get; set; }

    public readonly static VersionRange Any = new("*");

    public bool Contains(VersionIdentifier version)
    {
        if (Min != null)
        {
            if (MinEquals)
            {
                if (version < Min)
                    return false;
            }
            else
            {
                if (version <= Min)
                    return false;
            }
        }
        if (Max != null)
        {
            if (MaxEquals)
            {
                if (version > Max)
                    return false;
            }
            else
            {
                if (version >= Max)
                    return false;
            }
        }
        return true;
    }

    public override string ToString()
    {
        if (Min == null && Max == null)
            return "*";
        var sb = new StringBuilder();
        if (Min != null)
        {
            if (MinEquals)
                sb.Append(">=");
            else
                sb.Append('>');
            sb.Append(Min);
        }
        if (Max != null)
        {
            if (Min != null)
                sb.Append(' ');
            if (MaxEquals)
                sb.Append("<=");
            else
                sb.Append('<');
            sb.Append(Max);
        }
        return sb.ToString();
    }
}