using System.CommandLine;
using Pacmine.Environment;

namespace Pacmine.Console.Commands;

using Console = System.Console;

internal static class ListCommand
{
    public static Command Create()
    {
        var verboseOpt = new Option<bool>("--verbose", ["-v"])
        {
            Description = "Show more info about each package",
            DefaultValueFactory = _ => false
        };

        var rootOpt = new Option<string?>("--root", ["-r"])
        {
            Description = "Set the root directory for the list (default is shell's pwd)",
            DefaultValueFactory = _ => null
        };

        var cmd = new Command("list", "List all installed packages")
        {
            verboseOpt,
            rootOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var verbose = parseResult.GetValue(verboseOpt);
            var root = parseResult.GetValue(rootOpt);
            await ExecuteAsync(verbose, root);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(bool verbose, string? root)
    {
        using var env = CommandHelper.AccessEnvironment(root);

        var packages = env.Registry.GetAll().Values
            .OrderBy(p => p.Meta.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (packages.Length == 0)
        {
            ConsoleHelper.WriteInfo("No packages installed.");
            return;
        }

        if (verbose)
        {
            PrintVerbose(packages);
        }
        else
        {
            PrintDefault(packages);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Prints each package as a plain-text line "name version", color-coded by InstallReason.
    /// </summary>
    private static void PrintDefault(PackageRegistry[] packages)
    {
        foreach (var reg in packages)
        {
            PrintNameWithColor(reg);
            Console.WriteLine($" {reg.Meta.GetFullVersionString()}");
        }
    }

    /// <summary>
    /// Prints each package as a multi-line block (like apt show / pip show),
    /// showing all key metadata fields.
    /// </summary>
    private static void PrintVerbose(PackageRegistry[] packages)
    {
        for (int i = 0; i < packages.Length; i++)
        {
            var reg = packages[i];
            var meta = reg.Meta;

            PrintField("Name", meta.Name);
            PrintField("Version", meta.GetFullVersionString());

            if (!string.IsNullOrEmpty(meta.Category))
                PrintField("Category", meta.Category);

            PrintField("Description", meta.Description);
            PrintField("License", meta.License);
            PrintField("UpstreamUrl", meta.UpstreamUrl);
            PrintField("InstallReason", reg.InstallReason.ToString());
            PrintField("InstalledTime", reg.InstalledTime.ToString("u"));
            PrintField("PackagedTime", reg.PackagedTime.ToString("u"));
            PrintField("FileCount", reg.FileList.Count.ToString());

            if (meta.Depends.Count > 0)
            {
                var deps = string.Join(", ", meta.Depends.Select(kv => $"{kv.Key} ({kv.Value})"));
                PrintField("Depends", deps);
            }

            if (meta.Provides.Count > 0)
            {
                var provides = string.Join(", ", meta.Provides.Select(kv => $"{kv.Key} ({kv.Value})"));
                PrintField("Provides", provides);
            }

            if (meta.Conflicts.Count > 0)
            {
                var conflicts = string.Join(", ", meta.Conflicts.Select(kv => $"{kv.Key} ({kv.Value})"));
                PrintField("Conflicts", conflicts);
            }

            if (meta.Replaces.Count > 0)
            {
                var replaces = string.Join(", ", meta.Replaces.Select(kv => $"{kv.Key} ({kv.Value})"));
                PrintField("Replaces", replaces);
            }

            if (meta.Recommends.Count > 0)
            {
                var recommends = string.Join(", ", meta.Recommends.Select(kv => $"{kv.Key}"));
                PrintField("Recommends", recommends);
            }

            if (meta.Groups.Count > 0)
                PrintField("Groups", string.Join(", ", meta.Groups));

            // Separator between packages
            if (i < packages.Length - 1)
                Console.WriteLine();
        }
    }

    /// <summary>
    /// Prints the package name to stdout with color based on <see cref="InstallReasons"/>:
    ///   - Explicit: default color
    ///   - AsDependency: dark gray (dimmed)
    ///   - Environment: cyan
    /// </summary>
    private static void PrintNameWithColor(PackageRegistry reg)
    {
        var color = reg.InstallReason switch
        {
            InstallReasons.AsDependency => ConsoleColor.DarkGray,
            InstallReasons.Environment => ConsoleColor.Cyan,
            _ => ConsoleColor.White
        };

        ConsoleHelper.WriteInline(reg.Meta.Name, color);
    }

    /// <summary>
    /// Prints a single "Key: Value" line for verbose output, with the key in highlighted white.
    /// </summary>
    private static void PrintField(string key, string value)
    {
        ConsoleHelper.WriteInline($"{key,-16}", ConsoleColor.White);
        Console.WriteLine(value);
    }
}
