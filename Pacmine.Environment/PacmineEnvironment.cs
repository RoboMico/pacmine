using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment;

/// <summary>
/// Manages a Pacmine environment, which represents a game instance directory
/// with package registry, lock file, and meta info management.
/// </summary>
public class PacmineEnvironment : IDisposable
{
    /// <summary>
    /// Represents a file managed by a package, tracking its owner and SHA256 checksum.
    /// </summary>
    /// <param name="Owner">The name of the package that owns this file.</param>
    /// <param name="SHA256">The SHA256 checksum of the file contents.</param>
    public record ManagedFileRecord(string Owner, string SHA256);

    private Dictionary<string, VersionIdentifier> _packList = [];
    private Dictionary<string, ManagedFileRecord> _mngFiles = [];
    private Dictionary<string, Dictionary<string, VersionRange>> _denyList = [];
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
    /// The name of the file storing the list of installed packages.
    /// </summary>
    public const string PACKLIST_FILE_NAME = "packlist.json";

    /// <summary>
    /// The name of the file storing the list of managed files.
    /// </summary>
    public const string MANAGED_FILE_LIST_FILE_NAME = "managed_files.json";

    /// <summary>
    /// The name of the file storing the deny list.
    /// </summary>
    public const string DENY_LIST_FILE_NAME = "deny_list.json";

    private PacmineEnvironment(string path)
    {
        Path = path;
        SpecialFolder = new(System.IO.Path.Combine(path, SPECIAL_FOLDER_NAME));
        RegistryFolder = new(System.IO.Path.Combine(SpecialFolder.FullName, REGISTRY_FOLDER_NAME));
        LockFile = new(System.IO.Path.Combine(SpecialFolder.FullName, LOCKFILE_NAME));
        PackListFile = new(System.IO.Path.Combine(SpecialFolder.FullName, PACKLIST_FILE_NAME));
        ManagedFileListFile = new(System.IO.Path.Combine(SpecialFolder.FullName, MANAGED_FILE_LIST_FILE_NAME));
        DenyListFile = new(System.IO.Path.Combine(SpecialFolder.FullName, DENY_LIST_FILE_NAME));
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
    /// Gets the lock file information for this environment.
    /// </summary>
    public FileInfo LockFile { get; private set; }

    /// <summary>
    /// Gets the package list file information for this environment.
    /// </summary>
    public FileInfo PackListFile { get; private set; }

    /// <summary>
    /// Gets the file information of <see cref="MANAGED_FILE_LIST_FILE_NAME"/> in this environment.
    /// </summary>
    public FileInfo ManagedFileListFile { get; private set; }

    /// <summary>
    /// Gets the file information of <see cref="DENY_LIST_FILE_NAME"/> in this environment.
    /// </summary>
    public FileInfo DenyListFile { get; private set; }

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

        PacmineEnvironment env = new(directory);
        env.Lock();

        // Load packlist; treat missing or corrupted files as empty
        try
        {
            var rawDict = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(env.PackListFile.FullName)) ?? [];
            env._packList = rawDict.ToDictionary(kvp => kvp.Key, kvp => new VersionIdentifier(kvp.Value));
        }
        catch
        {
            env._packList = [];
        }

        // Load managed files; treat missing or corrupted files as empty
        try
        {
            env._mngFiles = JsonSerializer.Deserialize<Dictionary<string, ManagedFileRecord>>(
                File.ReadAllText(env.ManagedFileListFile.FullName)) ?? [];
        }
        catch
        {
            env._mngFiles = [];
        }

