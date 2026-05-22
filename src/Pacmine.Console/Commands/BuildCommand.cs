using System.CommandLine;
using System.Threading.Channels;
using Lua;
using Pacmine.Core;
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
            ConsoleHelper.WriteError("Recipe file not found: " + pathToLua);
            return;
        }

        ConsoleHelper.WriteInfo("=> Preparing recipe...");

        var script = await File.ReadAllTextAsync(pathToLua);

        // 2. Resolve working directory
        var wd = workingDirectory ?? Directory.GetCurrentDirectory();

        // 3. Create and configure the factory
        using var factory = new PackageBuilderFactory()
            .ConfigureWorkingDirectory(wd)
            .ConfigureGit(git)
            .ConfigureShellExecution(allowShell)
            .ConfigureArbitraryFileOperation(unsafeFilesys);

        // 4. Load recipe
        var recipe = await factory.LoadRecipeAsync(script);

        // 5. Create builder
        using var builder = factory.CreateBuilder();

        // Start background consumers that pipe Lua stdout/stderr to the console in real time.
        // The channels are completed when builder.Dispose() is called by the 'using' statement.
        var stdoutConsumer = Task.Run(() => ConsumeStdoutAsync(builder));
        var stderrConsumer = Task.Run(() => ConsumeStderrAsync(builder));

        // 6. Execute the pipeline
        try
        {
            builder.InitializeDirectories();

            // Fetch every source
            ConsoleHelper.WriteInfo("=> Fetching sources...");
            await IterateSourcesAsync(recipe.Sources, async (i, name) =>
            {
                ConsoleHelper.Write($"[{i + 1}/{recipe.Sources.Count}]: {name}");
                await builder.FetchSourceAsync(i);
            });

            // Verify every source
            ConsoleHelper.WriteInfo("=> Verifying sources...");
            await IterateSourcesAsync(recipe.Sources, async (i, name) =>
            {
                ConsoleHelper.WriteInline($"[{i + 1}/{recipe.Sources.Count}]({name}): ");
                if (!await builder.VerifySourceAsync(i))
                {
                    ConsoleHelper.WriteInline("Failed!\n", ConsoleColor.Red);
                    ConsoleHelper.WriteError("Some source files failed to verify. Build aborted.");
                    System.Environment.Exit(1);
                }
                ConsoleHelper.WriteInline("Passed\n", ConsoleColor.Green);
            });

            // Call Lua functions (undefined functions are ignored)
            await InvokeVoidLuaFunctionAsync("prepare", recipe.LuaFuncPrepare,
                builder.InvokePrepareAsync);

            await InvokeGetVersionLuaFunctionAsync(recipe.LuaFuncGetVersion,
                builder.InvokeGetVersionAsync);

            await InvokeVoidLuaFunctionAsync("build", recipe.LuaFuncBuild,
                builder.InvokeBuildAsync);

            await InvokeCheckLuaFunctionAsync(recipe.LuaFuncCheck,
                builder.InvokeCheckAsync);

            await InvokeVoidLuaFunctionAsync("package", recipe.LuaFuncPackage,
                builder.InvokePackageAsync);

            // Compress package
            ConsoleHelper.WriteInfo("=> Compressing Package...");
            var packagePath = await builder.CompressPackageAsync();
            ConsoleHelper.WriteSuccess($"Successfully created package {recipe.Meta.Name} at {packagePath}");
            builder.Dispose();
        }
        finally
        {
            // Clean up
            if (!noClean)
            {
                builder.CleanUp();
            }
        }

        // Wait for the output consumers to finish processing remaining messages
        // (builder.Dispose() called by 'using' above completes the channels).
        await Task.WhenAll(stdoutConsumer, stderrConsumer);
    }

    /// <summary>
    /// Iterates over a list of source entries, applying an action to each.
    /// </summary>
    private static async Task IterateSourcesAsync(
        List<string> sources,
        Func<int, string, Task> action)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            await action(i, sources[i]);
        }
    }

    /// <summary>
    /// Invokes a void-returning Lua function (prepare, build, package) with the standard
    /// section header and defined/undefined messaging.
    /// </summary>
    private static async Task InvokeVoidLuaFunctionAsync(
        string name,
        LuaFunction? func,
        Func<Task<bool>> invokeAsync)
    {
        ConsoleHelper.WriteInline($"=> {name}(): ", ConsoleColor.Cyan);
        if (func == null)
        {
            ConsoleHelper.WriteInline("Undefined, skipped\n", ConsoleColor.Cyan);
        }
        else
        {
            ConsoleHelper.WriteInline("Executing...\n", ConsoleColor.Cyan);
            await invokeAsync();
        }
    }

    /// <summary>
    /// Invokes the get_version Lua function and handles its result.
    /// Prints an error and exits if the function fails to return a valid version.
    /// </summary>
    private static async Task InvokeGetVersionLuaFunctionAsync(
        LuaFunction? func,
        Func<Task<Tuple<bool, VersionIdentifier?>>> invokeAsync)
    {
        ConsoleHelper.WriteInline("=> get_version(): ", ConsoleColor.Cyan);
        if (func == null)
        {
            ConsoleHelper.WriteInline("Undefined, skipped\n", ConsoleColor.Cyan);
            return;
        }

        ConsoleHelper.WriteInline("Executing...\n", ConsoleColor.Cyan);
        var res = await invokeAsync();
        if (res.Item2 == null)
        {
            ConsoleHelper.WriteError("Error: get_version() failed to return a valid value. Build aborted.");
            System.Environment.Exit(1);
        }

        ConsoleHelper.WriteInfo($"Package version updated to {res.Item2}");
    }

    /// <summary>
    /// Invokes the check Lua function and handles its result.
    /// Prints an error and exits if the function fails or returns false.
    /// </summary>
    private static async Task InvokeCheckLuaFunctionAsync(
        LuaFunction? func,
        Func<Task<Tuple<bool, bool?>>> invokeAsync)
    {
        ConsoleHelper.WriteInline("=> check(): ", ConsoleColor.Cyan);
        if (func == null)
        {
            ConsoleHelper.WriteInline("Undefined, skipped\n", ConsoleColor.Cyan);
            return;
        }

        ConsoleHelper.WriteInline("Executing...\n", ConsoleColor.Cyan);
        var res = await invokeAsync();
        if (res.Item2 == null)
        {
            ConsoleHelper.WriteError("Error: check() failed to return a valid value. Build aborted.");
            System.Environment.Exit(1);
        }
        else if (res.Item2 == false)
        {
            ConsoleHelper.WriteError("Error: check() returned false. Build aborted.");
            System.Environment.Exit(1);
        }
    }

    /// <summary>
    /// Consumes messages from the builder's stdout channel and writes them to the console.
    /// Runs as a background task during the build pipeline.
    /// </summary>
    private static async Task ConsumeStdoutAsync(PackageBuilder builder)
    {
        await foreach (var message in builder.StdoutReader.ReadAllAsync())
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Consumes messages from the builder's stderr channel and writes them to the console error stream.
    /// Runs as a background task during the build pipeline.
    /// </summary>
    private static async Task ConsumeStderrAsync(PackageBuilder builder)
    {
        await foreach (var message in builder.StderrReader.ReadAllAsync())
        {
            ConsoleHelper.WriteError(message);
        }
    }
}
