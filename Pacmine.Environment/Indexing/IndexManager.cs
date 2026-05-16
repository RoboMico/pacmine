using Pacmine.Environment;

namespace Pacmine.Environment.Indexing;

/// <summary>
/// Manages a collection of <see cref="IndexHandler"/> instances and broadcasts lifecycle events
/// (load, rebuild, write registry, remove registry, initialize) to all registered handlers.
/// Bound to both an index directory and a registry directory at construction.
/// </summary>
public class IndexManager
{
    private readonly List<IndexHandler> _handlers = [];
    private readonly DirectoryInfo _registryDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexManager"/> class.
    /// </summary>
    /// <param name="registryDirectory">The directory containing registry entries to scan during rebuilds.</param>
    public IndexManager(DirectoryInfo registryDirectory)
    {
        _registryDirectory = registryDirectory;
    }

    /// <summary>
    /// Gets the list of registered handlers.
    /// </summary>
    public IReadOnlyList<IndexHandler> Handlers => _handlers.AsReadOnly();

    /// <summary>
    /// Registers a handler. Lifecycle events will be broadcast to all registered handlers
    /// in the order they were added.
    /// </summary>
    /// <param name="handler">The handler to register.</param>
    public void AddHandler(IndexHandler handler)
    {
        _handlers.Add(handler);
    }

    /// <summary>
    /// Removes the first handler that matches the specified predicate.
    /// </summary>
    /// <param name="predicate">A predicate that identifies the handler to remove.</param>
    /// <returns><c>true</c> if a handler was removed; otherwise, <c>false</c>.</returns>
    public bool RemoveHandler(Predicate<IndexHandler> predicate)
    {
        var index = _handlers.FindIndex(predicate);
        if (index < 0)
            return false;
        _handlers.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Retrieves the first registered handler of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of handler to retrieve.</typeparam>
    /// <returns>The handler, or <c>null</c> if no handler of that type is registered.</returns>
    public T? GetHandler<T>() where T : IndexHandler
    {
        return _handlers.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Calls <see cref="IndexHandler.OnLoad"/> on all registered handlers.
    /// </summary>
    public void Load()
    {
        foreach (var handler in _handlers)
        {
            handler.OnLoad();
        }
    }

    /// <summary>
    /// Calls <see cref="IndexHandler.OnRebuild(DirectoryInfo)"/> on all registered handlers
    /// using the registry directory bound at construction.
    /// </summary>
    /// <returns><c>true</c> if any handler reported that its content was altered; otherwise, <c>false</c>.</returns>
    public bool Rebuild()
    {
        bool altered = false;
        foreach (var handler in _handlers)
        {
            altered |= handler.OnRebuild(_registryDirectory);
        }
        return altered;
    }

    /// <summary>
    /// Calls <see cref="IndexHandler.OnWriteRegistry(PackageRegistry)"/> on all registered handlers.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    public void OnWriteRegistry(PackageRegistry registry)
    {
        foreach (var handler in _handlers)
        {
            handler.OnWriteRegistry(registry);
        }
    }

    /// <summary>
    /// Calls <see cref="IndexHandler.OnRemoveRegistry(PackageRegistry)"/> on all registered handlers.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    public void OnRemoveRegistry(PackageRegistry registry)
    {
        foreach (var handler in _handlers)
        {
            handler.OnRemoveRegistry(registry);
        }
    }

    /// <summary>
    /// Calls <see cref="IndexHandler.Initialize"/> on all registered handlers.
    /// Each handler writes its empty or default data to disk.
    /// </summary>
    public void Initialize()
    {
        foreach (var handler in _handlers)
        {
            handler.Initialize();
        }
    }
}
