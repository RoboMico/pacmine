using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class ListCommand
{
    public static Command Create()
    {
        var verboseOpt = new Option<bool>("--verbose", ["-v"])
        {
            Description = "Show more info about each package",
            DefaultValueFactory = _ => false
        };

        var cmd = new Command("list", "List all installed packages")
        {
            verboseOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var verbose = parseResult.GetValue(verboseOpt);
            await ExecuteAsync(verbose);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(bool verbose)
    {
        throw new NotImplementedException();
    }
}
