using Wcwidth;

namespace Pacmine.Console;

/// <summary>
/// Provides styled console output methods for consistent CLI messaging.
/// </summary>
internal static class ConsoleHelper
{
    public class TableCell
    {
        private string _text;

        public TableCell(string text)
        {
            _text = text;
        }

        public virtual string Text { get => _text; }

        public virtual void Print(int width)
        {
            System.Console.Write(Text.PadRight(width));
        }
    };

    public class ColoredTableCell : TableCell
    {
        public record ColoredSegment(string Text, ConsoleColor Color);

        public ColoredTableCell(List<ColoredSegment> segments) : base("")
        {
            Segments = segments;
        }

        public List<ColoredSegment> Segments { get; set; } = [];

        public override string Text => string.Join("", Segments.Select(s => s.Text));

        public override void Print(int width)
        {
            foreach (var segment in Segments)
            {
                System.Console.ForegroundColor = segment.Color;
                System.Console.Write(segment.Text);
            }
            System.Console.ResetColor();
            System.Console.Write(new string(' ', width - Text.Length));
        }
    }

    /// <summary>
    /// Writes an informational message in cyan to stdout.
    /// </summary>
    public static void WriteInfo(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        System.Console.WriteLine(message);
        System.Console.ResetColor();
    }

    /// <summary>
    /// Writes a success message in green to stdout.
    /// </summary>
    public static void WriteSuccess(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine(message);
        System.Console.ResetColor();
    }

    /// <summary>
    /// Writes a warning message in yellow to stdout.
    /// </summary>
    public static void WriteWarning(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine(message);
        System.Console.ResetColor();
    }

    /// <summary>
    /// Writes an error message in red to stderr.
    /// </summary>
    public static void WriteError(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.Error.WriteLine(message);
        System.Console.ResetColor();
    }

    /// <summary>
    /// Writes a plain status message (no special color) to stdout.
    /// </summary>
    public static void Write(string message)
    {
        System.Console.WriteLine(message);
    }

    /// <summary>
    /// Writes a status message without a trailing newline, in the specified color.
    /// </summary>
    public static void WriteInline(string message, ConsoleColor color = ConsoleColor.Gray)
    {
        System.Console.ForegroundColor = color;
        System.Console.Write(message);
        System.Console.ResetColor();
    }

    public static void PrintTable(int columnCount, List<TableCell[]> cells, int padding = 1)
    {
        // validate rows
        foreach (var c in cells)
        {
            if (c.Length != columnCount)
            {
                throw new ArgumentException("All rows must have the same number of columns");
            }
        }

        // calculate width
        int[] columnWidth = new int[columnCount];
        for (int i = 0; i < cells.Count; i++)
        {
            for (int j = 0; j < columnCount; j++)
            {
                columnWidth[j] = Math.Max(columnWidth[j], UnicodeCalculator.GetWidth(cells[i][j].Text) + padding);
            }
        }
        int totalWidth = columnWidth.Sum();

        // print header (1st row)
        for (int j = 0; j < columnCount; j++)
        {
            cells[0][j].Print(columnWidth[j]);
        }
        System.Console.WriteLine();

        // print separator
        System.Console.WriteLine(new string('-', totalWidth));

        // print rows
        for (int i = 1; i < cells.Count; i++)
        {
            for (int j = 0; j < columnCount; j++)
            {
                cells[i][j].Print(columnWidth[j]);
            }
            System.Console.WriteLine();
        }
    }
}
