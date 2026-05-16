using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment.Indexing;

/// <summary>
/// Index handler that manages a JSON file (<c>package_list.json</c>) mapping package names
/// to their version identifiers, serving as a package listing index.
/// </summary>
public class PackageListHandler : IndexHandler<Dictionary<string, VersionIdentifier>>
{
    /// <summary>
    /// The filename used for the package list JSON file.
    /// </summary>
    public const string FILE_NAME = "package_list.json";

    private Dictionary<string, VersionIdentifier> _content;

    /// <summary>
    /// Initializes the handler with the specified index directory and an empty content dictionary.
    /// </summary>
    /// <param name="indexDirectory">The directory where index files are stored.</param>
    public PackageListHandler(DirectoryInfo indexDirectory) : base(indexDirectory)
    {
        _content = [];
    }

    /// <summary>
    /// Gets or sets the package name-to-version mapping and persists the data to disk on set.
    /// </summary>
    public override Dictionary<string, VersionIdentifier> Content
    {
        get => _content;
        set
        {
            _content = value;
            File.WriteAllText(Path.Combine(IndexDirectory.FullName, FILE_NAME),
                JsonSerializer.Serialize(_content.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RawString)));
        }
    }

    /// <summary>
    /// Loads the package list from the JSON file on disk. The <see cref="Content"/> is left empty if
    /// the file is unable to be read.
    /// </summary>
    public override void OnLoad()
    {
        try
        {
            var rawDict = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(IndexDirectory.FullName, FILE_NAME))) ?? [];
            _content = rawDict.ToDictionary(kvp => kvp.Key, kvp => new VersionIdentifier(kvp.Value));
        }
        catch
        {
            _content = [];
        }
    }

    /// <summary>
    /// Rebuilds the package list by scanning all registry records in the specified directory.
    /// Includes virtual packages from <see cref="PackageMeta.Provides"/>
    /// Only writes to disk if the scanned content differs from the current content.
    /// </summary>
    /// <param name="registryDirectory">The directory containing registry entries to scan.</param>
    /// <returns><c>true</c> if the content was altered during the rebuild; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(DirectoryInfo registryDirectory)
    {
        bool altered = false;

        var packageNames = new Dictionary<string, VersionIdentifier>();
        try
        {
            foreach (var subDir in registryDirectory.EnumerateDirectories())
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

        // Only write if the current list is different from the scanned result
        var serializedCurrent = JsonSerializer.Serialize(_content.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RawString));
        var serializedScanned = JsonSerializer.Serialize(packageNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RawString));
        if (serializedCurrent != serializedScanned)
        {
            Content = packageNames;
            altered = true;
        }

        return altered;
    }

    /// <summary>
    /// Adds or updates the package entry and any virtual packages from
    /// <see cref="PackageMeta.Provides"/> in the list when a registry entry is written.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    public override void OnWriteRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        newContent[registry.Meta.Name] = registry.Meta.Version;

        // Add virtual packages this package provides
        foreach (var (virtualName, virtualVersion) in registry.Meta.Provides)
        {
            newContent[virtualName] = virtualVersion;
        }

        Content = newContent;
    }

    /// <summary>
    /// Removes the package entry and any virtual packages from
    /// <see cref="PackageMeta.Provides"/> from the list when a registry is removed.
    /// If the package is not present in the list, this method does nothing.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    public override void OnRemoveRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        newContent.Remove(registry.Meta.Name);

        // Also remove any virtual packages that this package provides
        foreach (var virtualName in registry.Meta.Provides.Keys)
        {
            newContent.Remove(virtualName);
        }

        Content = newContent;
    }
}