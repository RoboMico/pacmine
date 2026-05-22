using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment.Indexing;

/// <summary>
/// Index handler that manages a JSON file (<c>depends_on.json</c>) mapping dependency names
/// (real or virtual) to the list of packages that declare them as dependencies,
/// serving as a reverse dependency index.
/// </summary>
/// <remarks>
/// Content type: <c>Dictionary<string, List<string>></c>
/// <list type="bullet">
///   <item>Key: A package name (real or virtual) that is depended upon</item>
///   <item>Value: List of package names that declare this as a dependency</item>
/// </list>
/// </remarks>
public class DependsOnHandler : IndexHandler<Dictionary<string, List<string>>>
{
    /// <summary>
    /// The filename used for the dependency mapping JSON file.
    /// </summary>
    public const string FILE_NAME = "depends_on.json";

    protected override string FileName => FILE_NAME;

    private Dictionary<string, List<string>> _content;

    /// <summary>
    /// Initializes the handler with the specified index directory and an empty content dictionary.
    /// </summary>
    /// <param name="indexDirectory">The directory where index files are stored.</param>
    public DependsOnHandler(DirectoryInfo indexDirectory) : base(indexDirectory)
    {
        _content = [];
    }

    /// <summary>
    /// Gets or sets the dependency name-to-dependents mapping.
    /// Setting this property updates the in-memory state only.
    /// Persistence is handled separately via <see cref="IndexHandler{T}.Persist"/>.
    /// </summary>
    public override Dictionary<string, List<string>> Content
    {
        get => _content;
        set => _content = value;
    }

    /// <summary>
    /// Loads the dependency mapping from the JSON file on disk.
    /// The <see cref="Content"/> is left empty if the file is unable to be read.
    /// </summary>
    public override void OnLoad()
    {
        try
        {
            _content = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
                File.ReadAllText(Path.Combine(IndexDirectory.FullName, FILE_NAME))) ?? [];
        }
        catch
        {
            _content = [];
        }
    }

    /// <summary>
    /// Rebuilds the dependency mapping from the provided registry records.
    /// For each registry, for each dependency declared in <see cref="PackageMeta.Depends"/>,
    /// adds the registry's package name to the list of dependents for that dependency.
    /// Only writes to disk if the scanned content differs from the current content.
    /// </summary>
    /// <param name="registries">The array of all registry entries to process.</param>
    /// <returns><c>true</c> if the rebuild was successful and the index was persisted; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(PackageRegistry[] registries)
    {
        var dependsOn = new Dictionary<string, List<string>>();
        foreach (var registry in registries)
        {
            foreach (var (depName, _) in registry.Meta.Depends)
            {
                if (!dependsOn.TryGetValue(depName, out var dependents))
                {
                    dependents = [];
                    dependsOn[depName] = dependents;
                }

                if (!dependents.Contains(registry.Meta.Name))
                {
                    dependents.Add(registry.Meta.Name);
                }
            }
        }

        if (!Persist(dependsOn))
            return false;
        _content = dependsOn;
        return true;
    }

    /// <summary>
    /// Adds or updates dependency entries when a registry entry is written.
    /// First removes all previous entries for this package name, then adds the current
    /// dependency declarations from <see cref="PackageMeta.Depends"/>.
    /// Persists to disk first; in-memory state is only updated on success.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    /// <returns><c>true</c> if the index was successfully persisted; otherwise, <c>false</c>.</returns>
    public override bool OnWriteRegistry(PackageRegistry registry)
    {
        // Build new state in isolation — no mutation of _content yet
        var newContent = new Dictionary<string, List<string>>(_content);
        foreach (var key in _content.Keys)
        {
            newContent[key] = new List<string>(_content[key]);
        }

        // First, remove all previous entries for this package
        RemovePackageEntries(newContent, registry.Meta.Name);

        // Then, add the new entries
        foreach (var (depName, _) in registry.Meta.Depends)
        {
            if (!newContent.TryGetValue(depName, out var dependents))
            {
                dependents = [];
                newContent[depName] = dependents;
            }

            if (!dependents.Contains(registry.Meta.Name))
            {
                dependents.Add(registry.Meta.Name);
            }
        }

        // Write to disk atomically first
        if (!Persist(newContent)) return false;

        // Only update in-memory state after successful disk write
        _content = newContent;
        return true;
    }

    /// <summary>
    /// Removes all dependency entries for the specified package when a registry is removed.
    /// Cleans up any dependency keys that become empty after removal.
    /// Persists to disk first; in-memory state is only updated on success.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    /// <returns><c>true</c> if the index was successfully persisted; otherwise, <c>false</c>.</returns>
    public override bool OnRemoveRegistry(PackageRegistry registry)
    {
        // Build new state in isolation
        var newContent = new Dictionary<string, List<string>>(_content);
        foreach (var key in _content.Keys)
        {
            newContent[key] = new List<string>(_content[key]);
        }

        RemovePackageEntries(newContent, registry.Meta.Name);

        // Write to disk atomically first
        if (!Persist(newContent)) return false;

        // Only update in-memory state after successful disk write
        _content = newContent;
        return true;
    }

    /// <summary>
    /// Removes all references to the specified package name from all dependency lists,
    /// cleaning up empty entries afterward.
    /// </summary>
    /// <param name="content">The content dictionary to modify.</param>
    /// <param name="packageName">The package name to remove from all dependency lists.</param>
    private static void RemovePackageEntries(
        Dictionary<string, List<string>> content,
        string packageName)
    {
        var emptyKeys = new List<string>();

        foreach (var (depName, dependents) in content)
        {
            dependents.Remove(packageName);
            if (dependents.Count == 0)
                emptyKeys.Add(depName);
        }

        foreach (var key in emptyKeys)
            content.Remove(key);
    }
}
