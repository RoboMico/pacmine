using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class InitCommand
{
    public static Command Create()
    {
        var dirArg = new Argument<string?>("dir")
        {
            Description = "The directory to init as the root, defaults to shell's pwd",
            DefaultValueFactory = _ => null
        };
        var skipOnboardOpt = new Option<bool>("--skip-onboard", ["-s"])
        {
            Description = "Skip the onboard wizard",
            DefaultValueFactory = _ => false
        };

        var cmd = new Command("init", "Init a game instance folder as pacmine root")
        {
            dirArg,
            skipOnboardOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var dir = parseResult.GetValue(dirArg);
            var skipOnboard = parseResult.GetValue(skipOnboardOpt);
            await ExecuteAsync(dir, skipOnboard);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(string? dir, bool skipOnboard)
    {
        throw new NotImplementedException();
    }
}
