using System.CommandLine;
using Pacmine.Environment;

namespace Pacmine.Console.Commands;

using Console = System.Console;

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
        dir ??= System.Environment.CurrentDirectory;
        bool success = true;
        try
        {
            using var _ = PacmineEnvironment.Create(dir);
        }
        catch (Exception e)
        {
            ConsoleHelper.WriteError($"Failed to initialize {dir}: {e.Message}");
            success = false;
        }
        if (!success)
        {
            System.Environment.Exit(1);
        }
        if (!skipOnboard)
        {
            ConsoleHelper.WriteWarning("Onboard Wizard is still under construction! Maybe come back and check later?");
        }
        ConsoleHelper.WriteSuccess($"Pacmine environment initialized successfully at {dir}");
    }
}
