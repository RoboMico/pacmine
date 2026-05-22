using System.Text.Json;

namespace Pacmine.Environment.Indexing;

/// <summary>
/// Manages a collection of <see cref="IndexHandler"/> instances and broadcasts lifecycle events
/// (load, rebuild, write registry, remove registry, initialize) to all registered handlers.
/// Bound to a registry directory at construction.
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
    /// Scans the registry directory, loads all registry entries,
    /// then passes the pre-loaded array to every registered handler's
    /// <see cref="IndexHandler.OnRebuild(PackageRegistry[])"/> method.
    /// </summary>
    /// <returns><c>true</c> if all handlers successfully rebuilt; otherwise, <c>false</c>.</returns>
    public bool TryRebuild()
    {
        // Scan registry directory once — all handlers share this single pass
        var registries = new List<PackageRegistry>();
        try
        {
            foreach (var subDir in _registryDirectory.EnumerateDirectories())
            {
                foreach (var file in subDir.EnumerateFiles("*.json"))
                {
                    try
                    {
                        var registry = JsonSerializer.Deserialize<PackageRegistry>(
                            File.ReadAllText(file.FullName));
                        if (registry != null)
                            registries.Add(registry);
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

        var registryArray = registries.ToArray();

        bool allSucceeded = true;
        foreach (var handler in _handlers)
        {
            if (!handler.OnRebuild(registryArray))
                allSucceeded = false;
        }
        return allSucceeded;
    }

    /// <summary>
    /// Calls <see cref="IndexHandler.OnWriteRegistry(PackageRegistry)"/> on all registered handlers.
    /// Returns <c>true</c> only if <b>all</b> handlers successfully persisted their index data.
    /// If any handler fails, the in-memory state of that handler remains unchanged, and
    /// <c>false</c> is returned. Since handlers are independent, a failure in one handler
    /// does not roll back the others.
    /// </summary>
    /// <param name="registry">The registry entry that was written.</param>
    /// <returns><c>true</c> if all handlers successfully persisted; otherwise, <c>false</c>.</returns>
    public bool OnWriteRegistry(PackageRegistry registry)
    {
        bool allSucceeded = true;
        foreach (var handler in _handlers)
        {
            if (!handler.OnWriteRegistry(registry))
                allSucceeded = false;
        }
        return allSucceeded;
    }

    /// <summary>
    /// Calls <see cref="IndexHandler.OnRemoveRegistry(PackageRegistry)"/> on all registered handlers.
    /// Returns <c>true</c> only if <b>all</b> handlers successfully persisted their index data.
    /// If any handler fails, the in-memory state of that handler remains unchanged, and
    /// <c>false</c> is returned. Since handlers are independent, a failure in one handler
    /// does not roll back the others.
    /// </summary>
    /// <param name="registry">The registry entry that was removed.</param>
    /// <returns><c>true</c> if all handlers successfully persisted; otherwise, <c>false</c>.</returns>
    public bool OnRemoveRegistry(PackageRegistry registry)
    {
        bool allSucceeded = true;
        foreach (var handler in _handlers)
        {
            if (!handler.OnRemoveRegistry(registry))
                allSucceeded = false;
        }
        return allSucceeded;
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
