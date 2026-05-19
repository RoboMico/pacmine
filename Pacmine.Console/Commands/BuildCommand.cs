using System.CommandLine;
using Pacmine.PackageCraft;

namespace Pacmine.Console.Commands;

using Console = System.Console;

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
        var gitOpt = new Option<string?>("--git", ["-g"])
        {
            Description = "Path to the Git executable. Defaults to 'git' on PATH.",
            DefaultValueFactory = _ => "git"
        };
        var allowShellOpt = new Option<bool>("--allow-shell", ["-s"])
        {
            Description = "Allow shell execution from Lua scripts.",
            DefaultValueFactory = _ => false
        };
        var unsafeFilesysOpt = new Option<bool>("--unsafe-filesys", ["-u"])
        {
            Description = "Allow arbitrary file system operations (outside src/pkg dirs).",
            DefaultValueFactory = _ => false
        };

        var cmd = new Command("build", "Build a package with PackageCraft")
        {
            pathToLuaArg,
            workingDirOpt,
            installOpt,
            noCleanOpt,
            gitOpt,
            allowShellOpt,
            unsafeFilesysOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var pathToLua = parseResult.GetValue(pathToLuaArg);
            var workingDir = parseResult.GetValue(workingDirOpt);
            var install = parseResult.GetValue(installOpt);
            var noClean = parseResult.GetValue(noCleanOpt);
            var git = parseResult.GetValue(gitOpt);
            var allowShell = parseResult.GetValue(allowShellOpt);
            var unsafeFilesys = parseResult.GetValue(unsafeFilesysOpt);
            await ExecuteAsync(pathToLua, workingDir, install, noClean, git, allowShell, unsafeFilesys);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(
        string? pathToLua,
        string? workingDirectory,
        bool install,
        bool noClean,
        string? git,
        bool allowShell,
        bool unsafeFilesys)
    {
        // 1. Read the Lua recipe
        if (pathToLua == null || !File.Exists(pathToLua))
        {
            Console.Error.WriteLine("Recipe file not found: " + pathToLua);
            return;
        }
        var script = await File.ReadAllTextAsync(pathToLua);

        // 2. Resolve working directory
        var wd = workingDirectory ?? Directory.GetCurrentDirectory();

        // 3. Create and configure the factory
        var factory = new PackageBuilderFactory()
            .ConfigureWorkingDirectory(wd)
            .ConfigureGit(git)
            .ConfigureShellExecution(allowShell)
            .ConfigureArbitraryFileOperation(unsafeFilesys);

        // 4. Load recipe
        var recipe = await factory.LoadRecipeAsync(script);

        // 5. Create builder
        var builder = factory.CreateBuilder(recipe);

        // 6. Execute the pipeline
        try
        {
            builder.InitializeDirectories();

            // Fetch every source
            for (int i = 0; i < recipe.Sources.Count; i++)
            {
                Console.WriteLine($"Fetching source [{i + 1}/{recipe.Sources.Count}]: {recipe.Sources[i]}");
                await builder.FetchSourceAsync(i);
            }

            // Verify every source
            for (int i = 0; i < recipe.Sources.Count; i++)
            {
                Console.WriteLine($"Verifying source [{i + 1}/{recipe.Sources.Count}]...");
                if (!await builder.VerifySourceAsync(i))
                {
                    Console.Error.WriteLine($"Source {i} checksum verification failed.");
                    return;
                }
            }

            // Call Lua functions (undefined functions are ignored)
            await builder.InvokePrepareAsync();
            await builder.InvokeGetVersionAsync();
            await builder.InvokeBuildAsync();
            await builder.InvokeCheckAsync();
            await builder.InvokePackageAsync();

            // Compress package
            await builder.CompressPackageAsync();
            Console.WriteLine("Package built successfully.");
        }
        finally
        {
            // Clean up
            if (!noClean)
            {
                await builder.CleanUpAsync();
            }
        }
    }
}
