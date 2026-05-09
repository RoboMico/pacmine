using System.Text;

namespace Pacmine.Models;

/// <summary>
/// Represents a version range expression.
/// Supports operators <c>&gt;=</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&lt;</c>, exact match, and wildcard <c>*</c>.
/// </summary>
public class VersionRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionRange"/> class by parsing the specified range expression.
    /// </summary>
    /// <param name="range">The version range expression string.</param>
    /// <exception cref="InvalidDataException">Thrown when the range expression is invalid.</exception>
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

    /// <summary>
    /// Gets or sets the minimum version bound. <c>null</c> represents negative infinity.
    /// </summary>
    public VersionIdentifier? Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum version bound. <c>null</c> represents positive infinity.
    /// </summary>
    public VersionIdentifier? Max { get; set; }

    /// <summary>
    /// Gets or sets whether the minimum bound is inclusive.
    /// </summary>
    public bool MinEquals { get; set; }

    /// <summary>
    /// Gets or sets whether the maximum bound is inclusive.
    /// </summary>
    public bool MaxEquals { get; set; }

    /// <summary>
    /// A version range that matches any version (<c>*</c>).
    /// </summary>
    public readonly static VersionRange Any = new("*");

    /// <summary>
    /// Determines whether the specified version falls within this range.
    /// </summary>
    /// <param name="version">The version to check.</param>
    /// <returns><c>true</c> if the version is contained within the range; otherwise, <c>false</c>.</returns>
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

    /// <summary>
    /// Returns the string representation of the version range expression.
    /// </summary>
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
