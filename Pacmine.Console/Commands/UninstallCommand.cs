using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class UninstallCommand
{
    public static Command Create()
    {
        var pkgNameListArg = new Argument<string[]>("pkgNameList", "The package name(s) to uninstall");

        var cmd = new Command("uninstall", "Uninstall a package")
        {
            pkgNameListArg
        };
        cmd.AddAlias("remove");

        cmd.SetHandler(ExecuteAsync, pkgNameListArg);
        return cmd;
    }

    private static async Task ExecuteAsync(string[] pkgNameList)
    {
        throw new NotImplementedException();
    }
}
