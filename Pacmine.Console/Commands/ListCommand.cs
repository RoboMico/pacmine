using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class ListCommand
{
    public static Command Create()
    {
        var verboseOpt = new Option<bool>(new[] { "--verbose", "-v" }, () => false, "Show more info about each package");

        var cmd = new Command("list", "List all installed packages")
        {
            verboseOpt
        };

        cmd.SetHandler(ExecuteAsync, verboseOpt);
        return cmd;
    }

    private static async Task ExecuteAsync(bool verbose)
    {
        throw new NotImplementedException();
    }
}
