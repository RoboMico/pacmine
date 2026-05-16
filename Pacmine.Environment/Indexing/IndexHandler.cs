namespace Pacmine.Environment.Indexing;

/// <summary>
/// Non-generic base class for index handlers that manage the lifecycle of index data
/// in a <see cref="PacmineEnvironment"/>. This class exists to allow heterogeneous collections
/// of handlers (e.g., in <see cref="IndexManager"/>).
/// </summary>
public abstract class IndexHandler
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

    /// <summary>
    /// Initializes the index by writing empty or default data to disk.
    /// Called when a new environment is being created.
    /// </summary>
    public abstract void Initialize();
}

/// <summary>
/// Abstract base class for index handlers that manage the lifecycle of typed index data
/// in a <see cref="PacmineEnvironment"/>.
/// </summary>
/// <typeparam name="T">The type of content managed by this handler. Must have a parameterless constructor.</typeparam>
public abstract class IndexHandler<T> : IndexHandler where T : new()
{
    /// <summary>
    /// Initializes the handler with the specified index directory.
    /// </summary>
    /// <param name="indexDirectory">The directory where index files are stored.</param>
    protected IndexHandler(DirectoryInfo indexDirectory) : base(indexDirectory)
    {
    }

    /// <summary>
    /// Gets or sets the typed content managed by this handler.
    /// Setting this property persists the data to disk.
    /// </summary>
    public abstract T Content { get; set; }

    /// <summary>
    /// Initializes the index by writing a new default instance of <typeparamref name="T"/>
    /// to disk. Since <typeparamref name="T"/> has a parameterless constructor and the
    /// <see cref="Content"/> setter triggers serialization, this effectively writes
    /// the empty/default representation of the content type to the index file.
    /// </summary>
    public override void Initialize()
    {
        Content = new T();
    }
}
