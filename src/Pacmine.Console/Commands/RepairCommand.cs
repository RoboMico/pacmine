using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class RepairCommand
{
    public static Command Create()
    {
        var dirArg = new Argument<string?>("dir")
        {
            Description = "The directory of the root, defaults to shell's pwd",
            DefaultValueFactory = _ => null
        };
        var printOpt = new Option<bool>("--print", ["-p"])
        {
            Description = "Do not deploy the repair, print the check report only",
            DefaultValueFactory = _ => false
        };

        var cmd = new Command("repair", "Repair all the packages")
        {
            dirArg,
            printOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var dir = parseResult.GetValue(dirArg);
            var print = parseResult.GetValue(printOpt);
            await ExecuteAsync(dir, print);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(string? dir, bool print)
    {
        throw new NotImplementedException();
    }
}
