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

    protected override string FileName => FILE_NAME;

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
    /// Gets or sets the package name-to-version mapping.
    /// Setting this property updates the in-memory state only.
    /// Persistence is handled separately via <see cref="IndexHandler{T}.Persist"/>.
    /// </summary>
    public override Dictionary<string, VersionIdentifier> Content
    {
        get => _content;
        set => _content = value;
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
    /// <returns><c>true</c> if the rebuild was successful and the index was persisted; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(PackageRegistry[] registries)
    {
        var packageNames = new Dictionary<string, VersionIdentifier>();
        foreach (var registry in registries)
        {
            packageNames[registry.Meta.Name] = registry.Meta.Version;
        }

        if (!Persist(_content))
            return false;
        _content = packageNames;
        return true;
    }

    /// <summary>
    /// Adds or updates the package entry in the list when a registry entry is written.
    /// Persists to disk first; in-memory state is only updated on success.
    /// Note: virtual packages are managed separately by <see cref="VirtualPackagesHandler"/>.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    /// <returns><c>true</c> if the index was successfully persisted; otherwise, <c>false</c>.</returns>
    public override bool OnWriteRegistry(PackageRegistry registry)
    {
        // Build new state in isolation — no mutation of _content yet
        var newContent = new Dictionary<string, VersionIdentifier>(_content)
        {
            [registry.Meta.Name] = registry.Meta.Version
        };

        // Write to disk atomically first
        if (!Persist(newContent)) return false;

        // Only update in-memory state after successful disk write
        _content = newContent;
        return true;
    }

    /// <summary>
    /// Removes the package entry from the list when a registry is removed.
    /// Persists to disk first; in-memory state is only updated on success.
    /// Note: virtual packages are managed separately by <see cref="VirtualPackagesHandler"/>.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    /// <returns><c>true</c> if the index was successfully persisted; otherwise, <c>false</c>.</returns>
    public override bool OnRemoveRegistry(PackageRegistry registry)
    {
        // Build new state in isolation
        var newContent = new Dictionary<string, VersionIdentifier>(_content);
        newContent.Remove(registry.Meta.Name);

        // Write to disk atomically first
        if (!Persist(newContent)) return false;

        // Only update in-memory state after successful disk write
        _content = newContent;
        return true;
    }
}
