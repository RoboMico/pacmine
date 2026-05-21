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
                JsonSerializer.Serialize(_content));
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
            _content = JsonSerializer.Deserialize<Dictionary<string, VersionIdentifier>>(
                File.ReadAllText(Path.Combine(IndexDirectory.FullName, FILE_NAME))) ?? [];
        }
        catch
        {
            _content = [];
        }
    }

    /// <summary>
    /// Rebuilds the package list from the provided registry records.
    /// Only writes to disk if the scanned content differs from the current content.
    /// Note: virtual packages are managed separately by <see cref="VirtualPackagesHandler"/>.
    /// </summary>
    /// <param name="registries">The array of all registry entries to process.</param>
    /// <returns><c>true</c> if the content was altered during the rebuild; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(PackageRegistry[] registries)
    {
        bool altered = false;

        var packageNames = new Dictionary<string, VersionIdentifier>();
        foreach (var registry in registries)
        {
            packageNames[registry.Meta.Name] = registry.Meta.Version;
        }

        // Only write if the current list is different from the scanned result
        var serializedCurrent = JsonSerializer.Serialize(_content);
        var serializedScanned = JsonSerializer.Serialize(packageNames);
        if (serializedCurrent != serializedScanned)
        {
            Content = packageNames;
            altered = true;
        }

        return altered;
    }

    /// <summary>
    /// Adds or updates the package entry in the list when a registry entry is written.
    /// Note: virtual packages are managed separately by <see cref="VirtualPackagesHandler"/>.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    public override void OnWriteRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        newContent[registry.Meta.Name] = registry.Meta.Version;
        Content = newContent;
    }

    /// <summary>
    /// Removes the package entry from the list when a registry is removed.
    /// Note: virtual packages are managed separately by <see cref="VirtualPackagesHandler"/>.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    public override void OnRemoveRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        newContent.Remove(registry.Meta.Name);
        Content = newContent;
    }
}