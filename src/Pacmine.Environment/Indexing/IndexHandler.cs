using System.Text.Json;

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
    /// Called to rebuild the index from the provided set of registry records.
    /// </summary>
    /// <param name="registries">The array of all registry entries to process.</param>
    /// <returns><c>true</c> if the content was altered during the rebuild; otherwise, <c>false</c>.</returns>
    public abstract bool OnRebuild(PackageRegistry[] registries);

    /// <summary>
    /// Called when a registry entry is written, to update the index accordingly.
    /// Returns <c>true</c> if the index was successfully updated and persisted to disk;
    /// <c>false</c> if the operation failed (e.g., disk I/O error).
    /// When <c>false</c> is returned, the in-memory state is unchanged and the on-disk
    /// index file remains in its previous consistent state.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    /// <returns><c>true</c> if the index was successfully persisted; otherwise, <c>false</c>.</returns>
    public abstract bool OnWriteRegistry(PackageRegistry registry);

    /// <summary>
    /// Called when a registry entry is removed, to update the index accordingly.
    /// Returns <c>true</c> if the index was successfully updated and persisted to disk;
    /// <c>false</c> if the operation failed (e.g., disk I/O error).
    /// When <c>false</c> is returned, the in-memory state is unchanged and the on-disk
    /// index file remains in its previous consistent state.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    /// <returns><c>true</c> if the index was successfully persisted; otherwise, <c>false</c>.</returns>
    public abstract bool OnRemoveRegistry(PackageRegistry registry);

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
    /// Setting this property updates only the in-memory state.
    /// Persistence to disk is handled separately via <see cref="Persist"/>.
    /// </summary>
    public abstract T Content { get; set; }

    /// <summary>
    /// Initializes the index by writing a new default instance of <typeparamref name="T"/>
    /// to disk. Since <typeparamref name="T"/> has a parameterless constructor and
    /// <see cref="Persist"/> is called afterward, this effectively writes
    /// the empty/default representation of the content type to the index file.
    /// </summary>
    public override void Initialize()
    {
        Content = new T();
        Persist(Content);
    }

    /// <summary>
    /// Gets the filename used for the index file on disk.
    /// Each concrete handler must provide its own constant.
    /// </summary>
    protected abstract string FileName { get; }

    /// <summary>
    /// Persists the <paramref name="content"/> to disk using an atomic
    /// write pattern (write to a temporary file, then rename). Returns <c>true</c> if
    /// the write succeeded; <c>false</c> if an I/O error occurred.
    /// </summary>
    /// /// <param name="content">The content to write to disk.</param>
    /// <returns><c>true</c> if the content was successfully written to disk; otherwise, <c>false</c>.</returns>
    protected bool Persist(object content)
    {
        try
        {
            var path = Path.Combine(IndexDirectory.FullName, FileName);
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(content));
            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
