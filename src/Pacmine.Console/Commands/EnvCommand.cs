using System.CommandLine;
using Pacmine.Core;
using Pacmine.Environment;

namespace Pacmine.Console.Commands;

using Console = System.Console;

internal static class EnvCommand
{
    public static Command Create()
    {
        var envCmd = new Command("env", "Manage environment packages");

        var rootOpt = new Option<string?>("--root", ["-r"])
        {
            Description = "Set the root directory for the operation (default is shell's pwd)",
            DefaultValueFactory = _ => null
        };

        var nameArg = new Argument<string>("name")
        {
            Description = "The name of the environment package"
        };
        var versionArg = new Argument<string>("version")
        {
            Description = "The version of the environment package"
        };
        var setCmd = new Command("set", "Add an env package (if a new one) or change its version")
        {
            nameArg,
            versionArg,
            rootOpt
        };
        setCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetValue(nameArg);
            var version = parseResult.GetValue(versionArg);
            var root = parseResult.GetValue(rootOpt);
            await ExecuteSetAsync(name, version, root);
        });

        var unsetNameArg = new Argument<string>("name")
        {
            Description = "The name of the environment package to remove"
        };
        var unsetCmd = new Command("unset", "Remove an env package")
        {
            unsetNameArg,
            rootOpt
        };
        unsetCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetValue(unsetNameArg);
            var root = parseResult.GetValue(rootOpt);
            await ExecuteUnsetAsync(name, root);
        });

        envCmd.Add(setCmd);
        envCmd.Add(unsetCmd);

        return envCmd;
    }

    private static async Task ExecuteSetAsync(string? name, string? version, string? root)
    {
        // ── Guard clauses ─────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(name))
        {
            ConsoleHelper.WriteError("Package name is required.");
            System.Environment.Exit(1);
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            ConsoleHelper.WriteError("Package version is required.");
            System.Environment.Exit(1);
        }

        if (!PackageMeta.PackageNameRegex.IsMatch(name!))
        {
            ConsoleHelper.WriteError(
                $"Invalid package name: '{name}'. Package names must start with a letter or digit " +
                "and contain only lowercase letters, digits, hyphens, underscores, and periods.");
            System.Environment.Exit(1);
        }

        using var env = CommandHelper.AccessEnvironment(root);

        var newMeta = new PackageMeta
        {
            Name = name!,
            Version = new VersionIdentifier(version!),
            Description = $"environment package {name}",
            Category = "env"
        };
        var newVersionStr = newMeta.Version.ToString();

        var existingMetas = env.PackageRegistry.Values.Select(r => r.Meta).ToArray();

        if (env.PackageRegistry.TryGetValue(name!, out var existing))
        {
            // ── UPDATE PATH ───────────────────────────────────────────
            if (existing.InstallReason != InstallReasons.Environment)
            {
                ConsoleHelper.WriteError(
                    $"Package '{name}' is already installed as a regular package (InstallReason: {existing.InstallReason}). " +
                    "Use 'uninstall' to remove it first.");
                env.Dispose();
                System.Environment.Exit(1);
            }

            var oldVersionStr = existing.Meta.Version.ToString();

            // No-op: same version
            if (existing.Meta.Version.IsEquivalentTo(newMeta.Version) &&
                existing.Meta.Epoch == newMeta.Epoch &&
                existing.Meta.Release == newMeta.Release)
            {
                ConsoleHelper.WriteWarning($"Environment package '{name}' is already at version {oldVersionStr}.");
                return;
            }

            // Build simulated base: existing packages minus the old version, plus the new version
            var simulatedBase = existingMetas
                .Where(m => m.Name != name)
                .Append(newMeta)
                .ToArray();

            var reasons = PackageRelationUtil.CheckSet(simulatedBase);
            if (reasons.Length > 0)
            {
                CommandHelper.PrintInvalidReasons(reasons);
                env.Dispose();
                System.Environment.Exit(1);
            }

            // Print plan
            ConsoleHelper.WriteInfo($"Setting env package:");
            Console.Write("  ");
            ConsoleHelper.WriteInline(name!, ConsoleColor.White);
            Console.Write("  ");
            ConsoleHelper.WriteInline(oldVersionStr, ConsoleColor.DarkGray);
            Console.Write(" → ");
            ConsoleHelper.WriteInline(newVersionStr, ConsoleColor.Green);
            Console.WriteLine();

            if (!CommandHelper.ConfirmPrompt())
            {
                ConsoleHelper.WriteWarning("Operation cancelled.");
                env.Dispose();
                return;
            }

            var success = env.TryWriteRegistry(new PackageRegistry
            {
                Meta = newMeta,
                FileList = [],
                InstallReason = InstallReasons.Environment,
                InstalledTime = DateTime.UtcNow
            });

            if (!success)
            {
                ConsoleHelper.WriteError(
                    $"Failed to write registry for '{name}'. The environment may be in an inconsistent state. Run 'repair' to fix.");
                env.Dispose();
                System.Environment.Exit(1);
            }

            ConsoleHelper.WriteSuccess($"Environment package '{name}' updated: {oldVersionStr} → {newVersionStr}.");
        }
        else
        {
            // ── CREATE PATH ───────────────────────────────────────────
            var reasons = PackageRelationUtil.CheckAdd(existingMetas, [newMeta]);
            if (reasons.Length > 0)
            {
                CommandHelper.PrintInvalidReasons(reasons);
                env.Dispose();
                System.Environment.Exit(1);
            }

            // Print plan
            ConsoleHelper.WriteInfo($"Setting env package:");
            Console.Write("  ");
            ConsoleHelper.WriteInline(name!, ConsoleColor.White);
            Console.Write("  ");
            ConsoleHelper.WriteInline("(new)", ConsoleColor.DarkGray);
            Console.Write(" → ");
            ConsoleHelper.WriteInline(newVersionStr, ConsoleColor.Green);
            Console.WriteLine();

            if (!CommandHelper.ConfirmPrompt())
            {
                ConsoleHelper.WriteWarning("Operation cancelled.");
                env.Dispose();
                return;
            }

            var success = env.TryWriteRegistry(new PackageRegistry
            {
                Meta = newMeta,
                FileList = [],
                InstallReason = InstallReasons.Environment,
                InstalledTime = DateTime.UtcNow
            });

            if (!success)
            {
                ConsoleHelper.WriteError(
                    $"Failed to write registry for '{name}'. The environment may be in an inconsistent state. Run 'repair' to fix.");
                env.Dispose();
                System.Environment.Exit(1);
            }

            ConsoleHelper.WriteSuccess($"Environment package '{name}' set to {newVersionStr}.");
        }

        await Task.CompletedTask;
    }

    private static async Task ExecuteUnsetAsync(string? name, string? root)
    {
        // ── Guard clauses ─────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(name))
        {
            ConsoleHelper.WriteError("Package name is required.");
            System.Environment.Exit(1);
        }

        using var env = CommandHelper.AccessEnvironment(root);

        if (!env.PackageRegistry.TryGetValue(name!, out var existing))
        {
            ConsoleHelper.WriteError($"Package '{name}' is not installed.");
            env.Dispose();
            System.Environment.Exit(1);
        }

        if (existing.InstallReason != InstallReasons.Environment)
        {
            ConsoleHelper.WriteError(
                $"Package '{name}' is not an environment package (InstallReason: {existing.InstallReason}). " +
                "Use 'uninstall' to remove it.");
            env.Dispose();
            System.Environment.Exit(1);
        }

        // Check that removal won't break dependencies
        var existingMetas = env.PackageRegistry.Values.Select(r => r.Meta).ToArray();
        var reasons = PackageRelationUtil.CheckRemove(existingMetas, [name!]);
        if (reasons.Length > 0)
        {
            ConsoleHelper.WriteError("Cannot remove environment package: other packages depend on it:");
            CommandHelper.PrintInvalidReasons(reasons);
            env.Dispose();
            System.Environment.Exit(1);
        }

        var versionStr = existing.Meta.Version.ToString();

        // Print plan
        ConsoleHelper.WriteInfo($"Removing env package:");
        Console.Write("  ");
        ConsoleHelper.WriteInline(name!, ConsoleColor.White);
        Console.Write("  ");
        ConsoleHelper.WriteInline(versionStr, ConsoleColor.Cyan);
        Console.WriteLine();

        if (!CommandHelper.ConfirmPrompt())
        {
            ConsoleHelper.WriteWarning("Operation cancelled.");
            env.Dispose();
            return;
        }

        var success = env.TryRemoveRegistry(name!);
        if (!success)
        {
            ConsoleHelper.WriteError(
                $"Failed to remove registry entry for '{name}'. " +
                "The environment may be in an inconsistent state. Run 'repair' to fix.");
            env.Dispose();
            System.Environment.Exit(1);
        }

        ConsoleHelper.WriteSuccess($"Environment package '{name}' removed.");
        await Task.CompletedTask;
    }
}
