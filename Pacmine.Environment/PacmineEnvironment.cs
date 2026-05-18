using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pacmine.Core;
using Pacmine.Environment.Indexing;

namespace Pacmine.Environment;

/// <summary>
/// Manages a Pacmine environment, which represents a game instance directory
/// with package registry, lock file, and meta info management.
/// </summary>
public class PacmineEnvironment : IDisposable
{
    private IndexManager _indexManager = null!;
    private FileStream? _lockStream;

    /// <summary>
    /// The name of the special folder used to store environment data.
    /// </summary>
    public const string SPECIAL_FOLDER_NAME = ".pacmine";

    /// <summary>
    /// The name of the registry folder used to store package metadata.
    /// </summary>
    public const string REGISTRY_FOLDER_NAME = "registry";

    /// <summary>
    /// The name of the lock file used for concurrency control.
    /// </summary>
    public const string LOCKFILE_NAME = "lock";

    /// <summary>
    /// The name of the index folder storing quickly accessible data to avoid full scan on registry.
    /// </summary>
    public const string INDEX_FOLDER_NAME = "index";

    private PacmineEnvironment(string path)
    {
        Path = path;
        SpecialFolder = new(System.IO.Path.Combine(path, SPECIAL_FOLDER_NAME));
        RegistryFolder = new(System.IO.Path.Combine(SpecialFolder.FullName, REGISTRY_FOLDER_NAME));
        IndexFolder = new(System.IO.Path.Combine(SpecialFolder.FullName, INDEX_FOLDER_NAME));
        LockFile = new(System.IO.Path.Combine(SpecialFolder.FullName, LOCKFILE_NAME));
    }

    /// <summary>
    /// Gets the root path of the environment.
    /// </summary>
    public string Path { get; private set; }

    /// <summary>
    /// Gets the special folder directory for this environment.
    /// </summary>
    public DirectoryInfo SpecialFolder { get; private set; }

    /// <summary>
    /// Gets the registry folder directory for this environment.
    /// </summary>
    public DirectoryInfo RegistryFolder { get; private set; }

    /// <summary>
    /// Gets the index folder directory for this environment.
    /// </summary>
    public DirectoryInfo IndexFolder { get; private set; }

    /// <summary>
    /// Gets the lock file information for this environment.
    /// </summary>
    public FileInfo LockFile { get; private set; }

    /// <summary>
    /// Gets the <see cref="Indexing.IndexManager"/> that manages the index files
    /// (package list, managed files, deny list) for this environment.
    /// </summary>
    public IndexManager IndexManager => _indexManager;

    private void Lock()
    {
        try
        {
            _lockStream = new FileStream(
                LockFile.FullName,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read);
            _lockStream.SetLength(0);
            var pidBytes = Encoding.UTF8.GetBytes(System.Environment.ProcessId.ToString());
            _lockStream.Write(pidBytes);
            _lockStream.Flush();
        }
        catch (IOException ex)
        {
            var pid = TryReadLockPid();
            var extra = pid >= 0 ? $" by process {pid}" : "";
            throw new IOException($"Environment is locked{extra}.", ex);
        }
    }

