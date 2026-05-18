using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment.Indexing;

/// <summary>
/// Index handler that manages a JSON file (<c>virtual_packages.json</c>) mapping virtual package names
/// to their versions and provider packages, serving as a virtual package index.
/// A single virtual package (name + version) can be provided by multiple packages.
/// </summary>
/// <remarks>
/// Content type: <c>Dictionary<string, Dictionary<VersionIdentifier, List<string>>></c>
/// <list type="bullet">
///   <item>Outer key: virtual package name</item>
///   <item>Middle key: version identifier</item>
///   <item>Inner value: list of provider package names</item>
/// </list>
/// </remarks>
public class VirtualPackagesHandler : IndexHandler<Dictionary<string, Dictionary<VersionIdentifier, List<string>>>>
{
    /// <summary>
    /// The filename used for the virtual packages JSON file.
    /// </summary>
    public const string FILE_NAME = "virtual_packages.json";

    private Dictionary<string, Dictionary<VersionIdentifier, List<string>>> _content;

    /// <summary>
    /// Initializes the handler with the specified index directory and an empty content dictionary.
    /// </summary>
    /// <param name="indexDirectory">The directory where index files are stored.</param>
    public VirtualPackagesHandler(DirectoryInfo indexDirectory) : base(indexDirectory)
    {
        _content = [];
    }

    /// <summary>
    /// Gets or sets the virtual package mapping and persists the data to disk on set.
    /// </summary>
    public override Dictionary<string, Dictionary<VersionIdentifier, List<string>>> Content
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
    /// Loads the virtual packages from the JSON file on disk.
    /// The <see cref="Content"/> is left empty if the file is unable to be read.
    /// </summary>
    public override void OnLoad()
    {
        try
        {
            _content = JsonSerializer.Deserialize<Dictionary<string, Dictionary<VersionIdentifier, List<string>>>>(
                File.ReadAllText(Path.Combine(IndexDirectory.FullName, FILE_NAME))) ?? [];
        }
        catch
        {
            _content = [];
        }
    }

    /// <summary>
    /// Rebuilds the virtual package index from the provided registry records.
    /// Collects all virtual packages from <see cref="PackageMeta.Provides"/>, aggregating
    /// multiple providers for the same virtual package name and version.
    /// Only writes to disk if the scanned content differs from the current content.
    /// </summary>
    /// <param name="registries">The array of all registry entries to process.</param>
    /// <returns><c>true</c> if the content was altered during the rebuild; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(PackageRegistry[] registries)
    {
        bool altered = false;

        var virtualPkgs = new Dictionary<string, Dictionary<VersionIdentifier, List<string>>>();
        foreach (var registry in registries)
        {
            foreach (var (virtualName, virtualVersion) in registry.Meta.Provides)
            {
                if (!virtualPkgs.TryGetValue(virtualName, out var versionDict))
                {
                    versionDict = [];
                    virtualPkgs[virtualName] = versionDict;
                }

                if (!versionDict.TryGetValue(virtualVersion, out var providers))
                {
                    providers = [];
                    versionDict[virtualVersion] = providers;
                }

                if (!providers.Contains(registry.Meta.Name))
                {
                    providers.Add(registry.Meta.Name);
                }
            }
        }

        var serializedCurrent = JsonSerializer.Serialize(_content);
        var serializedScanned = JsonSerializer.Serialize(virtualPkgs);
        if (serializedCurrent != serializedScanned)
        {
            Content = virtualPkgs;
            altered = true;
        }

        return altered;
    }

    /// <summary>
    /// Adds or updates virtual package entries when a registry entry is written.
    /// First removes all previous entries for this provider, then adds the current
    /// virtual package declarations from <see cref="PackageMeta.Provides"/>.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    public override void OnWriteRegistry(PackageRegistry registry)
    {
        var newContent = Content;

        // First, remove all previous entries for this provider
        RemoveProviderEntries(newContent, registry.Meta.Name);

        // Then, add the new entries
        foreach (var (virtualName, virtualVersion) in registry.Meta.Provides)
        {
            if (!newContent.TryGetValue(virtualName, out var versionDict))
            {
                versionDict = [];
                newContent[virtualName] = versionDict;
            }

            if (!versionDict.TryGetValue(virtualVersion, out var providers))
            {
                providers = [];
                versionDict[virtualVersion] = providers;
            }

            if (!providers.Contains(registry.Meta.Name))
            {
                providers.Add(registry.Meta.Name);
            }
        }

        Content = newContent;
    }

    /// <summary>
    /// Removes all virtual package entries for the specified package when a registry is removed.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    public override void OnRemoveRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        RemoveProviderEntries(newContent, registry.Meta.Name);
        Content = newContent;
    }

    private static void RemoveProviderEntries(
        Dictionary<string, Dictionary<VersionIdentifier, List<string>>> content,
        string providerName)
    {
        var emptyVirtualNames = new List<string>();

        foreach (var (virtualName, versionDict) in content)
        {
            var emptyVersions = new List<VersionIdentifier>();

            foreach (var (version, providers) in versionDict)
            {
                providers.Remove(providerName);
                if (providers.Count == 0)
                    emptyVersions.Add(version);
            }

            foreach (var version in emptyVersions)
                versionDict.Remove(version);

            if (versionDict.Count == 0)
                emptyVirtualNames.Add(virtualName);
        }

        foreach (var virtualName in emptyVirtualNames)
            content.Remove(virtualName);
    }
}
