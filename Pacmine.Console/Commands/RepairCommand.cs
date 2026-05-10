using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class RepairCommand
{
    public static Command Create()
    {
        var dirArg = new Argument<string?>("dir", () => null, "The directory of the root, defaults to shell's pwd");
        var printOpt = new Option<bool>(new[] { "--print", "-p" }, () => false, "Do not deploy the repair, print the check report only");

        var cmd = new Command("repair", "Repair all the packages")
        {
            dirArg,
            printOpt
        };

        cmd.SetHandler(ExecuteAsync, dirArg, printOpt);
        return cmd;
    }

    private static async Task ExecuteAsync(string? dir, bool print)
    {
        throw new NotImplementedException();
    }
}
