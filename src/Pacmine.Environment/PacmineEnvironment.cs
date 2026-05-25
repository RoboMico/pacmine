using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pacmine.Environment;

/// <summary>
/// Manages a Pacmine environment, which represents a game instance directory
/// with package registry, lock file, and meta info management.
/// </summary>
public class PacmineEnvironment : IDisposable
{
    private static JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
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
    /// The name of the package list file used as a cache storing names of all installed packages.
    /// Each line in this plain-text file contains one package name.
    /// </summary>
    public const string PACKAGE_LIST_FILE_NAME = "package_list";

    private PacmineEnvironment(string path)
    {
        RootPath = path;
        SpecialFolder = new(Path.Combine(path, SPECIAL_FOLDER_NAME));
        RegistryFolder = new(Path.Combine(SpecialFolder.FullName, REGISTRY_FOLDER_NAME));
        LockFile = new(Path.Combine(SpecialFolder.FullName, LOCKFILE_NAME));
        PackageListFile = new(Path.Combine(SpecialFolder.FullName, PACKAGE_LIST_FILE_NAME));
        PackageRegistry = [];
    }

    /// <summary>
    /// Gets the root path of the environment.
    /// </summary>
    public string RootPath { get; private set; }

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
    public FileInfo PackageListFile { get; private set; }

    /// <summary>
    /// Gets the full in-memory copy of the package registry, keyed by package name.
    /// Populated at construction time from the on-disk registry and updated dynamically
    /// by registry operations. Can be manually refreshed by calling <see cref="Scan"/>.
    /// </summary>
    public Dictionary<string, PackageRegistry> PackageRegistry { get; private set; }

    /// <summary>
    /// Scans the registry folder for all package JSON files, reloads the in-memory
    /// <see cref="PackageRegistry"/> dictionary, and rebuilds the <c>package_list</c>
    /// cache file from scratch.
    /// </summary>
    /// <remarks>
    /// This method ignores the existing <c>package_list</c> file and enumerates all
    /// <c>.json</c> files directly under the registry folder. After scanning, the
    /// <c>package_list</c> is rewritten to match the in-memory dictionary keys.
    /// </remarks>
    public void Scan()
    {
        PackageRegistry.Clear();

        if (!RegistryFolder.Exists)
            return;

        // Enumerate all .json files recursively under the registry folder
        foreach (var jsonFile in RegistryFolder.EnumerateFiles("*.json", SearchOption.AllDirectories))
        {
            try
            {
                var registry = JsonSerializer.Deserialize<PackageRegistry>(
                    File.ReadAllText(jsonFile.FullName));
                if (registry != null)
                    PackageRegistry[registry.Meta.Name] = registry;
            }
            catch
            {
                // Skip corrupt or unreadable files
            }
        }

        SavePackageNameList();
    }

