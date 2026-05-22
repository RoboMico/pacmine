namespace Pacmine.Console;

/// <summary>
/// Provides styled console output methods for consistent CLI messaging.
/// All methods automatically reset the console color after writing.
/// </summary>
internal static class ConsoleHelper
{
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
}
