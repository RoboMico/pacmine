using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class InstallCommand
{
    public static Command Create()
    {
        var pkgNameListArg = new Argument<string[]>("pkgNameList", "The package name(s) to install");
        var rootOpt = new Option<string?>(new[] { "--root", "-r" }, () => null, "Set the root directory for the install (default is shell's pwd)");
        var localOpt = new Option<bool>(new[] { "--local", "-l" }, () => false, "Install a local package (treat the package name as the path to the package)");

        var cmd = new Command("install", "Install a remote/local package")
        {
            pkgNameListArg,
            rootOpt,
            localOpt
        };

        cmd.SetHandler(ExecuteAsync, pkgNameListArg, rootOpt, localOpt);
        return cmd;
    }

    private static async Task ExecuteAsync(string[] pkgNameList, string? root, bool local)
    {
        throw new NotImplementedException();
    }
}