    /// <summary>
    /// Loads the package registry into memory using the <c>package_list</c> file
    /// as a reference. For each package name listed in the file, the corresponding
    /// JSON file in the registry folder is deserialized and added to <see cref="PackageRegistry"/>.
    /// </summary>
    private void LoadRegistryFromDisk()
    {
        var names = LoadPackageNameList();
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            char initLetter = name[0];
            var registryFile = new FileInfo(Path.Combine(
                RegistryFolder.FullName,
                initLetter.ToString(),
                $"{name}.json"));

            if (!registryFile.Exists)
                continue;

            try
            {
                var registry = JsonSerializer.Deserialize<PackageRegistry>(
                    File.ReadAllText(registryFile.FullName));
                if (registry != null)
                    PackageRegistry[registry.Meta.Name] = registry;
            }
            catch
            {
                // Skip corrupt or unreadable files
            }
        }
    }

    /// <summary>
    /// Reads the <c>package_list</c> file and returns the list of package names.
    /// Each line in the file corresponds to one package name.
    /// </summary>
    private List<string> LoadPackageNameList()
    {
        if (!PackageListFile.Exists)
            return [];

        try
        {
            return [.. File.ReadAllLines(PackageListFile.FullName)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Writes the <c>package_list</c> file with one package name per line,
    /// derived from the current keys of the in-memory <see cref="PackageRegistry"/>.
    /// </summary>
    private void SavePackageNameList()
    {
        try
        {
            File.WriteAllLines(PackageListFile.FullName, PackageRegistry.Keys);
        }
        catch
        {
            // Best-effort write
        }
    }

    private static void LockDirectory(string path, out FileStream lockStream)
    {
        try
        {
            lockStream = new FileStream(
                Path.Combine(path, SPECIAL_FOLDER_NAME, LOCKFILE_NAME),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read);
            lockStream.SetLength(0);
            var pidBytes = Encoding.UTF8.GetBytes(System.Environment.ProcessId.ToString());
            lockStream.Write(pidBytes);
            lockStream.Flush();
        }
        catch (IOException ex)
        {
            var pid = GetLockerPid(path);
            var extra = pid >= 0 ? $"(locked by process {pid})" : "";
            throw new IOException($"Unable to access the environment{extra}.", ex);
        }
    }

    private void Lock()
    {
        LockDirectory(RootPath, out _lockStream);
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
        var lockFile = new FileInfo(Path.Combine(directory, SPECIAL_FOLDER_NAME, LOCKFILE_NAME));
        if (!lockFile.Exists)
            return -1;

        try
        {
            using var fs = new FileStream(
                lockFile.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
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
        if (!Directory.Exists(Path.Combine(directory, SPECIAL_FOLDER_NAME)))
            throw new Exception("Invalid environment directory");

        PacmineEnvironment env = new(directory);
        try
        {
            env.Lock();
            env.LoadRegistryFromDisk();
            return env;
        }
        catch
        {
            env.Dispose();
            throw;
        }

    }

    /// <summary>
    /// Creates a new Pacmine environment in the specified directory and acquires the lock.
    /// </summary>
    /// <param name="directory">The directory to initialize as the environment root.</param>
    /// <returns>The newly created <see cref="PacmineEnvironment"/> instance.</returns>
    /// <exception cref="Exception">Thrown when an environment already exists in the directory.</exception>
    public static PacmineEnvironment Create(string directory)
    {
        string spFolderPath = Path.Combine(directory, SPECIAL_FOLDER_NAME);
        if (Directory.Exists(spFolderPath))
            throw new Exception("Environment already exists");

        Directory.CreateDirectory(spFolderPath);
        LockDirectory(directory, out FileStream lockStream);
        try
        {
            var registryFolder = Directory.CreateDirectory(Path.Combine(spFolderPath, REGISTRY_FOLDER_NAME));
            PacmineEnvironment env = new(directory);
            env.PackageListFile.Create().Dispose();
            env._lockStream = lockStream;
            return env;
        }
        catch
        {
            lockStream.Dispose();
            Directory.Delete(spFolderPath, true);
            throw;
        }
    }

    /// <summary>
    /// Writes a package registry record to disk and updates the in-memory
    /// <see cref="PackageRegistry"/> dictionary and the <c>package_list</c> cache.
    /// </summary>
    /// <param name="registry">The package registry to write.</param>
    /// <returns><c>true</c> if the registry was successfully written; <c>false</c> otherwise.</returns>
    public bool TryWriteRegistry(PackageRegistry registry)
    {
        char initLetter = registry.Meta.Name[0];
        DirectoryInfo layerDir = new(Path.Combine(RegistryFolder.FullName, initLetter.ToString()));
        if (!layerDir.Exists) layerDir.Create();
        string targetPath = Path.Combine(layerDir.FullName, $"{registry.Meta.Name}.json");
        string tmpPath = targetPath + ".tmp";
        string restorePath = targetPath + ".old";
        try
        {
            File.WriteAllText(tmpPath, JsonSerializer.Serialize(registry, _jsonOptions));
            if (File.Exists(targetPath))
                File.Move(targetPath, restorePath, true);
            File.Move(tmpPath, targetPath, true);
            if (File.Exists(restorePath))
                File.Delete(restorePath);
            // Update in-memory dictionary and name list cache
            PackageRegistry[registry.Meta.Name] = registry;
            SavePackageNameList();
        }
        catch
        {
            try
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
                if (File.Exists(restorePath))
                {
                    File.Move(restorePath, targetPath, true);
                    File.Delete(restorePath);
                }
            }
            catch
            {
                // best-effort restore
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// Removes a package registry record from disk and from the in-memory
    /// <see cref="PackageRegistry"/> dictionary and the <c>package_list</c> cache.
    /// </summary>
    /// <param name="packageName">The name of the package to remove.</param>
    /// <returns><c>true</c> if the package was successfully removed;
    /// <c>false</c> otherwise or if the package does not exist.</returns>
    public bool TryRemoveRegistry(string packageName)
    {
        char initLetter = packageName[0];
        var registryFile = new FileInfo(Path.Combine(
            RegistryFolder.FullName,
            initLetter.ToString(),
            $"{packageName}.json"));

        if (!registryFile.Exists)
            return false;

        try
        {
            registryFile.Delete();
            // Remove from in-memory dictionary and name list cache
            PackageRegistry.Remove(packageName);
            SavePackageNameList();
        }
        catch
        {
            // best-effort attempt, no way to restore
            return false;
        }
        return true;
    }

    /// <summary>
    /// Remove all files owned by the specified package from disk.
    /// This is a pure file system operation and has no effect on the registry or name list.
    /// </summary>
    /// <param name="packageName">The name of the package whose files to remove.</param>
    public void RemovePackageFiles(string packageName)
    {
        if (!PackageRegistry.TryGetValue(packageName, out var reg))
            return;

        foreach (var filePath in reg.FileList.Keys)
        {
            var fullPath = Path.Combine(RootPath, filePath);
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
        // Build file-to-owner mapping from the in-memory package registry
        var mngFiles = new Dictionary<string, string>();
        foreach (var (pkgName, pkgReg) in PackageRegistry)
        {
            foreach (var filePath in pkgReg.FileList.Keys)
            {
                // Last write wins if multiple packages claim the same file
                // (impossible case in a valid environment, but just in case)
                mngFiles[filePath] = pkgName;
            }
        }

        var conflicts = new Dictionary<string, string>();

        foreach (var fileName in fileNames)
        {
            // Check if the file is managed by a package not in the ignored list
            if (mngFiles.TryGetValue(fileName, out var owner))
            {
                if (!ignoredOwners.Contains(owner))
                {
                    conflicts[fileName] = owner;
                }
            }
            // Check if the file exists on disk but is not managed (orphan file)
            else
            {
                var fullPath = Path.Combine(RootPath, fileName);
                if (File.Exists(fullPath))
                {
                    conflicts[fileName] = string.Empty;
                }
            }
        }

        return conflicts;
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
    /// This is a pure file system operation and has no effect on the registry or name list.
    /// The caller should use <see cref="TryWriteRegistry"/> separately to persist the returned
    /// file list as part of a <see cref="PackageRegistry"/>.
    /// </remarks>
    public Dictionary<string, string> UpdateFiles(string owner, DirectoryInfo source)
    {
        if (!source.Exists)
            throw new DirectoryNotFoundException($"Source directory '{source.FullName}' does not exist");

        var fileList = new Dictionary<string, string>();
        var sourceFiles = source.EnumerateFiles("*", SearchOption.AllDirectories);

        // Track which files were previously owned by this package
        var previouslyOwned = PackageRegistry.TryGetValue(owner, out var existing)
            ? existing.FileList.Keys.ToHashSet()
            : [];

        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(source.FullName, sourceFile.FullName);
            var targetPath = Path.Combine(RootPath, relativePath);

            var targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir != null)
                Directory.CreateDirectory(targetDir);

            File.Copy(sourceFile.FullName, targetPath, overwrite: true);

            // Compute SHA256 checksum
            using var sha256 = SHA256.Create();
            using var fs = sourceFile.OpenRead();
            var hashBytes = sha256.ComputeHash(fs);
            var checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();

            fileList[relativePath] = checksum;
            previouslyOwned.Remove(relativePath);
        }

        // Remove stale files that are no longer in the source directory
        foreach (var staleFile in previouslyOwned)
        {
            var fullPath = Path.Combine(RootPath, staleFile);
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
