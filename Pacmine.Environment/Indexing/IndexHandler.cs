namespace Pacmine.Environment.Indexing;

/// <summary>
/// Abstract base class for index handlers that manage the lifecycle of index data
/// in a <see cref="PacmineEnvironment"/>.
/// </summary>
/// <typeparam name="T">The type of content managed by this handler.</typeparam>
public abstract class IndexHandler<T> where T : new()
{
    /// <summary>
    /// Gets the directory where index files are stored.
    /// </summary>
    protected DirectoryInfo IndexDirectory { get; private set; }

    /// <summary>
    /// Initializes the handler with the specified index directory.
    /// </summary>
    /// <param name="indexDirectory">The directory where index files are stored.</param>
    public IndexHandler(DirectoryInfo indexDirectory)
    {
        IndexDirectory = indexDirectory;
    }

    /// <summary>
    /// Gets or sets the typed content managed by this handler.
    /// </summary>
    public abstract T Content { get; set; }

    /// <summary>
    /// Called to load or deserialize index data from disk.
    /// </summary>
    public abstract void OnLoad();

    /// <summary>
    /// Called to rebuild the index by scanning the specified registry directory.
    /// </summary>
    /// <param name="registryDirectory">The directory containing registry entries to scan.</param>
    /// <returns><c>true</c> if the content was altered during the rebuild; otherwise, <c>false</c>.</returns>
    public abstract bool OnRebuild(DirectoryInfo registryDirectory);

    /// <summary>
    /// Called when a registry entry is written, to update the index accordingly.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    public abstract void OnWriteRegistry(PackageRegistry registry);

    /// <summary>
    /// Called when a registry entry is removed, to update the index accordingly.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    public abstract void OnRemoveRegistry(PackageRegistry registry);
}
