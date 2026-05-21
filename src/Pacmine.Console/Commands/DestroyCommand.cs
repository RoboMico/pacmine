using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class DestroyCommand
{
    public static Command Create()
    {
        var dirArg = new Argument<string?>("dir")
        {
            Description = "The directory of the root, defaults to shell's pwd",
            DefaultValueFactory = _ => null
        };
        var keepOpt = new Option<bool>("--keep", ["-k"])
        {
            Description = "Remove the root but keep the package files",
            DefaultValueFactory = _ => false
        };

        var cmd = new Command("destroy", "Completely uninstall all packages and destroy the root")
        {
            dirArg,
            keepOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var dir = parseResult.GetValue(dirArg);
            var keep = parseResult.GetValue(keepOpt);
            await ExecuteAsync(dir, keep);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(string? dir, bool keep)
    {
        throw new NotImplementedException();
    }
}
