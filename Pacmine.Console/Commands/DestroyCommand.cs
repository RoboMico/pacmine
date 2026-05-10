using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class DestroyCommand
{
    public static Command Create()
    {
        var dirArg = new Argument<string?>("dir", () => null, "The directory of the root, defaults to shell's pwd");
        var keepOpt = new Option<bool>(new[] { "--keep", "-k" }, () => false, "Remove the root but keep the package files");

        var cmd = new Command("destroy", "Completely uninstall all packages and destroy the root")
        {
            dirArg,
            keepOpt
        };

        cmd.SetHandler(ExecuteAsync, dirArg, keepOpt);
        return cmd;
    }

    private static async Task ExecuteAsync(string? dir, bool keep)
    {
        throw new NotImplementedException();
    }
}
