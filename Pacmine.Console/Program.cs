using System.CommandLine;
using Pacmine.Console.Commands;

namespace Pacmine.Console;

/*
pacmine init [dir] - Init a game instance folder as pacmine root
    [dir] - The directory to init as the root, defaults to shell's pwd
    --skip-onboard, -s - Skip the onboard wizard
pacmine destroy [dir] - Completely uninstall all packages and destroy the root
    [dir] - The directory of the root, defaults to shell's pwd
    --keep, -k - Remove the root but keep the package files (package info registry is still lost)
pacmine install <pkgNameList> - Install a remote/local package
    --root, -r - Set the root directory for the install(default is shell's pwd)
    --local, -l - Install a local package(treat the package name as the path to the package)
pacmine build <pathToLua> - Build a package with PackageCraft
    --working-directory, -w - Set the working directory for the build(default is shell's pwd)
    --install, -i - Immediately install the package after building, skipping the compressing/uncompressing
    --no-clean, -n - Skip cleaning up
pacmine uninstall <pkgNameList> - Uninstall a package
    Alias: remove
pacmine list - List all installed packages
    --verbose, -v - Show more info about each package
    (TODO: need an idea for powerful advanced filtering and sorting functions)
pacmine env - Manage environment packages
(env packs are to track version info of game files that are not managed by pacmine but required by other packages,
such as minecraft installation, fabric/forge loader, etc.)
(env packs cannot be uninstalled using "pacmine uninstall")
(env packs contain no file and depend on no other packages)
(there is no need to maintain a separate env registry file, env packs are just essentially empty packages)
    pacmine env set <name> <version> - Add a env package(if a new one) or change its version
    pacmine env unset <name> - Remove a env package
pacmine repair [dir] - Repair all the packages
(check all files that are owned by a package; if a file is missing or failed the checksum, reinstall the package)
    [dir] - The directory of the root, defaults to shell's pwd
    --print, -p - Do not deploy the repair, print the check report only
pacmine help - Show help
pacmine --help - Show help
pacmine version - Show version info
pacmine --version - Show version info
*/

/*
The onboard wizard of "pacmine init" would look like this:

----

The game instance is getting ready to be managed using Pacmine:
    /the/path/to/.minecraft/version/26.1-fabric
It is recommended to:
    1. Clear all previously installed mods/resource packs/shaderpacks/etc. You may reinstall them using Pacmine later.
    2. Make sure you have enabled Isolated Working Directory for this game instance in your launcher, if you are using
       the official launcher or compatible ones(e.g. HMCL).

Now Pacmine would like to know about the environment information of this game instance. Press a key to continue:
    [a] - (Default)Let Pacmine detect it automatically
    [s] - Set it up manually by answering a few questions
    [k] - Skip this step

----

Does it looks right?
    Minecraft version: 26.1.2
    Fabric loader version: 0.18.4
    NeoForge loader version: <undetected>
    Java version: 25 (guessed)

If something is wrong, you can correct it by yourself by following the prompt in the next step. Press a key to continue:
    [y] - (Default)Go ahead
    [n] - Turn back and choose another method

----

Pacmine will initialize the following environment packages:
    minecraft 26.1.2
    fabric-loader 0.18.4
    java-jre 25

You can change the version of these packages or add new ones by using the command:
    pacmine env set <name> <version>
Remove an environment package by using the command:
    pacmine env unset <name>

Now press a key to confirm your choice:
    [y] - (Default)Proceed
    [n] - Turn back

----

Are you REALLY want to skip the environment setup? Ignoring it will cause most of the packages to fail to install.
You may manage the environment manually using command line:
    pacmine env set <name> <version>

Press a key to continue:
    [y] - I know what I'm doing!
    [n] - (Default)Turn back

----

*/

/// <summary>
/// Entry point for the Pacmine console application.
/// </summary>
public class Program
{
    /// <summary>
    /// The main entry point for the Pacmine CLI tool.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task that represents the asynchronous operation, returning the exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Pacmine - Minecraft Package Manager")
        {
            InitCommand.Create(),
            DestroyCommand.Create(),
            InstallCommand.Create(),
            BuildCommand.Create(),
            UninstallCommand.Create(),
            ListCommand.Create(),
            EnvCommand.Create(),
            RepairCommand.Create()
        };

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }
}
