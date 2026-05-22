using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment.Indexing;

/// <summary>
/// Index handler that manages a JSON file (<c>deny_list.json</c>) mapping conflict source package names
/// to their denied packages and version ranges, serving as a conflict resolution index.
/// </summary>
public class DenyListHandler : IndexHandler<Dictionary<string, Dictionary<string, VersionRange>>>
{
    /// <summary>
    /// The filename used for the deny list JSON file.
    /// </summary>
    public const string FILE_NAME = "deny_list.json";

    protected override string FileName => FILE_NAME;

    private Dictionary<string, Dictionary<string, VersionRange>> _content;

    /// <summary>
    /// Initializes the handler with the specified index directory and an empty content dictionary.
    /// </summary>
    /// <param name="indexDirectory">The directory where index files are stored.</param>
    public DenyListHandler(DirectoryInfo indexDirectory) : base(indexDirectory)
    {
        _content = [];
    }

    /// <summary>
    /// Gets or sets the deny list mapping.
    /// Setting this property updates the in-memory state only.
    /// Persistence is handled separately via <see cref="IndexHandler{T}.Persist"/>.
    /// </summary>
    public override Dictionary<string, Dictionary<string, VersionRange>> Content
    {
        get => _content;
        set => _content = value;
    }

    /// <summary>
    /// Loads the deny list from the JSON file on disk.
    /// The <see cref="Content"/> is left empty if the file is unable to be read.
    /// </summary>
    public override void OnLoad()
    {
        try
        {
            _content = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, VersionRange>>>(
                File.ReadAllText(Path.Combine(IndexDirectory.FullName, FILE_NAME))) ?? [];
        }
        catch
        {
            _content = [];
        }
    }

    /// <summary>
    /// Rebuilds the deny list from the provided registry records, collecting
    /// conflict declarations. Only writes to disk if the scanned content differs
    /// from the current content.
    /// </summary>
    /// <param name="registries">The array of all registry entries to process.</param>
    /// <returns><c>true</c> if the rebuild was successful and the index was persisted; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(PackageRegistry[] registries)
    {
        var denyList = new Dictionary<string, Dictionary<string, VersionRange>>();
        foreach (var registry in registries)
        {
            if (registry.Meta.Conflicts.Count > 0)
            {
                denyList[registry.Meta.Name] = new Dictionary<string, VersionRange>(registry.Meta.Conflicts);
            }
        }
        if (!Persist(denyList))
            return false;
        _content = denyList;
        return true;
    }

    /// <summary>
    /// Adds or updates the deny list with the registry's conflict declarations when a registry entry is written.
    /// If the registry declares no conflicts, its entry is removed from the deny list.
    /// Persists to disk first; in-memory state is only updated on success.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    /// <returns><c>true</c> if the index was successfully persisted; otherwise, <c>false</c>.</returns>
    public override bool OnWriteRegistry(PackageRegistry registry)
    {
        // Build new state in isolation — no mutation of _content yet
        var newContent = new Dictionary<string, Dictionary<string, VersionRange>>(_content);
        foreach (var key in _content.Keys)
        {
            newContent[key] = new Dictionary<string, VersionRange>(_content[key]);
        }

        if (registry.Meta.Conflicts.Count > 0)
        {
            newContent[registry.Meta.Name] = new Dictionary<string, VersionRange>(registry.Meta.Conflicts);
        }
        else
        {
            // No conflicts declared — remove this package's entry if it exists
            newContent.Remove(registry.Meta.Name);
        }

        // Write to disk atomically first
        if (!Persist(newContent)) return false;

        // Only update in-memory state after successful disk write
        _content = newContent;
        return true;
    }

    /// <summary>
    /// Removes the deny list entry for the specified package when a registry is removed.
    /// If the package is not present in the list, this method does nothing.
    /// Persists to disk first; in-memory state is only updated on success.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    /// <returns><c>true</c> if the index was successfully persisted; otherwise, <c>false</c>.</returns>
    public override bool OnRemoveRegistry(PackageRegistry registry)
    {
        // Build new state in isolation
        var newContent = new Dictionary<string, Dictionary<string, VersionRange>>(_content);
        foreach (var key in _content.Keys)
        {
            newContent[key] = new Dictionary<string, VersionRange>(_content[key]);
        }

        newContent.Remove(registry.Meta.Name);

        // Write to disk atomically first
        if (!Persist(newContent)) return false;

        // Only update in-memory state after successful disk write
        _content = newContent;
        return true;
    }
}
