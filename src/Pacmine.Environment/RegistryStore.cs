using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment;

/// <summary>
/// Manages the on-disk and in-memory store of installed package metadata.
/// Provides atomic writes via tmp→target rename and maintains a <c>package_list</c>
/// plain-text index for fast loading.
/// </summary>
public class RegistryStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly Dictionary<string, PackageRegistry> _packages = [];

    private readonly DirectoryInfo _registryFolder;
    private readonly FileInfo _packageListFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryStore"/> class.
    /// Loads the registry from disk using the <c>package_list</c> file as a reference.
    /// </summary>
    /// <param name="registryFolder">The registry folder containing sharded JSON files.</param>
    /// <param name="packageListFile">The package list file for fast name-based lookup.</param>
    public RegistryStore(DirectoryInfo registryFolder, FileInfo packageListFile)
    {
        _registryFolder = registryFolder;
        _packageListFile = packageListFile;
        LoadRegistryFromDisk();
    }

    // ── Reads ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the registry entry for the specified package, or <c>null</c> if not found.
    /// </summary>
    public PackageRegistry? TryGet(string packageName)
    {
        _packages.TryGetValue(packageName, out var reg);
        return reg;
    }

    /// <summary>
    /// Gets a read-only view of all package registry entries.
    /// </summary>
    public IReadOnlyDictionary<string, PackageRegistry> GetAll()
    {
        return _packages.AsReadOnly();
    }

    /// <summary>
    /// Gets the metadata for all installed packages.
    /// </summary>
    public PackageMeta[] GetAllMetas()
    {
        return _packages.Values.Select(r => r.Meta).ToArray();
    }

    /// <summary>
    /// Checks whether a package with the specified name exists in the registry.
    /// </summary>
    public bool Contains(string packageName)
    {
        return _packages.ContainsKey(packageName);
    }

    /// <summary>
    /// Gets the number of packages in the registry.
    /// </summary>
    public int Count => _packages.Count;

    // ── Writes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a package registry record to disk (atomic write) and updates the in-memory store.
    /// </summary>
    /// <param name="registry">The package registry to write.</param>
    /// <returns><c>true</c> if the registry was successfully written; <c>false</c> otherwise.</returns>
    public bool Write(PackageRegistry registry)
    {
        char initLetter = registry.Meta.Name[0];
        DirectoryInfo layerDir = new(Path.Combine(_registryFolder.FullName, initLetter.ToString()));
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
            _packages[registry.Meta.Name] = registry;
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
    /// Removes a package registry record from disk and from the in-memory store.
    /// </summary>
    /// <param name="packageName">The name of the package to remove.</param>
    /// <returns><c>true</c> if the package was successfully removed;
    /// <c>false</c> otherwise or if the package does not exist.</returns>
    public bool Remove(string packageName)
    {
        char initLetter = packageName[0];
        var registryFile = new FileInfo(Path.Combine(
            _registryFolder.FullName,
            initLetter.ToString(),
            $"{packageName}.json"));

        if (!registryFile.Exists)
            return false;

        try
        {
            registryFile.Delete();
            // Remove from in-memory dictionary and name list cache
            _packages.Remove(packageName);
            SavePackageNameList();
        }
        catch
        {
            // best-effort attempt, no way to restore
            return false;
        }
        return true;
    }

    // ── Maintenance ────────────────────────────────────────────────────────

    /// <summary>
    /// Scans the registry folder for all package JSON files, reloads the in-memory
    /// store, and rebuilds the <c>package_list</c> cache file from scratch.
    /// </summary>
    /// <remarks>
    /// This method ignores the existing <c>package_list</c> file and enumerates all
    /// <c>.json</c> files directly under the registry folder. After scanning, the
    /// <c>package_list</c> is rewritten to match the in-memory dictionary keys.
    /// </remarks>
    public void Scan()
    {
        _packages.Clear();

        if (!_registryFolder.Exists)
            return;

        // Enumerate all .json files recursively under the registry folder
        foreach (var jsonFile in _registryFolder.EnumerateFiles("*.json", SearchOption.AllDirectories))
        {
            try
            {
                var registry = JsonSerializer.Deserialize<PackageRegistry>(
                    File.ReadAllText(jsonFile.FullName));
                if (registry != null)
                    _packages[registry.Meta.Name] = registry;
            }
            catch
            {
                // Skip corrupt or unreadable files
            }
        }

        SavePackageNameList();
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Loads the package registry into memory using the <c>package_list</c> file
    /// as a reference. For each package name listed in the file, the corresponding
    /// JSON file in the registry folder is deserialized and added to the store.
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
                _registryFolder.FullName,
                initLetter.ToString(),
                $"{name}.json"));

            if (!registryFile.Exists)
                continue;

            try
            {
                var registry = JsonSerializer.Deserialize<PackageRegistry>(
                    File.ReadAllText(registryFile.FullName));
                if (registry != null)
                    _packages[registry.Meta.Name] = registry;
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
        if (!_packageListFile.Exists)
            return [];

        try
        {
            return [.. File.ReadAllLines(_packageListFile.FullName)
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
    /// derived from the current keys of the in-memory store.
    /// </summary>
    private void SavePackageNameList()
    {
        try
        {
            File.WriteAllLines(_packageListFile.FullName, _packages.Keys);
        }
        catch
        {
            // Best-effort write
        }
    }
}
