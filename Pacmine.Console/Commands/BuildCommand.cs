using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class BuildCommand
{
    public static Command Create()
    {
        var pathToLuaArg = new Argument<string>("pathToLua")
        {
            Description = "The path to the Lua recipe file",
            Arity = ArgumentArity.ExactlyOne
        };
        var workingDirOpt = new Option<string?>("--working-directory", ["-w"])
        {
            Description = "Set the working directory for the build (default is shell's pwd)",
            DefaultValueFactory = _ => null
        };
        var installOpt = new Option<bool>("--install", ["-i"])
        {
            Description = "Immediately install the package after building, skipping the compressing/uncompressing",
            DefaultValueFactory = _ => false
        };
        var noCleanOpt = new Option<bool>("--no-clean", ["-n"])
        {
            Description = "Skip cleaning up",
            DefaultValueFactory = _ => false
        };

        var cmd = new Command("build", "Build a package with PackageCraft")
        {
            pathToLuaArg,
            workingDirOpt,
            installOpt,
            noCleanOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var pathToLua = parseResult.GetValue(pathToLuaArg);
            var workingDir = parseResult.GetValue(workingDirOpt);
            var install = parseResult.GetValue(installOpt);
            var noClean = parseResult.GetValue(noCleanOpt);
            await ExecuteAsync(pathToLua, workingDir, install, noClean);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(string? pathToLua, string? workingDirectory, bool install, bool noClean)
    {
        throw new NotImplementedException();
    }
}
