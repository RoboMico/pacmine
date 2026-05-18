using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class UninstallCommand
{
    public static Command Create()
    {
        var pkgNameListArg = new Argument<string[]>("pkgNameList")
        {
            Description = "The package name(s) to uninstall"
        };

        var cmd = new Command("uninstall", "Uninstall a package");
        cmd.Aliases.Add("remove");
        cmd.Add(pkgNameListArg);
        cmd.SetAction(async (parseResult) =>
        {
            var pkgNames = parseResult.GetValue(pkgNameListArg);
            await ExecuteAsync(pkgNames);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(string[]? pkgNameList)
    {
        throw new NotImplementedException();
    }
}
