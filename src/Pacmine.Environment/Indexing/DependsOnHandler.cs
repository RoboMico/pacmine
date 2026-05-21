using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment.Indexing;

/// <summary>
/// Index handler that manages a JSON file (<c>depends_on.json</c>) mapping dependency names
/// (real or virtual) to the list of packages that declare them as dependencies,
/// serving as a reverse dependency index.
/// </summary>
/// <remarks>
/// Content type: <c>Dictionary&lt;string, List&lt;string&gt;&gt;</c>
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
    /// Gets or sets the dependency name-to-dependents mapping and persists the data to disk on set.
    /// </summary>
    public override Dictionary<string, List<string>> Content
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
    /// <returns><c>true</c> if the content was altered during the rebuild; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(PackageRegistry[] registries)
    {
        bool altered = false;

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

        var serializedCurrent = JsonSerializer.Serialize(_content);
        var serializedScanned = JsonSerializer.Serialize(dependsOn);
        if (serializedCurrent != serializedScanned)
        {
            Content = dependsOn;
            altered = true;
        }

        return altered;
    }

    /// <summary>
    /// Adds or updates dependency entries when a registry entry is written.
    /// First removes all previous entries for this package name, then adds the current
    /// dependency declarations from <see cref="PackageMeta.Depends"/>.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    public override void OnWriteRegistry(PackageRegistry registry)
    {
        var newContent = Content;

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

        Content = newContent;
    }

    /// <summary>
    /// Removes all dependency entries for the specified package when a registry is removed.
    /// Cleans up any dependency keys that become empty after removal.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    public override void OnRemoveRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        RemovePackageEntries(newContent, registry.Meta.Name);
        Content = newContent;
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
