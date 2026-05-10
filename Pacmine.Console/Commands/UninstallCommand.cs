using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class UninstallCommand
{
    public static Command Create()
    {
        var pkgNameListArg = new Argument<string[]>("pkgNameList", "The package name(s) to uninstall");
        var purgeOpt = new Option<bool>(new[] { "--purge", "-p" }, () => false, "Do not keep the \"variable files\" (usually config files) owned by the package");

        var cmd = new Command("uninstall", "Uninstall a package")
        {
            pkgNameListArg,
            purgeOpt
        };
        cmd.AddAlias("remove");

        cmd.SetHandler(ExecuteAsync, pkgNameListArg, purgeOpt);
        return cmd;
    }

    private static async Task ExecuteAsync(string[] pkgNameList, bool purge)
    {
        throw new NotImplementedException();
    }
}
