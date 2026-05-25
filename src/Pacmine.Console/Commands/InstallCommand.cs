using System.CommandLine;
using System.IO.Compression;
using Pacmine.Core;
using Pacmine.Environment;

namespace Pacmine.Console.Commands;

using Console = System.Console;

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
        var forceOpt = new Option<bool>("--force", ["-f"])
        {
            Description = "Force overwrite orphan files (files not managed by any package)",
            DefaultValueFactory = _ => false
        };

        var cmd = new Command("install", "Install a remote/local package")
        {
            pkgNameListArg,
            rootOpt,
            localOpt,
            forceOpt
        };
        cmd.SetAction(async (parseResult) =>
        {
            var pkgNames = parseResult.GetValue(pkgNameListArg);
            var root = parseResult.GetValue(rootOpt);
            var local = parseResult.GetValue(localOpt);
            var force = parseResult.GetValue(forceOpt);
            await ExecuteAsync(pkgNames, root, local, force);
        });
        return cmd;
    }

    private static async Task ExecuteAsync(string[]? pkgNameList, string? root, bool local, bool force)
    {
        pkgNameList ??= [];
        if (!local)
        {
            ConsoleHelper.WriteError("Downloading remote packages is not yet implemented. Install local packages with flag --local.");
            System.Environment.Exit(1);
        }
        root ??= System.Environment.CurrentDirectory;
        int locker = PacmineEnvironment.GetLockerPid(root);
        if (locker > 0)
        {
            ConsoleHelper.WriteError($"The directory is locked by process {locker}.");
            System.Environment.Exit(1);
        }

        using var env = PacmineEnvironment.Access(root);

        // ═══════════════════════════════════════════════════════════════
        // Phase 1: Read all package archives and collect metadata
        // ═══════════════════════════════════════════════════════════════
        var packageInfos = new List<(string ArchivePath, PackageMeta Meta, DateTime PackagedTime, List<string> FileNames)>();
        bool hasError = false;

        foreach (var pkgPath in pkgNameList)
        {
            if (!File.Exists(pkgPath))
            {
                ConsoleHelper.WriteError($"File not found: {pkgPath}");
                hasError = true;
                continue;
            }

            try
            {
                using var archive = ZipFile.OpenRead(pkgPath);
                var meta = PackageParser.GetMeta(archive);
                if (meta == null)
                {
                    ConsoleHelper.WriteError($"Not a valid Pacmine package (missing meta): {pkgPath}");
                    hasError = true;
                    continue;
                }

                var packagedTime = PackageParser.GetPackagedTime(archive) ?? DateTime.MinValue;

                var fileNames = new List<string>();
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName == PackageParser.META_FILE_NAME || string.IsNullOrEmpty(entry.Name))
                        continue;
                    fileNames.Add(entry.FullName);
                }

                packageInfos.Add((pkgPath, meta, packagedTime, fileNames));
            }
            catch (InvalidDataException)
            {
                ConsoleHelper.WriteError($"Not a valid ZIP archive: {pkgPath}");
                hasError = true;
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Failed to read package {pkgPath}: {ex.Message}");
                hasError = true;
            }
        }

        if (hasError)
        {
            env.Dispose();
            System.Environment.Exit(1);
        }

        ConsoleHelper.WriteInfo($"Read {packageInfos.Count} package(s).");

        // ═══════════════════════════════════════════════════════════════
        // Phase 2: Batch validation
        // ═══════════════════════════════════════════════════════════════
        ConsoleHelper.WriteInfo("Validating packages...");

        // 2a. Check for duplicate package names in the batch
        var nameSet = new HashSet<string>();
        foreach (var (_, meta, _, _) in packageInfos)
        {
            if (!nameSet.Add(meta.Name))
            {
                ConsoleHelper.WriteError($"Duplicate package in batch: {meta.Name}");
                env.Dispose();
                System.Environment.Exit(1);
            }
        }

        // 2b. Check internal conflicts between batch members
        for (int i = 0; i < packageInfos.Count; i++)
        {
            for (int j = i + 1; j < packageInfos.Count; j++)
            {
                if (packageInfos[i].Meta.IsConflictingWith(packageInfos[j].Meta))
                {
                    ConsoleHelper.WriteError(
                        $"Internal conflict: {packageInfos[i].Meta.Name} conflicts with {packageInfos[j].Meta.Name}");
                    env.Dispose();
                    System.Environment.Exit(1);
                }
            }
        }

        // 2c. Check acceptance against the environment (called once for the whole batch)
        var allMetas = packageInfos.Select(p => p.Meta).ToArray();
        var allReasons = env.CheckAcceptance(allMetas);
        var refusedNames = allReasons.Select(r => r.RefusedPackageName).ToHashSet();

        // Print any acceptance errors
        foreach (var reason in allReasons)
        {
            switch (reason)
            {
                case ConflictUnacceptReason c:
                    ConsoleHelper.WriteError(
                        $"Package {c.RefusedPackageName} conflicts with package {c.ConflictingPackageName} (version range {c.ConflictingVersions})");
                    break;
                case MissingDependsUnacceptReason m:
                    ConsoleHelper.WriteError(
                        $"Package {m.RefusedPackageName} has unsatisfied dependency: {m.MissingDependName} {m.DesiredVersions}");
                    break;
                case PackageReplacedUnacceptReason r:
                    ConsoleHelper.WriteError(
                        $"Package {r.RefusedPackageName} would replace {r.ReplacedPackageName}");
                    break;
            }
        }

        // Remove refused packages from the batch
        packageInfos.RemoveAll(p => refusedNames.Contains(p.Meta.Name));

        if (packageInfos.Count == 0)
        {
            ConsoleHelper.WriteError("Nothing to install.");
            env.Dispose();
            System.Environment.Exit(1);
        }

        // 2d. Topological sort — dependencies must be installed before dependents
        var batchNames = packageInfos.Select(p => p.Meta.Name).ToHashSet();
        var installOrder = TopologicalSort(packageInfos, batchNames);

        // ── Warn about reinstalling / downgrading packages ──────────
        foreach (var (_, meta, _, _) in installOrder)
        {
            var existing = env.GetRegistry(meta.Name);
            if (existing == null)
                continue;
            if (meta.Version == existing.Meta.Version &&
                     meta.Release == existing.Meta.Release &&
                     meta.Epoch == existing.Meta.Epoch)
            {
                ConsoleHelper.WriteWarning(
                    $"Package {meta.Name} version {meta.GetFullVersionString()} is already installed (reinstalling)");
            }
            else if (existing.Meta.IsNewerThan(meta))
            {
                ConsoleHelper.WriteWarning(
                    $"Downgrading {meta.Name}: {existing.Meta.GetFullVersionString()} → {meta.GetFullVersionString()}");
            }
        }

        // ── Confirmation prompt ──────────────────────────────────────
        PrintInstallPlan(installOrder, env);

        Console.Write("Continue? [Y/n] ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (response != "y" && response != "yes" && response != "")
        {
            ConsoleHelper.WriteWarning("Installation cancelled.");
            env.Dispose();
            return;
        }

        // ═══════════════════════════════════════════════════════════════
        // Phase 3: Install packages in dependency order
        // ═══════════════════════════════════════════════════════════════
        foreach (var (archivePath, meta, packagedTime, fileNames) in installOrder)
        {
            try
            {
                InstallSinglePackage(env, archivePath, meta, packagedTime, fileNames, force);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Unexpected error installing {meta.Name}: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// Prints a table of packages to be installed, with version differences highlighted.
    /// Only one version component (epoch, version, release) is highlighted per row,
    /// ordered by priority: epoch > version > release.
    /// </summary>
    private static void PrintInstallPlan(
        List<(string ArchivePath, PackageMeta Meta, DateTime PackagedTime, List<string> FileNames)> installOrder,
        PacmineEnvironment env)
    {
        // Compute column widths
        int maxNameLen = "Package".Length;
        int maxOldLen = "Old Version".Length;
        int maxNewLen = "New Version".Length;
        foreach (var (_, meta, _, _) in installOrder)
        {
            if (meta.Name.Length > maxNameLen)
                maxNameLen = meta.Name.Length;
            var existing = env.GetRegistry(meta.Name);
            if (existing != null)
            {
                var oldVer = existing.Meta.GetFullVersionString();
                if (oldVer.Length > maxOldLen)
                    maxOldLen = oldVer.Length;
            }
            var newVer = meta.GetFullVersionString();
            if (newVer.Length > maxNewLen)
                maxNewLen = newVer.Length;
        }

        // Print header
        Console.WriteLine();
        Console.Write($"Package ({installOrder.Count})".PadRight(maxNameLen));
        Console.Write("    ");
        Console.Write("Old Version".PadRight(maxOldLen));
        Console.Write("    ");
        Console.WriteLine("New Version");
        Console.Write(new string('-', maxNameLen));
        Console.Write("    ");
        Console.Write(new string('-', maxOldLen));
        Console.Write("    ");
        Console.WriteLine(new string('-', maxNewLen));

        // Print each row
        foreach (var (_, meta, _, _) in installOrder)
        {
            var existing = env.GetRegistry(meta.Name);
            var newVer = meta.GetFullVersionString();
            var oldVer = existing?.Meta.GetFullVersionString() ?? "-";
            var isUpgrade = existing != null && meta.IsNewerThan(existing.Meta);

            Console.Write(meta.Name.PadRight(maxNameLen));

            Console.Write("    ");
            Console.Write(oldVer.PadRight(maxOldLen));
            Console.Write("    ");

            if (existing == null)
            {
                // New install — no highlighting
                Console.WriteLine(newVer);
            }
            else
            {
                // Print new version with highlighting
                PrintVersionHighlighted(meta, existing.Meta, isUpgrade);
                Console.WriteLine();
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Prints the new version string with the differing component highlighted.
    /// Priority: epoch > version > release. Only one component is highlighted.
    /// Upgrade → green (<see cref="ConsoleColor.Green"/>), downgrade → dark yellow (<see cref="ConsoleColor.DarkYellow"/>).
    /// Gray components use <see cref="ConsoleColor.DarkGray"/>.
    /// </summary>
    private static void PrintVersionHighlighted(PackageMeta newMeta, PackageMeta oldMeta, bool isUpgrade)
    {
        var highlightColor = isUpgrade ? ConsoleColor.Green : ConsoleColor.DarkYellow;
        var grayColor = ConsoleColor.DarkGray;

        if (newMeta.Epoch != oldMeta.Epoch)
        {
            // Highlight epoch, version normal, release gray
            Console.ForegroundColor = highlightColor;
            Console.Write($"{newMeta.Epoch}:");
            Console.ResetColor();
            Console.Write($"{newMeta.Version}");
            Console.ForegroundColor = grayColor;
            Console.Write($"#{newMeta.Release}");
            Console.ResetColor();
        }
        else if (newMeta.Version != oldMeta.Version)
        {
            // Epoch gray (if non-zero), highlight version, release gray
            if (newMeta.Epoch != 0)
            {
                Console.ForegroundColor = grayColor;
                Console.Write($"{newMeta.Epoch}:");
                Console.ResetColor();
            }
            Console.ForegroundColor = highlightColor;
            Console.Write($"{newMeta.Version}");
            Console.ResetColor();
            Console.ForegroundColor = grayColor;
            Console.Write($"#{newMeta.Release}");
            Console.ResetColor();
        }
        else if (newMeta.Release != oldMeta.Release)
        {
            // Epoch gray (if non-zero), version normal, highlight release
            if (newMeta.Epoch != 0)
            {
                Console.ForegroundColor = grayColor;
                Console.Write($"{newMeta.Epoch}:");
                Console.ResetColor();
            }
            Console.Write($"{newMeta.Version}");
            Console.ForegroundColor = highlightColor;
            Console.Write($"#{newMeta.Release}");
            Console.ResetColor();
        }
        else
        {
            // All components are the same
            Console.Write(newMeta.GetFullVersionString());
        }
    }

    /// <summary>
    /// Performs a topological sort on the batch of packages so that dependencies
    /// are installed before the packages that depend on them.
    /// </summary>
    private static List<(string ArchivePath, PackageMeta Meta, DateTime PackagedTime, List<string> FileNames)> TopologicalSort(
        List<(string ArchivePath, PackageMeta Meta, DateTime PackagedTime, List<string> FileNames)> packages,
        HashSet<string> batchNames)
    {
        var inDegree = new Dictionary<string, int>();
        var adjacency = new Dictionary<string, List<string>>();

        foreach (var (_, meta, _, _) in packages)
        {
            inDegree[meta.Name] = 0;
            adjacency[meta.Name] = [];
        }

        foreach (var (_, meta, _, _) in packages)
        {
            foreach (var (depName, _) in meta.Depends)
            {
                if (batchNames.Contains(depName))
                {
                    adjacency[depName].Add(meta.Name);
                    inDegree[meta.Name]++;
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

        if (sorted.Count != packages.Count)
        {
            ConsoleHelper.WriteError("Circular dependency detected within the batch. Cannot determine install order.");
            System.Environment.Exit(1);
        }

        var nameToInfo = packages.ToDictionary(p => p.Meta.Name);
        return sorted.Select(name => nameToInfo[name]).ToList();
    }

    /// <summary>
    /// Installs a single package into the environment: checks file conflicts,
    /// extracts the archive to a temp directory, copies files via <see cref="PacmineEnvironment.UpdateFiles"/>,
    /// and persists the registry record. Acceptance has already been checked once for the whole batch.
    /// </summary>
    private static void InstallSinglePackage(
        PacmineEnvironment env,
        string archivePath,
        PackageMeta meta,
        DateTime packagedTime,
        List<string> fileNames,
        bool force)
    {
        ConsoleHelper.WriteInfo($"Installing {meta.Name} {meta.GetFullVersionString()}...");

        // 1. Check file conflicts
        var existing = env.GetRegistry(meta.Name);
        var ignoredOwners = existing != null ? [meta.Name] : Array.Empty<string>();
        var conflicts = env.CheckConflictFiles([.. fileNames], ignoredOwners);
        bool hasFatal = false;
        foreach (var (fileName, owner) in conflicts)
        {
            if (string.IsNullOrEmpty(owner))
            {
                if (force)
                {
                    ConsoleHelper.WriteWarning($"  Overwriting orphan file: {fileName}");
                }
                else
                {
                    ConsoleHelper.WriteError(
                        $"  File conflict: {fileName} already exists (not managed by any package). Use --force to overwrite.");
                    hasFatal = true;
                }
            }
            else
            {
                ConsoleHelper.WriteError($"  File conflict: {fileName} is owned by package {owner}");
                hasFatal = true;
            }
        }

        if (hasFatal)
        {
            ConsoleHelper.WriteError($"  Skipping {meta.Name} due to file conflicts.");
            return;
        }

        // 3. Extract archive to a temporary directory (excluding the meta file)
        var tempDir = Path.Combine(Path.GetTempPath(), $"pacmine_install_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName == PackageParser.META_FILE_NAME || string.IsNullOrEmpty(entry.Name))
                    continue;

                var targetPath = Path.Combine(tempDir, entry.FullName);
                var targetDir = Path.GetDirectoryName(targetPath);
                if (targetDir != null)
                    Directory.CreateDirectory(targetDir);

                entry.ExtractToFile(targetPath, overwrite: true);
            }
        }
        catch
        {
            TryDeleteDirectory(tempDir);
            throw;
        }

        // 4. Install files into the environment (copy + compute SHA-256 checksums)
        Dictionary<string, string> fileList;
        try
        {
            fileList = env.UpdateFiles(meta.Name, new DirectoryInfo(tempDir));
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"  Failed to install files for {meta.Name}: {ex.Message}");
            TryDeleteDirectory(tempDir);
            return;
        }

        // 5. Write the package registry record
        bool writeSuccess = env.TryWriteRegistry(new PackageRegistry
        {
            Meta = meta,
            FileList = fileList,
            InstallReason = InstallReasons.Explicit,
            PackagedTime = packagedTime,
            InstalledTime = DateTime.UtcNow
        });

        // 6. Clean up the temporary directory
        TryDeleteDirectory(tempDir);

        if (!writeSuccess)
        {
            ConsoleHelper.WriteError(
                $"  Failed to write registry for {meta.Name}. The files have been installed but the registry update failed. Try running 'repair'.");
            return;
        }
    }

    /// <summary>
    /// Attempts to delete a directory and all its contents, swallowing any exceptions.
    /// </summary>
    private static void TryDeleteDirectory(string path)
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
