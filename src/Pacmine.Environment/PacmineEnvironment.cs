namespace Pacmine.Environment;

/// <summary>
/// Manages a Pacmine environment, which represents a game instance directory
/// with package registry, lock file, and meta info management.
/// This is a thin coordinator composing <see cref="EnvironmentLock"/>, <see cref="RegistryStore"/>,
/// and file management operations.
/// </summary>
public class PacmineEnvironment : IDisposable
{
    /// <summary>
    /// The name of the special folder used to store environment data.
    /// </summary>
    public const string SPECIAL_FOLDER_NAME = ".pacmine";

    /// <summary>
    /// The name of the registry folder used to store package metadata.
    /// </summary>
    public const string REGISTRY_FOLDER_NAME = "registry";

    /// <summary>
    /// The name of the lock file used for concurrency control.
    /// </summary>
    public const string LOCKFILE_NAME = "lock";

    /// <summary>
    /// The name of the package list file used as a cache storing names of all installed packages.
    /// Each line in this plain-text file contains one package name.
    /// </summary>
    public const string PACKAGE_LIST_FILE_NAME = "package_list";

    private PacmineEnvironment(string path, EnvironmentLock envLock, RegistryStore registry)
    {
        RootPath = path;
        SpecialFolder = new(Path.Combine(path, SPECIAL_FOLDER_NAME));
        RegistryFolder = new(Path.Combine(SpecialFolder.FullName, REGISTRY_FOLDER_NAME));
        LockFile = new(Path.Combine(SpecialFolder.FullName, LOCKFILE_NAME));
        PackageListFile = new(Path.Combine(SpecialFolder.FullName, PACKAGE_LIST_FILE_NAME));
        Lock = envLock;
        Registry = registry;
    }

    /// <summary>
    /// Gets the root path of the environment.
    /// </summary>
    public string RootPath { get; private set; }

    /// <summary>
    /// Gets the special folder directory for this environment.
    /// </summary>
    public DirectoryInfo SpecialFolder { get; private set; }

    /// <summary>
    /// Gets the registry folder directory for this environment.
    /// </summary>
    public DirectoryInfo RegistryFolder { get; private set; }

    /// <summary>
    /// Gets the lock file information for this environment.
    /// </summary>
    public FileInfo LockFile { get; private set; }

    /// <summary>
    /// Gets the package list file information for this environment.
    /// </summary>
    public FileInfo PackageListFile { get; private set; }

    /// <summary>
    /// Gets the environment lock for concurrency control.
    /// </summary>
    public EnvironmentLock Lock { get; }

    /// <summary>
    /// Gets the package registry store for reading and writing installed package metadata.
    /// </summary>
    public RegistryStore Registry { get; }

    /// <summary>
    /// Gets the process ID of the process that currently holds the lock for the specified directory.
    /// </summary>
    /// <param name="directory">The environment root directory.</param>
    /// <returns>The process ID of the locker, or -1 if the lock file does not exist or cannot be read.</returns>
    public static int GetLockerPid(string directory)
    {
        return EnvironmentLock.GetLockerPid(directory);
    }

    /// <summary>
    /// Accesses an existing Pacmine environment and acquires the lock.
    /// </summary>
    /// <param name="directory">The environment root directory.</param>
    /// <returns>The initialized <see cref="PacmineEnvironment"/> instance.</returns>
    /// <exception cref="Exception">Thrown when the environment directory does not exist or is locked by another process.</exception>
    public static PacmineEnvironment Access(string directory)
    {
        if (!Directory.Exists(Path.Combine(directory, SPECIAL_FOLDER_NAME)))
            throw new Exception("Invalid environment directory");

        var envLock = new EnvironmentLock(directory);
        try
        {
            var registryFolder = new DirectoryInfo(
                Path.Combine(directory, SPECIAL_FOLDER_NAME, REGISTRY_FOLDER_NAME));
            var packageListFile = new FileInfo(
                Path.Combine(directory, SPECIAL_FOLDER_NAME, PACKAGE_LIST_FILE_NAME));
            var registry = new RegistryStore(registryFolder, packageListFile);
            return new PacmineEnvironment(directory, envLock, registry);
        }
        catch
        {
            envLock.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a new Pacmine environment in the specified directory and acquires the lock.
    /// </summary>
    /// <param name="directory">The directory to initialize as the environment root.</param>
    /// <returns>The newly created <see cref="PacmineEnvironment"/> instance.</returns>
    /// <exception cref="Exception">Thrown when an environment already exists in the directory.</exception>
    public static PacmineEnvironment Create(string directory)
    {
        string spFolderPath = Path.Combine(directory, SPECIAL_FOLDER_NAME);
        if (Directory.Exists(spFolderPath))
            throw new Exception("Environment already exists");

        Directory.CreateDirectory(spFolderPath);
        var envLock = new EnvironmentLock(directory);
        try
        {
            var registryFolder = Directory.CreateDirectory(Path.Combine(spFolderPath, REGISTRY_FOLDER_NAME));
            var packageListFile = new FileInfo(Path.Combine(spFolderPath, PACKAGE_LIST_FILE_NAME));
            packageListFile.Create().Dispose();
            var registry = new RegistryStore(new DirectoryInfo(registryFolder.FullName), packageListFile);
            return new PacmineEnvironment(directory, envLock, registry);
        }
        catch
        {
            envLock.Dispose();
            Directory.Delete(spFolderPath, true);
            throw;
        }
    }

    /// <summary>
    /// Deletes the special folder and all its contents from the environment.
    /// </summary>
    public void Destroy()
    {
        Lock.Dispose();
        SpecialFolder.Delete(true);
    }

    /// <summary>
    /// Releases the lock held by this environment instance.
    /// </summary>
    public void Dispose()
    {
        Lock.Dispose();
    }
}
