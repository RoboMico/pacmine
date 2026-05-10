using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class BuildCommand
{
    public static Command Create()
    {
        var pathToLuaArg = new Argument<string>("pathToLua", "The path to the Lua recipe file");
        var workingDirOpt = new Option<string?>(["--working-directory", "-w"], () => null, "Set the working directory for the build (default is shell's pwd)");
        var installOpt = new Option<bool>(["--install", "-i"], () => false, "Immediately install the package after building, skipping the compressing/uncompressing");
        var noCleanOpt = new Option<bool>(["--no-clean", "-n"], () => false, "Skip cleaning up");

        var cmd = new Command("build", "Build a package with PackageCraft")
        {
            pathToLuaArg,
            workingDirOpt,
            installOpt,
            noCleanOpt
        };

        cmd.SetHandler(ExecuteAsync, pathToLuaArg, workingDirOpt, installOpt, noCleanOpt);
        return cmd;
    }

    private static async Task ExecuteAsync(string pathToLua, string? workingDirectory, bool install, bool noClean)
    {
        throw new NotImplementedException();
    }
}
