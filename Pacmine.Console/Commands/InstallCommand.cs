using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class InstallCommand
{
    public static Command Create()
    {
        var pkgNameListArg = new Argument<string[]>("pkgNameList")
        {
            Description = "The package name(s) to install",
            Arity = ArgumentArity.OneOrMore
        };
        var rootOpt = new Option<string?>("--root", ["-r"])
        {
            Description = "Set the root directory for the install (default is shell's pwd)",
            DefaultValueFactory = _ => null
        };
        var localOpt = new Option<bool>("--local", ["-l"])
        {
            Description = "Install local packages (treat the package name as the path to the package)",
            DefaultValueFactory = _ => false
        };

        var cmd = new Command("install", "Install a remote/local package")
        {
            pkgNameListArg,
            rootOpt,
            localOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var pkgNames = parseResult.GetValue(pkgNameListArg);
            var root = parseResult.GetValue(rootOpt);
            var local = parseResult.GetValue(localOpt);
            await ExecuteAsync(pkgNames, root, local);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(string[]? pkgNameList, string? root, bool local)
    {
        throw new NotImplementedException();
    }
}
