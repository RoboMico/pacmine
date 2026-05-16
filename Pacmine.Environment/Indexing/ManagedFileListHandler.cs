using System.Text.Json;
using Pacmine.Core;

namespace Pacmine.Environment.Indexing;

/// <summary>
/// Represents a file managed by a package, tracking its owner and SHA256 checksum.
/// </summary>
/// <param name="Owner">The name of the package that owns this file.</param>
/// <param name="SHA256">The SHA256 checksum of the file contents.</param>
public record ManagedFileRecord(string Owner, string SHA256);

/// <summary>
/// Index handler that manages a JSON file (<c>managed_files.json</c>) mapping file paths
/// to their <see cref="ManagedFileRecord"/> entries, serving as a managed file index.
/// </summary>
public class ManagedFileListHandler : IndexHandler<Dictionary<string, ManagedFileRecord>>
{
    /// <summary>
    /// The filename used for the managed file list JSON file.
    /// </summary>
    public const string FILE_NAME = "managed_files.json";

    private Dictionary<string, ManagedFileRecord> _content;

    /// <summary>
    /// Initializes the handler with the specified index directory and an empty content dictionary.
    /// </summary>
    /// <param name="indexDirectory">The directory where index files are stored.</param>
    public ManagedFileListHandler(DirectoryInfo indexDirectory) : base(indexDirectory)
    {
        _content = [];
    }

    /// <summary>
    /// Gets or sets the file path-to-record mapping and persists the data to disk on set.
    /// </summary>
    public override Dictionary<string, ManagedFileRecord> Content
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
    /// Loads the managed file list from the JSON file on disk. The <see cref="Content"/> is left empty if
    /// the file is unable to be read.
    /// </summary>
    public override void OnLoad()
    {
        try
        {
            _content = JsonSerializer.Deserialize<Dictionary<string, ManagedFileRecord>>(
                File.ReadAllText(Path.Combine(IndexDirectory.FullName, FILE_NAME))) ?? [];
        }
        catch
        {
            _content = [];
        }
    }

    /// <summary>
    /// Rebuilds the managed file list by scanning all registry records in the specified directory.
    /// Only writes to disk if the scanned content differs from the current content.
    /// </summary>
    /// <param name="registryDirectory">The directory containing registry entries to scan.</param>
    /// <returns><c>true</c> if the content was altered during the rebuild; otherwise, <c>false</c>.</returns>
    public override bool OnRebuild(DirectoryInfo registryDirectory)
    {
        bool altered = false;

        var mngFiles = new Dictionary<string, ManagedFileRecord>();
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

                        foreach (var kvp in registry.FileList)
                        {
                            mngFiles[kvp.Key] = new ManagedFileRecord(registry.Meta.Name, kvp.Value);
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

        var serializedCurrent = JsonSerializer.Serialize(_content);
        var serializedScanned = JsonSerializer.Serialize(mngFiles);
        if (serializedCurrent != serializedScanned)
        {
            Content = mngFiles;
            altered = true;
        }

        return altered;
    }

    /// <summary>
    /// Adds or updates managed file entries when a registry entry is written.
    /// Stale entries that are no longer in the registry's file list are removed.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    public override void OnWriteRegistry(PackageRegistry registry)
    {
        var newContent = Content;

        // Remove stale managed file entries that belong to this package
        // but are no longer in the registry's file list
        var staleEntries = newContent
            .Where(kvp => kvp.Value.Owner == registry.Meta.Name && !registry.FileList.ContainsKey(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var staleFile in staleEntries)
        {
            newContent.Remove(staleFile);
        }

        // Add or update managed file entries from the registry's file list
        foreach (var p in registry.FileList)
        {
            newContent[p.Key] = new ManagedFileRecord(registry.Meta.Name, p.Value);
        }

        Content = newContent;
    }

    /// <summary>
    /// Removes all managed file entries owned by the specified package when a registry is removed.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    public override void OnRemoveRegistry(PackageRegistry registry)
    {
        var newContent = Content;
        var toRemove = newContent
            .Where(kvp => kvp.Value.Owner == registry.Meta.Name)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var file in toRemove)
        {
            newContent.Remove(file);
        }
        Content = newContent;
    }
}
