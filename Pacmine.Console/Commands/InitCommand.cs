using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class InitCommand
{
    public static Command Create()
    {
        var dirArg = new Argument<string?>("dir", () => null, "The directory to init as the root, defaults to shell's pwd");
        var skipOnboardOpt = new Option<bool>(new[] { "--skip-onboard", "-s" }, () => false, "Skip the onboard wizard");

        var cmd = new Command("init", "Init a game instance folder as pacmine root")
        {
            dirArg,
            skipOnboardOpt
        };

        cmd.SetHandler(ExecuteAsync, dirArg, skipOnboardOpt);
        return cmd;
    }

    private static async Task ExecuteAsync(string? dir, bool skipOnboard)
    {
        throw new NotImplementedException();
    }
}
