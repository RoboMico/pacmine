using System.Text.Json;

namespace Pacmine.Environment;

/*
an environment folder structure would like this:

26.1.2-fabric
├─ <other game files>
└─ .pacmine
   ├─ lock
   ├─ packlist
   ├─ managed_files.json
   └─ registry
      ├─ a   (first layer, sort the packages by the initial letter of package name)
      ├─ b
      ├─ ...
      └─ s
         └─ sodium-mc26.1-fabric.json
*/

/// <summary>
/// Manages a Pacmine environment, which represents a game instance directory
/// with package registry, lock file, and meta info management.
/// </summary>
public class PacmineEnvironment : IDisposable
{
    private List<string> _packList = [];
    private List<ManagedFileRecord> _mngFiles = [];

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
    /// The name of the file storing the list of installed packages.
    /// </summary>
    public const string PACKLIST_FILE_NAME = "packlist";

    /// <summary>
    /// The name of the file storing the list of managed files.
    /// </summary>
    public const string MANAGED_FILE_LIST_FILE_NAME = "managed_files.json";

    private PacmineEnvironment(string path)
    {
        Path = path;
        SpecialFolder = new(System.IO.Path.Combine(path, SPECIAL_FOLDER_NAME));
        RegistryFolder = new(System.IO.Path.Combine(SpecialFolder.FullName, REGISTRY_FOLDER_NAME));
        LockFile = new(System.IO.Path.Combine(SpecialFolder.FullName, LOCKFILE_NAME));
        PackListFile = new(System.IO.Path.Combine(SpecialFolder.FullName, PACKLIST_FILE_NAME));
    }

    /// <summary>
    /// Gets the root path of the environment.
    /// </summary>
    public string Path { get; private set; }

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
    public FileInfo PackListFile { get; private set; }

    /// <summary>
    /// Gets the file information of <see cref="MANAGED_FILE_LIST_FILE_NAME"/> in this environment.
    /// </summary>
    public FileInfo ManagedFileListFile { get; private set; }

    private void Lock()
    {
        int pid = System.Environment.ProcessId;
        LockFile.Create();
        File.WriteAllText(LockFile.FullName, $"{pid}");
    }

    private void Unlock()
    {
        if (LockFile.Exists) LockFile.Delete();
    }

    /// <summary>
    /// Gets the process ID of the process that currently holds the lock for the specified directory.
    /// </summary>
    /// <param name="directory">The environment root directory.</param>
    /// <returns>The process ID of the locker, or -1 if the environment is not locked.</returns>
    public static int GetLockerPid(string directory)
    {
        FileInfo lockFile = new(System.IO.Path.Combine(directory, SPECIAL_FOLDER_NAME, LOCKFILE_NAME));
        if (lockFile.Exists)
        {
            return int.Parse(File.ReadAllText(lockFile.FullName));
        }
        else
        {
            return -1;
        }
    }

    /// <summary>
    /// Accesses an existing Pacmine environment and acquires the lock.
    /// </summary>
    /// <param name="directory">The environment root directory.</param>
    /// <returns>The initialized <see cref="PacmineEnvironment"/> instance.</returns>
    /// <exception cref="Exception">Thrown when the environment directory does not exist or is locked by another process.</exception>
    public static PacmineEnvironment Access(string directory)
    {
        if (!Directory.Exists(System.IO.Path.Combine(directory, SPECIAL_FOLDER_NAME)))
        {
            throw new Exception("Invalid environment directory");
        }
        int lockerPid = GetLockerPid(directory);
        if (lockerPid > 0)
        {
            throw new Exception($"Another process {lockerPid} has locked the directory");
        }
        PacmineEnvironment env = new(directory);
        env.Lock();
        env._packList = File.ReadAllLines(env.PackListFile.FullName).ToList();
        env._mngFiles = JsonSerializer.Deserialize<List<ManagedFileRecord>>(
            File.ReadAllText(env.ManagedFileListFile.FullName)) ?? throw new Exception("Invalid managed files list");
        // TODO: auto rebuild the cache and generate managed_files.json if it is corrupted/missing,
        // just throw the exception for now
        return env;
    }

    /// <summary>
    /// Creates a new Pacmine environment in the specified directory and acquires the lock.
    /// </summary>
    /// <param name="directory">The directory to initialize as the environment root.</param>
    /// <returns>The newly created <see cref="PacmineEnvironment"/> instance.</returns>
    /// <exception cref="Exception">Thrown when an environment already exists in the directory.</exception>
    public static PacmineEnvironment Create(string directory)
    {
        string databasePath = System.IO.Path.Combine(directory, SPECIAL_FOLDER_NAME);
        if (Directory.Exists(databasePath))
        {
            throw new Exception("Environment already created");
        }

        Directory.CreateDirectory(databasePath);
        File.Create(System.IO.Path.Combine(databasePath, PACKLIST_FILE_NAME));

        return Access(directory);
    }

    /// <summary>
    /// Gets the list of installed package names.
    /// </summary>
    public List<string> PackageList
    {
        get => _packList;
        set
        {
            _packList = value;
            File.WriteAllLines(PackListFile.FullName, _packList);
        }
    }

    /// <summary>
    /// Gets the list of managed files in this environment.
    /// </summary>
    public List<ManagedFileRecord> ManagedFiles
    {
        get => _mngFiles;
        set
        {
            _mngFiles = value;
            File.WriteAllText(JsonSerializer.Serialize(_mngFiles), ManagedFileListFile.FullName);
        }
    }

    /// <summary>
    /// Deletes the special folder and all its contents from the environment.
    /// </summary>
    public void Destroy()
    {
        SpecialFolder.Delete();
    }

    /// <summary>
    /// Releases the lock held by this environment instance.
    /// </summary>
    public void Dispose()
    {
        Unlock();
    }
}
