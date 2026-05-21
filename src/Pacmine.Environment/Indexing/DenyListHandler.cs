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
    /// Gets or sets the deny list mapping and persists the data to disk on set.
    /// </summary>
    public override Dictionary<string, Dictionary<string, VersionRange>> Content
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
    /// <returns><c>true</c> if the content was altered during the rebuild; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(PackageRegistry[] registries)
    {
        bool altered = false;

        var denyList = new Dictionary<string, Dictionary<string, VersionRange>>();
        foreach (var registry in registries)
        {
            if (registry.Meta.Conflicts.Count > 0)
            {
                denyList[registry.Meta.Name] = new Dictionary<string, VersionRange>(registry.Meta.Conflicts);
            }
        }

        var serializedCurrent = JsonSerializer.Serialize(_content);
        var serializedScanned = JsonSerializer.Serialize(denyList);
        if (serializedCurrent != serializedScanned)
        {
            Content = denyList;
            altered = true;
        }

        return altered;
    }

    /// <summary>
    /// Adds or updates the deny list with the registry's conflict declarations when a registry entry is written.
    /// If the registry declares no conflicts, its entry is removed from the deny list.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    public override void OnWriteRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        if (registry.Meta.Conflicts.Count > 0)
        {
            newContent[registry.Meta.Name] = new Dictionary<string, VersionRange>(registry.Meta.Conflicts);
        }
        else
        {
            // No conflicts declared — remove this package's entry if it exists
            newContent.Remove(registry.Meta.Name);
        }
        Content = newContent;
    }

    /// <summary>
    /// Removes the deny list entry for the specified package when a registry is removed.
    /// If the package is not present in the list, this method does nothing.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    public override void OnRemoveRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        newContent.Remove(registry.Meta.Name);
        Content = newContent;
    }
}
