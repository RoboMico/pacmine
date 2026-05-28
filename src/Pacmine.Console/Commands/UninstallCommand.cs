using System.CommandLine;
using Pacmine.Core;
using Pacmine.Environment;

namespace Pacmine.Console.Commands;

using Console = System.Console;

internal static class UninstallCommand
{
    public static Command Create()
    {
        var pkgNameListArg = new Argument<string[]>("pkgNameList")
        {
            Description = "The package name(s) to uninstall",
            Arity = ArgumentArity.OneOrMore
        };
        var rootOpt = new Option<string?>("--root", ["-r"])
        {
            Description = "Set the root directory for the operation (default is shell's pwd)",
            DefaultValueFactory = _ => null
        };

        var cmd = new Command("uninstall", "Uninstall a package")
        {
            pkgNameListArg,
            rootOpt
        };
        cmd.Aliases.Add("remove");
        cmd.SetAction(async (parseResult) =>
        {
            var pkgNames = parseResult.GetValue(pkgNameListArg);
            var root = parseResult.GetValue(rootOpt);
            await ExecuteAsync(pkgNames, root);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(string[]? pkgNameList, string? root)
    {
        pkgNameList ??= [];

        using var env = CommandHelper.AccessEnvironment(root);

        // ═══════════════════════════════════════════════════════════════
        // Phase 1: Validate that all requested packages exist
        // ═══════════════════════════════════════════════════════════════
        ConsoleHelper.WriteInfo("Validating packages...");

        var notFound = pkgNameList.Where(n => !env.Registry.Contains(n)).ToArray();
        if (notFound.Length > 0)
        {
            foreach (var name in notFound)
                ConsoleHelper.WriteError($"Package not installed: {name}");
            env.Dispose();
            System.Environment.Exit(1);
        }

        // Collect metadata for the packages to remove
        var toRemove = pkgNameList.Select(n => env.Registry.TryGet(n)!).ToList();
        var toRemoveNames = pkgNameList.ToHashSet();

        // ═══════════════════════════════════════════════════════════════
        // Phase 2: Check that removal won't break remaining packages
        // ═══════════════════════════════════════════════════════════════
        var existingMetas = env.Registry.GetAllMetas();
        var removeReasons = PackageRelationUtil.CheckRemove(existingMetas, pkgNameList);

        if (removeReasons.Length > 0)
        {
            ConsoleHelper.WriteError("Cannot uninstall: some packages are required by others:");
            foreach (var reason in removeReasons)
            {
                if (reason is MissingDependsInvalidReason m)
                {
                    // m.TargetPackageName is the depender, m.MissingDependName is the removed package
                    ConsoleHelper.WriteError(
                        $"  {m.TargetPackageName} depends on {m.MissingDependName} {m.DesiredVersions}");
                }
            }
            env.Dispose();
            System.Environment.Exit(1);
        }

        // ═══════════════════════════════════════════════════════════════
        // Phase 3: Determine removal order (reverse topological sort)
        // ═══════════════════════════════════════════════════════════════
        // Sort: dependencies before dependents, then reverse → dependents removed first.
        var sorted = CommandHelper.TopologicalSort(
            toRemove,
            r => r.Meta.Name,
            r => r.Meta.Depends.Keys,
            toRemoveNames);
        sorted.Reverse();

        // ═══════════════════════════════════════════════════════════════
        // Phase 4: Print plan and confirm
        // ═══════════════════════════════════════════════════════════════
        PrintUninstallPlan(sorted);

        if (!CommandHelper.ConfirmPrompt())
        {
            ConsoleHelper.WriteWarning("Uninstall cancelled.");
            env.Dispose();
            return;
        }

        // ═══════════════════════════════════════════════════════════════
        // Phase 5: Remove packages in order
        // ═══════════════════════════════════════════════════════════════
        foreach (var registry in sorted)
        {
            var name = registry.Meta.Name;
            ConsoleHelper.WriteInfo($"Removing {name} {registry.Meta.GetFullVersionString()}...");

            // Remove all files owned by this package from disk
            var reg = env.Registry.TryGet(name);
            if (reg != null)
                FileManager.RemoveFiles(env.RootPath, reg.FileList.Keys);

            // Remove the registry entry
            if (!env.Registry.Remove(name))
            {
                ConsoleHelper.WriteError(
                    $"  Failed to remove registry entry for {name}. The files have been deleted but the registry update failed. Run 'repair' to fix.");
            }
        }
    }

    /// <summary>
    /// Prints a table of packages to be uninstalled.
    /// </summary>
    private static void PrintUninstallPlan(List<PackageRegistry> installOrder)
    {
        var rows = new List<ConsoleHelper.TableCell[]>();

        // Header row
        rows.Add([
            new ConsoleHelper.TableCell($"Package ({installOrder.Count})"),
            new ConsoleHelper.TableCell("Version")
        ]);

        // Data rows
        foreach (var reg in installOrder)
        {
            rows.Add([
                new ConsoleHelper.TableCell(reg.Meta.Name),
                new ConsoleHelper.TableCell(reg.Meta.GetFullVersionString())
            ]);
        }

        ConsoleHelper.PrintTable(2, rows, 3);
        Console.WriteLine();
    }
}
