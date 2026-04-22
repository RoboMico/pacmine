using Pacmine;
using Pacmine.PackageCraft;

namespace Pacmine.Console;

/*
pacmine init <dir> - Init a game instance folder as pacmine root
pacmine uninstall <dir> - Completely uninstall all packages and destroy the root
pacmine compress - Compress a folder into a package(for test purpose only)
pacmine install - Install a remote/local package
pacmine build - Build a package with BuildYAML
pacmine uninstall - Uninstall a package
pacmine list - List all installed packages
*/

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = await PackageBuilder.CreateAsync(File.ReadAllText(args[0]));
        await builder.ConfigureWorkingDirector(Environment.CurrentDirectory).BuildAsync();
        return 0;
    }
}