    private int TryReadLockPid()
    {
        try
        {
            using var fs = new FileStream(
                LockFile.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var text = reader.ReadToEnd();
            return int.TryParse(text.Trim(), out var pid) ? pid : -1;
        }
        catch
        {
            return -1;
        }
    }

    private void Unlock()
    {
        _lockStream?.Dispose();
        _lockStream = null;
        try
        {
            if (LockFile.Exists)
                LockFile.Delete();
        }
        catch
        {
            // best-effort cleanup
        }
    }

    /// <summary>
    /// Gets the process ID of the process that currently holds the lock for the specified directory.
    /// </summary>
    /// <param name="directory">The environment root directory.</param>
    /// <returns>The process ID of the locker, or -1 if the lock file does not exist or cannot be read.</returns>
    public static int GetLockerPid(string directory)
    {
        var lockFile = new FileInfo(System.IO.Path.Combine(directory, SPECIAL_FOLDER_NAME, LOCKFILE_NAME));
        if (!lockFile.Exists)
            return -1;

        try
        {
            using var fs = new FileStream(
                lockFile.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var text = reader.ReadToEnd();
            return int.TryParse(text.Trim(), out var pid) ? pid : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Accesses an existing Pacmine environment and acquires the lock.
    /// </summary>
    /// <param name="directory">The environment root directory.</param>
    /// <returns>The initialized <see cref="PacmineEnvironment"/> instance.</returns>
    /// <exception cref="Exception">Thrown when the environment directory does not exist or is locked by another process.</exception>
    public static PacmineEnvironment Access(string directory)
    {
        if (!Directory.Exists(System.IO.Path.Combine(directory, SPECIAL_FOLDER_NAME)))
            throw new Exception("Invalid environment directory");
        if (GetLockerPid(directory) >= 0)
            throw new Exception("Environment is locked");

        PacmineEnvironment env = new(directory);
        env.Lock();

        // Set up IndexManager with the standard handlers
        env._indexManager = new IndexManager(env.RegistryFolder);
        env._indexManager.AddHandler(new PackageListHandler(env.IndexFolder));
        env._indexManager.AddHandler(new VirtualPackagesHandler(env.IndexFolder));
        env._indexManager.AddHandler(new DependsOnHandler(env.IndexFolder));
        env._indexManager.AddHandler(new ManagedFileListHandler(env.IndexFolder));
        env._indexManager.AddHandler(new DenyListHandler(env.IndexFolder));
        env._indexManager.Load();

        return env;
    }

    /// <summary>
    /// Rebuilds the index files (package list, managed files, deny list) from the existing registry records.
    /// Call this to repair a corrupted environment where registry JSON files are intact
    /// but the auxiliary index files are missing or out of sync.
    /// </summary>
    /// <returns><c>true</c> if any repairs were made; <c>false</c> if no registry records exist.</returns>
    public bool Repair()
    {
        if (!RegistryFolder.Exists)
            return false;

        return _indexManager.Rebuild();
    }

    /// <summary>
    /// Creates a new Pacmine environment in the specified directory and acquires the lock.
    /// </summary>
    /// <param name="directory">The directory to initialize as the environment root.</param>
    /// <returns>The newly created <see cref="PacmineEnvironment"/> instance.</returns>
    /// <exception cref="Exception">Thrown when an environment already exists in the directory.</exception>
    public static PacmineEnvironment Create(string directory)
    {
        string spFolderPath = System.IO.Path.Combine(directory, SPECIAL_FOLDER_NAME);
        if (Directory.Exists(spFolderPath))
            throw new Exception("Environment already created");

        Directory.CreateDirectory(spFolderPath);

        // Initialize index files via IndexManager
        var specialFolder = new DirectoryInfo(spFolderPath);
        var registryFolder = new DirectoryInfo(System.IO.Path.Combine(spFolderPath, REGISTRY_FOLDER_NAME));
        var indexFolder = new DirectoryInfo(System.IO.Path.Combine(spFolderPath, INDEX_FOLDER_NAME));

        specialFolder.Create();
        registryFolder.Create();
        indexFolder.Create();

        var initIndex = new IndexManager(registryFolder);
        initIndex.AddHandler(new PackageListHandler(indexFolder));
        initIndex.AddHandler(new VirtualPackagesHandler(indexFolder));
        initIndex.AddHandler(new DependsOnHandler(indexFolder));
        initIndex.AddHandler(new ManagedFileListHandler(indexFolder));
        initIndex.AddHandler(new DenyListHandler(indexFolder));
        initIndex.Initialize();

        return Access(directory);
    }

    /// <summary>
    /// Checks if the specified packages are acceptable in the environment (no conflict, dependencies satisfied, etc).
    /// Note that this method does not check the internal compatibility of <paramref name="packages"/>.
    /// See <see cref="PackageMeta.IsConflictingWith(PackageMeta)"/> for that.
    /// </summary>
    /// <param name="packages">The package list to check.</param>
    /// <returns>An array of <see cref="UnacceptReason"/> indicating why each package is unacceptable.</returns>
    public UnacceptReason[] CheckAcceptance(PackageMeta[] packages)
    {
        var reasons = new List<UnacceptReason>();
        var packList = _indexManager.GetHandler<PackageListHandler>()?.Content ?? [];
        var denyList = _indexManager.GetHandler<DenyListHandler>()?.Content ?? [];
        var virtualPkgs = _indexManager.GetHandler<VirtualPackagesHandler>()?.Content ?? [];

        foreach (var pkgMeta in packages)
        {
            // 1. Check DenyList: any installed package denies this package?
            //    DenyList is keyed by conflict source (installed package name),
            //    value maps denied package name → version range.
            foreach (var (conflictSource, deniedPackages) in denyList)
            {
                // Check the package's own name
                if (deniedPackages.TryGetValue(pkgMeta.Name, out var range))
                {
                    if (range.Contains(pkgMeta.Version))
                        reasons.Add(new ConflictUnacceptReason(pkgMeta.Name, conflictSource, range));
                }

                // Check virtual packages this package provides
                foreach (var (virtualName, virtualVersion) in pkgMeta.Provides)
                {
                    if (deniedPackages.TryGetValue(virtualName, out var virtualRange))
                    {
                        if (virtualRange.Contains(virtualVersion))
                            reasons.Add(new ConflictUnacceptReason(pkgMeta.Name, conflictSource, virtualRange));
                    }
                }
            }

            // 2. Check this package's own Conflicts against installed packages
            //    (the reverse direction — the incoming package denies an installed one)
            //    Check both real packages and virtual packages.
            foreach (var (conflictedPkg, conflictRange) in pkgMeta.Conflicts)
            {
                bool conflictFound = false;

                // Check against real installed packages
                if (packList!.TryGetValue(conflictedPkg, out var installedVersion))
                {
                    if (conflictRange.Contains(installedVersion))
                        conflictFound = true;
                }

                // Check against virtual packages provided by installed packages
                if (!conflictFound && virtualPkgs!.TryGetValue(conflictedPkg, out var virtualVersions))
                {
                    foreach (var version in virtualVersions.Keys)
                    {
                        if (conflictRange.Contains(version))
                        {
                            conflictFound = true;
                            break;
                        }
                    }
                }

                if (conflictFound)
                    reasons.Add(new ConflictUnacceptReason(pkgMeta.Name, conflictedPkg, conflictRange));
            }

            // 3. Check missing or unsatisfied dependencies.
            //    A dependency can be satisfied by either a real installed package
            //    or a virtual package provided by any installed package.
            foreach (var (depName, depRange) in pkgMeta.Depends)
            {
                bool satisfied = false;

                // Check against real installed packages
                if (packList!.TryGetValue(depName, out var installedVersion))
                {
                    if (depRange.Contains(installedVersion))
                        satisfied = true;
                }

                // Check against virtual packages provided by installed packages
                if (!satisfied && virtualPkgs!.TryGetValue(depName, out var virtualVersions))
                {
                    foreach (var version in virtualVersions.Keys)
                    {
                        if (depRange.Contains(version))
                        {
                            satisfied = true;
                            break;
                        }
                    }
                }

                if (!satisfied)
                    reasons.Add(new MissingDependsUnacceptReason(pkgMeta.Name, depName, depRange));
            }

            // 4. Check if this package replaces any already-installed package
            foreach (var (replacedPkg, _) in pkgMeta.Replaces)
            {
                if (packList.ContainsKey(replacedPkg))
                {
                    reasons.Add(new PackageReplacedUnacceptReason(pkgMeta.Name, replacedPkg));
                }
            }
        }

        return reasons.ToArray();
    }

    /// <summary>
    /// Determines whether the specified set of packages can be safely uninstalled.
    /// Checks for non-existent packages, reverse dependencies (real and virtual),
    /// and version-aware virtual package dependency satisfaction.
    /// </summary>
    /// <param name="packages">The array of package names to check for uninstall.</param>
    /// <returns>An array of <see cref="UninstallDenyReason"/> indicating why each package cannot be uninstalled.
    /// An empty array indicates that the operation is safe.</returns>
    public UninstallDenyReason[] CheckCanUninstall(string[] packages)
    {
        var reasons = new List<UninstallDenyReason>();
        var uninstallSet = packages.ToHashSet();

        var packList = _indexManager.GetHandler<PackageListHandler>()?.Content ?? [];
        var dependsOn = _indexManager.GetHandler<DependsOnHandler>()?.Content ?? [];
        var virtualPkgs = _indexManager.GetHandler<VirtualPackagesHandler>()?.Content;

        foreach (var pkgName in packages)
        {
            // 1. Existence check
            if (!packList.ContainsKey(pkgName))
            {
                reasons.Add(new NotExistDenyReason(pkgName));
                continue;
            }

            // 2. Check real-name reverse dependencies (version-agnostic — a real package name is unique)
            if (dependsOn.TryGetValue(pkgName, out var dependents))
            {
                foreach (var depender in dependents)
                {
                    if (!uninstallSet.Contains(depender))
                    {
                        reasons.Add(new BreakDependDenyReason(pkgName, depender));
                    }
                }
            }

            // 3. Check virtual package reverse dependencies (version-aware)
            //    Single registry file read is acceptable (not a full scan)
            var registry = GetRegistry(pkgName);
            if (registry == null) continue;

            foreach (var (virtualName, providedVersion) in registry.Meta.Provides)
            {
                if (!dependsOn.TryGetValue(virtualName, out var virtualDeps))
                    continue;

                foreach (var depender in virtualDeps)
                {
                    if (uninstallSet.Contains(depender))
                        continue;

                    // Read the dependent's registry to verify their exact version requirement.
                    var dependerRegistry = GetRegistry(depender);
                    if (dependerRegistry == null) continue;

                    // Does this dependent actually require the version being removed?
                    if (!dependerRegistry.Meta.Depends.TryGetValue(virtualName, out var requiredRange))
                        continue;

                    if (!requiredRange.Contains(providedVersion))
                        continue;  // dependent needs a different version — not affected

                    // Check if another provider (of any version) satisfies the dependent's requirement
                    bool otherProviderExists = virtualPkgs != null
                        && virtualPkgs.TryGetValue(virtualName, out var versionDict)
                        && versionDict.Any(kvp =>
                            requiredRange.Contains(kvp.Key)
                            && kvp.Value.Any(p => !uninstallSet.Contains(p)));

                    if (!otherProviderExists)
                    {
                        reasons.Add(new BreakDependDenyReason(pkgName, depender));
                    }
                }
            }
        }

        return reasons.ToArray();
    }

    /// <summary>
    /// Write a record into the package registry(create a new one or update existing one).
    /// Stale managed file entries that are no longer in the package's file list are removed.
    /// </summary>
    /// <param name="registry">The package registry to write.</param>
    public void WriteRegistry(PackageRegistry registry)
    {
        char initLetter = registry.Meta.Name[0];
        DirectoryInfo layerDir = new(System.IO.Path.Combine(RegistryFolder.FullName, initLetter.ToString()));
        if (!layerDir.Exists) layerDir.Create();

        File.WriteAllText(
            System.IO.Path.Combine(
                layerDir.FullName,
                $"{registry.Meta.Name}.json"),
            JsonSerializer.Serialize(registry));

        _indexManager.OnWriteRegistry(registry);
    }

    /// <summary>
    /// Remove a record from the package registry.
    /// </summary>
    /// <param name="packageName">The name of the package to remove.</param>
    /// <exception cref="Exception">Thrown when the package does not exist.</exception>
    public void RemoveRegistry(string packageName)
    {
        char initLetter = packageName[0];
        var registryFile = new FileInfo(System.IO.Path.Combine(
            RegistryFolder.FullName,
            initLetter.ToString(),
            $"{packageName}.json"));

        if (!registryFile.Exists)
            throw new Exception($"Package '{packageName}' does not exist in the registry");

        // Read the registry before deleting it, so we can access virtual package info
        var registry = JsonSerializer.Deserialize<PackageRegistry>(
            File.ReadAllText(registryFile.FullName));

        registryFile.Delete();

        _indexManager.OnRemoveRegistry(registry!);
    }

    /// <summary>
    /// Remove all files owned by the specified package from disk.
    /// Uses the in-memory managed file index to find the files — does NOT
    /// read the package registry from disk and does NOT modify the managed file index.
    /// </summary>
    /// <param name="packageName">The name of the package whose files to remove.</param>
    public void RemovePackageFiles(string packageName)
    {
        var mngHandler = _indexManager.GetHandler<ManagedFileListHandler>();
        if (mngHandler == null) return;

        var ownedFiles = mngHandler.Content
            .Where(kvp => kvp.Value.Owner == packageName)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var filePath in ownedFiles)
        {
            var fullPath = System.IO.Path.Combine(Path, filePath);
            try
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch
            {
                // best-effort deletion
            }
        }
    }

    /// <summary>
    /// Check if there are any conflicts between the specified file list and the files in this environment.
    /// </summary>
    /// <param name="fileNames">The list of file names to check for conflicts.</param>
    /// <param name="ignoredOwners">The list of package names whose files should be ignored when checking for conflicts.
    /// Pass an empty array to check against all managed files.</param>
    /// <returns>A dictionary mapping each conflicting file name to its owner.
    /// An empty owner string indicates an orphan file (exists on disk but not managed by any package).</returns>
    public Dictionary<string, string> CheckConflictFiles(string[] fileNames, string[] ignoredOwners)
    {
        var conflicts = new Dictionary<string, string>();
        var mngFiles = _indexManager.GetHandler<ManagedFileListHandler>()?.Content;

        foreach (var fileName in fileNames)
        {
            // Check if the file is managed by a package not in the ignored list
            if (mngFiles?.TryGetValue(fileName, out var record) == true)
            {
                if (!ignoredOwners.Contains(record.Owner))
                {
                    conflicts[fileName] = record.Owner;
                }
            }
            // Check if the file exists on disk but is not managed (orphan file)
            else
            {
                var fullPath = System.IO.Path.Combine(Path, fileName);
                if (File.Exists(fullPath))
                {
                    conflicts[fileName] = string.Empty;
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Get the package registry of the specified package.
    /// </summary>
    /// <param name="packageName">The name of the package to get the registry of.</param>
    /// <returns>The registry record, or <c>null</c> if the package does not exist.</returns>
    public PackageRegistry? GetRegistry(string packageName)
    {
        char initLetter = packageName[0];
        var registryFile = new FileInfo(System.IO.Path.Combine(
            RegistryFolder.FullName,
            initLetter.ToString(),
            $"{packageName}.json"));

        if (!registryFile.Exists)
            return null;

        return JsonSerializer.Deserialize<PackageRegistry>(
            File.ReadAllText(registryFile.FullName));
    }

    /// <summary>
    /// Install or update files from the specified source directory into the environment.
    /// All files in the source directory are considered to be owned by <paramref name="owner"/>.
    /// New files are copied, existing files are overwritten, and files that are no longer
    /// present in the source are removed from the environment.
    /// </summary>
    /// <param name="owner">The name of the package that owns these files.</param>
    /// <param name="source">The source directory containing all files to install.</param>
    /// <returns>A dictionary mapping each relative file path to its SHA256 checksum.</returns>
    /// <remarks>
    /// This method only performs file operations — it does not read or write the package registry.
    /// The caller should use <see cref="WriteRegistry"/> separately to persist the returned
    /// file list as part of a <see cref="PackageRegistry"/>.
    /// </remarks>
    public Dictionary<string, string> UpdateFiles(string owner, DirectoryInfo source)
    {
        if (!source.Exists)
            throw new DirectoryNotFoundException($"Source directory '{source.FullName}' does not exist");

        var fileList = new Dictionary<string, string>();
        var sourceFiles = source.EnumerateFiles("*", SearchOption.AllDirectories);

        // Track which files were previously owned by this package
        var mngFiles = _indexManager.GetHandler<ManagedFileListHandler>()?.Content ?? [];
        var previouslyOwned = mngFiles
            .Where(kvp => kvp.Value.Owner == owner)
            .Select(kvp => kvp.Key)
            .ToHashSet();

        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = System.IO.Path.GetRelativePath(source.FullName, sourceFile.FullName);
            var targetPath = System.IO.Path.Combine(Path, relativePath);

            var targetDir = System.IO.Path.GetDirectoryName(targetPath);
            if (targetDir != null)
                Directory.CreateDirectory(targetDir);

            File.Copy(sourceFile.FullName, targetPath, overwrite: true);

            // Compute SHA256 checksum
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(sourceFile.OpenRead());
            var checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();

            fileList[relativePath] = checksum;
            previouslyOwned.Remove(relativePath);
        }

        // Remove stale files that are no longer in the source directory
        foreach (var staleFile in previouslyOwned)
        {
            var fullPath = System.IO.Path.Combine(Path, staleFile);
            try
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch
            {
                // best-effort deletion
            }
        }

        return fileList;
    }

    /// <summary>
    /// Deletes the special folder and all its contents from the environment.
    /// </summary>
    public void Destroy()
    {
        Unlock();
        SpecialFolder.Delete(true);
    }

    /// <summary>
    /// Releases the lock held by this environment instance.
    /// </summary>
    public void Dispose()
    {
        Unlock();
    }
}