        // Load deny list; treat missing or corrupted files as empty.
        // Stored as Dictionary<string, Dictionary<string, string>> where the innermost strings
        // are VersionRange.ToString() representations.
        try
        {
            var rawDenyList = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                File.ReadAllText(env.DenyListFile.FullName)) ?? [];
            env._denyList = rawDenyList.ToDictionary(
                outer => outer.Key,
                outer => outer.Value.ToDictionary(
                    inner => inner.Key,
                    inner => new VersionRange(inner.Value)));
        }
        catch
        {
            env._denyList = [];
        }

        return env;
    }

    /// <summary>
    /// Rebuilds <see cref="PackListFile"/>, <see cref="ManagedFileListFile"/>,
    /// and <see cref="DenyListFile"/> from the existing registry records.
    /// Call this to repair a corrupted environment where registry JSON files are intact
    /// but the auxiliary index files are missing or out of sync.
    /// </summary>
    /// <returns><c>true</c> if any repairs were made; <c>false</c> if no registry records exist.</returns>
    public bool Repair()
    {
        if (!RegistryFolder.Exists)
            return false;

        bool repaired = false;

        // Scan registry to rebuild package list (names → versions + virtual packages)
        var packageNames = new Dictionary<string, VersionIdentifier>();
        try
        {
            foreach (var subDir in RegistryFolder.EnumerateDirectories())
            {
                foreach (var file in subDir.EnumerateFiles("*.json"))
                {
                    try
                    {
                        var registry = JsonSerializer.Deserialize<PackageRegistry>(
                            File.ReadAllText(file.FullName));
                        if (registry == null) continue;

                        // Add the package itself
                        packageNames[registry.Meta.Name] = registry.Meta.Version;

                        // Add virtual packages this package provides
                        foreach (var (virtualName, virtualVersion) in registry.Meta.Provides)
                        {
                            packageNames[virtualName] = virtualVersion;
                        }
                    }
                    catch
                    {
                        // skip corrupt registry entries
                    }
                }
            }
        }
        catch
        {
            // best-effort scan
        }

        if (packageNames.Count > 0)
        {
            // Only write if the current list is different from the scanned result
            var serializedCurrent = JsonSerializer.Serialize(_packList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RawString));
            var serializedScanned = JsonSerializer.Serialize(packageNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RawString));
            if (serializedCurrent != serializedScanned)
            {
                _packList = packageNames;
                File.WriteAllText(PackListFile.FullName,
                    JsonSerializer.Serialize(_packList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RawString)));
                repaired = true;
            }
        }

        // Scan registry to rebuild managed files
        var mngFiles = new Dictionary<string, ManagedFileRecord>();
        try
        {
            foreach (var subDir in RegistryFolder.EnumerateDirectories())
            {
                foreach (var file in subDir.EnumerateFiles("*.json"))
                {
                    try
                    {
                        var registry = JsonSerializer.Deserialize<PackageRegistry>(
                            File.ReadAllText(file.FullName));
                        if (registry == null) continue;

                        foreach (var kvp in registry.FileList)
                        {
                            mngFiles[kvp.Key] = new ManagedFileRecord(registry.Meta.Name, kvp.Value);
                        }
                    }
                    catch
                    {
                        // skip corrupt registry entries
                    }
                }
            }
        }
        catch
        {
            // best-effort scan
        }

        if (mngFiles.Count > 0)
        {
            var serializedCurrent = JsonSerializer.Serialize(_mngFiles);
            var serializedScanned = JsonSerializer.Serialize(mngFiles);
            if (serializedCurrent != serializedScanned)
            {
                _mngFiles = mngFiles;
                File.WriteAllText(ManagedFileListFile.FullName, JsonSerializer.Serialize(_mngFiles));
                repaired = true;
            }
        }

        // Scan registry to rebuild deny list from conflict declarations
        var denyList = new Dictionary<string, Dictionary<string, VersionRange>>();
        try
        {
            foreach (var subDir in RegistryFolder.EnumerateDirectories())
            {
                foreach (var file in subDir.EnumerateFiles("*.json"))
                {
                    try
                    {
                        var registry = JsonSerializer.Deserialize<PackageRegistry>(
                            File.ReadAllText(file.FullName));
                        if (registry == null) continue;

                        if (registry.Meta.Conflicts.Count > 0)
                        {
                            denyList[registry.Meta.Name] = new Dictionary<string, VersionRange>(registry.Meta.Conflicts);
                        }
                    }
                    catch
                    {
                        // skip corrupt registry entries
                    }
                }
            }
        }
        catch
        {
            // best-effort scan
        }

        var serializedDenyCurrent = JsonSerializer.Serialize(
            _denyList.ToDictionary(outer => outer.Key,
                outer => outer.Value.ToDictionary(inner => inner.Key, inner => inner.Value.ToString())));
        var serializedDenyScanned = JsonSerializer.Serialize(
            denyList.ToDictionary(outer => outer.Key,
                outer => outer.Value.ToDictionary(inner => inner.Key, inner => inner.Value.ToString())));
        if (serializedDenyCurrent != serializedDenyScanned)
        {
            _denyList = denyList;
            var serializable = _denyList.ToDictionary(
                outer => outer.Key,
                outer => outer.Value.ToDictionary(
                    inner => inner.Key,
                    inner => inner.Value.ToString()));
            File.WriteAllText(DenyListFile.FullName, JsonSerializer.Serialize(serializable));
            repaired = true;
        }

        return repaired;
    }

    /// <summary>
    /// Creates a new Pacmine environment in the specified directory and acquires the lock.
    /// </summary>
    /// <param name="directory">The directory to initialize as the environment root.</param>
    /// <returns>The newly created <see cref="PacmineEnvironment"/> instance.</returns>
    /// <exception cref="Exception">Thrown when an environment already exists in the directory.</exception>
    public static PacmineEnvironment Create(string directory)
    {
        string databasePath = System.IO.Path.Combine(directory, SPECIAL_FOLDER_NAME);
        if (Directory.Exists(databasePath))
            throw new Exception("Environment already created");

        Directory.CreateDirectory(databasePath);
        File.WriteAllText(System.IO.Path.Combine(databasePath, PACKLIST_FILE_NAME), "{}");
        File.WriteAllText(System.IO.Path.Combine(databasePath, MANAGED_FILE_LIST_FILE_NAME), "{}");
        File.WriteAllText(System.IO.Path.Combine(databasePath, DENY_LIST_FILE_NAME), "{}");
        Directory.CreateDirectory(System.IO.Path.Combine(databasePath, REGISTRY_FOLDER_NAME));

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

        foreach (var pkgMeta in packages)
        {
            // 1. Check DenyList: any installed package denies this package?
            //    DenyList is keyed by conflict source (installed package name),
            //    value maps denied package name → version range.
            foreach (var (conflictSource, deniedPackages) in DenyList)
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
            foreach (var (conflictedPkg, conflictRange) in pkgMeta.Conflicts)
            {
                if (PackageList.TryGetValue(conflictedPkg, out var installedVersion))
                {
                    if (conflictRange.Contains(installedVersion))
                        reasons.Add(new ConflictUnacceptReason(pkgMeta.Name, conflictedPkg, conflictRange));
                }
            }

            // 3. Check missing or unsatisfied dependencies
            foreach (var (depName, depRange) in pkgMeta.Depends)
            {
                if (!PackageList.TryGetValue(depName, out var installedVersion) || !depRange.Contains(installedVersion))
                {
                    reasons.Add(new MissingDependsUnacceptReason(pkgMeta.Name, depName, depRange));
                }
            }

            // 4. Check if this package replaces any already-installed package
            foreach (var (replacedPkg, _) in pkgMeta.Replaces)
            {
                if (PackageList.ContainsKey(replacedPkg))
                {
                    reasons.Add(new PackageReplacedUnacceptReason(pkgMeta.Name, replacedPkg));
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
        File.WriteAllText(
            System.IO.Path.Combine(
                RegistryFolder.FullName,
                initLetter.ToString(),
                $"{registry.Meta.Name}.json"),
            JsonSerializer.Serialize(registry));

        var newMngFileList = ManagedFiles;

        // Remove stale managed file entries that belong to this package
        // but are no longer in the registry's file list
        var staleEntries = newMngFileList
            .Where(kvp => kvp.Value.Owner == registry.Meta.Name && !registry.FileList.ContainsKey(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var staleFile in staleEntries)
        {
            newMngFileList.Remove(staleFile);
        }

        // Add or update managed file entries from the registry's file list
        foreach (var p in registry.FileList)
        {
            newMngFileList[p.Key] = new(registry.Meta.Name, p.Value);
        }

        ManagedFiles = newMngFileList;

        // Ensure the package is listed in the package list (with its version)
        var packList = PackageList;
        packList[registry.Meta.Name] = registry.Meta.Version;

        // Add virtual packages that this package provides
        foreach (var (virtualName, virtualVersion) in registry.Meta.Provides)
        {
            packList[virtualName] = virtualVersion;
        }

        PackageList = packList;

        // Sync deny list: add/update this package's conflict declarations
        var newDenyList = DenyList;
        if (registry.Meta.Conflicts.Count > 0)
        {
            newDenyList[registry.Meta.Name] = new Dictionary<string, VersionRange>(registry.Meta.Conflicts);
        }
        else
        {
            // No conflicts declared — remove this package's entry if it exists
            newDenyList.Remove(registry.Meta.Name);
        }
        DenyList = newDenyList;
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

        // Remove managed file entries owned by this package
        var newMngFileList = ManagedFiles;
        var toRemove = newMngFileList
            .Where(kvp => kvp.Value.Owner == packageName)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var file in toRemove)
        {
            newMngFileList.Remove(file);
        }
        ManagedFiles = newMngFileList;

        // Remove the package from the package list
        var packList = PackageList;
        packList.Remove(packageName);

        // Also remove any virtual packages that this package provides
        if (registry != null)
        {
            foreach (var virtualName in registry.Meta.Provides.Keys)
            {
                packList.Remove(virtualName);
            }
        }

        PackageList = packList;

        // Remove deny list entry for this package
        var newDenyList = DenyList;
        newDenyList.Remove(packageName);
        DenyList = newDenyList;
    }

    /// <summary>
    /// Remove all files owned by the specified package.
    /// </summary>
    /// <param name="packageName">The name of the package to remove.</param>
    /// <exception cref="Exception">Thrown when the package does not exist.</exception>
    public void RemovePackageFiles(string packageName)
    {
        var registry = GetRegistry(packageName)
            ?? throw new Exception($"Package '{packageName}' does not exist in the registry");

        var newMngFileList = ManagedFiles;
        foreach (var kvp in registry.FileList)
        {
            var filePath = System.IO.Path.Combine(Path, kvp.Key);
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // best-effort deletion
            }
            newMngFileList.Remove(kvp.Key);
        }
        ManagedFiles = newMngFileList;
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

        foreach (var fileName in fileNames)
        {
            // Check if the file is managed by a package not in the ignored list
            if (_mngFiles.TryGetValue(fileName, out var record))
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
        var previouslyOwned = ManagedFiles
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
    /// Gets the dictionary of installed packages and their versions.
    /// Includes virtual packages provided by installed packages.
    /// </summary>
    public Dictionary<string, VersionIdentifier> PackageList
    {
        get => _packList;
        private set
        {
            _packList = value;
            File.WriteAllText(PackListFile.FullName,
                JsonSerializer.Serialize(_packList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RawString)));
        }
    }

    /// <summary>
    /// Gets the list of managed files in this environment.
    /// </summary>
    public Dictionary<string, ManagedFileRecord> ManagedFiles
    {
        get => _mngFiles;
        private set
        {
            _mngFiles = value;
            File.WriteAllText(ManagedFileListFile.FullName, JsonSerializer.Serialize(_mngFiles));
        }
    }

    /// <summary>
    /// Gets the list of packages that are denied to be installed because of conflicts.
    /// Key is conflict source, value is a dictionary of (package name, version range).
    /// This property only serves as a cache of the <see cref="PackageMeta.Conflicts"/> properties of all packages,
    /// so that file conflicts are ignored - it is caller's responsibility to check file conflicts before trying to
    /// install a package. See <see cref="CheckConflictFiles(string[], string[])"/>.
    /// </summary>
    public Dictionary<string, Dictionary<string, VersionRange>> DenyList
    {
        get => _denyList;
        private set
        {
            _denyList = value;
            // Serialize as Dictionary<string, Dictionary<string, string>> so VersionRange is stored as its string
            var serializable = _denyList.ToDictionary(
                outer => outer.Key,
                outer => outer.Value.ToDictionary(
                    inner => inner.Key,
                    inner => inner.Value.ToString()));
            File.WriteAllText(DenyListFile.FullName, JsonSerializer.Serialize(serializable));
        }
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

