using Pacmine.Core;
using Pacmine.Environment;

namespace Pacmine.Console.Commands;

using Console = System.Console;

/// <summary>
/// Provides reusable utility methods shared across CLI command implementations.
/// </summary>
internal static class CommandHelper
{
    /// <summary>
    /// Resolves the root directory (defaults to shell's current directory),
    /// checks for an existing lock, and accesses the Pacmine environment.
    /// Exits the process if the directory is locked.
    /// </summary>
    /// <param name="root">The optional root directory. If null, uses <see cref="System.Environment.CurrentDirectory"/>.</param>
    /// <returns>An accessed and locked <see cref="PacmineEnvironment"/> instance.</returns>
    public static PacmineEnvironment AccessEnvironment(string? root)
    {
        root ??= System.Environment.CurrentDirectory;
        if (!Directory.Exists(Path.Combine(root, PacmineEnvironment.SPECIAL_FOLDER_NAME)))
        {
            ConsoleHelper.WriteError($"The directory {root} does not contain a Pacmine environment.");
            System.Environment.Exit(1);
        }
        int locker = EnvironmentLock.GetLockerPid(root);
        if (locker > 0)
        {
            ConsoleHelper.WriteError($"The directory is locked by process {locker}.");
            System.Environment.Exit(1);
        }

        return PacmineEnvironment.Access(root);
    }

    /// <summary>
    /// Performs a topological sort (Kahn's algorithm) on a collection of items
    /// based on dependency edges defined within an optional scope.
    /// </summary>
    /// <typeparam name="T">The type of items to sort.</typeparam>
    /// <param name="items">The items to sort.</param>
    /// <param name="keySelector">Function to extract the unique name/key of each item.</param>
    /// <param name="dependencySelector">Function to extract the dependency names of each item.</param>
    /// <param name="scope">
    /// Optional set of names constraining which dependencies are considered as edges
    /// within the sort. Dependencies outside this set are ignored (treated as external).
    /// If null, all dependencies are treated as internal edges.
    /// </param>
    /// <returns>A topologically sorted list of items (dependencies before dependents).</returns>
    public static List<T> TopologicalSort<T>(
        IEnumerable<T> items,
        Func<T, string> keySelector,
        Func<T, IEnumerable<string>> dependencySelector,
        HashSet<string>? scope = null)
    {
        var itemList = items.ToList();
        var itemMap = itemList.ToDictionary(keySelector);

        var inDegree = new Dictionary<string, int>();
        var adjacency = new Dictionary<string, List<string>>();

        foreach (var item in itemList)
        {
            var name = keySelector(item);
            inDegree[name] = 0;
            adjacency[name] = [];
        }

        foreach (var item in itemList)
        {
            var name = keySelector(item);
            foreach (var depName in dependencySelector(item))
            {
                // Only consider dependencies within scope (or all if scope is null).
                if (scope == null || scope.Contains(depName))
                {
                    if (adjacency.ContainsKey(depName))
                    {
                        adjacency[depName].Add(name);
                        inDegree[name]++;
                    }
                }
            }
        }

        // Kahn's algorithm
        var queue = new Queue<string>();
        foreach (var (name, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(name);
        }

        var sorted = new List<string>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);
            foreach (var dependent in adjacency[current])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        if (sorted.Count != itemList.Count)
        {
            ConsoleHelper.WriteError("Circular dependency detected. Cannot determine order.");
            System.Environment.Exit(1);
        }

        return sorted.Select(name => itemMap[name]).ToList();
    }

    /// <summary>
    /// Prints a confirmation prompt and returns whether the user confirmed.
    /// Accepts "y", "yes", or empty input as confirmation.
    /// </summary>
    /// <param name="message">The prompt message to display.</param>
    /// <returns><c>true</c> if the user confirmed; <c>false</c> otherwise.</returns>
    public static bool ConfirmPrompt(string message = "Continue? [Y/n] ")
    {
        Console.Write(message);
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return response == "y" || response == "yes" || response == "";
    }

    /// <summary>
    /// Prints formatted error messages for an array of <see cref="InvalidReason"/> instances.
    /// </summary>
    /// <param name="reasons">The reasons to print.</param>
    public static void PrintInvalidReasons(InvalidReason[] reasons)
    {
        foreach (var reason in reasons)
        {
            switch (reason)
            {
                case DuplicateNameInvalidReason d:
                    ConsoleHelper.WriteError($"Duplicate package: {d.TargetPackageName}");
                    break;
                case ConflictInvalidReason c:
                    ConsoleHelper.WriteError(
                        $"Package {c.TargetPackageName} conflicts with package {c.ConflictingPackageName}");
                    break;
                case MissingDependsInvalidReason m:
                    ConsoleHelper.WriteError(
                        $"Package {m.TargetPackageName} has unsatisfied dependency: {m.MissingDependName} {m.DesiredVersions}");
                    break;
                case PackageReplacedInvalidReason r:
                    ConsoleHelper.WriteError(
                        $"Package {r.TargetPackageName} would replace {r.ReplacedPackageName}");
                    break;
            }
        }
    }

    /// <summary>
    /// Attempts to delete a directory and all its contents, swallowing any exceptions.
    /// </summary>
    /// <param name="path">The directory path to delete.</param>
    public static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
