namespace Pacmine.Environment;

public class PacmineEnvironment : IDisposable
{
    public const string DATABASE_DIR_NAME = ".pacmine";
    public const string LOCKFILE_NAME = "lock";
    public const string PACKLIST_FILE_NAME = "packlist";

    private PacmineEnvironment(string path)
    {
        Path = path;
        DatabaseDirectory = new(System.IO.Path.Combine(path, DATABASE_DIR_NAME));
        LockFile = new(System.IO.Path.Combine(DatabaseDirectory.FullName, LOCKFILE_NAME));
        PackListFile = new(System.IO.Path.Combine(DatabaseDirectory.FullName, PACKLIST_FILE_NAME));
    }

    public string Path { get; private set; }

    public DirectoryInfo DatabaseDirectory { get; private set; }

    public FileInfo LockFile { get; private set; }

    public FileInfo PackListFile { get; private set; }

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

    public static int GetLockerPid(string directory)
    {
        FileInfo lockFile = new(System.IO.Path.Combine(directory, DATABASE_DIR_NAME, LOCKFILE_NAME));
        if (lockFile.Exists)
        {
            return int.Parse(File.ReadAllText(lockFile.FullName));
        }
        else
        {
            return -1;
        }
    }

    public static PacmineEnvironment Access(string directory)
    {
        if (!Directory.Exists(System.IO.Path.Combine(directory, DATABASE_DIR_NAME)))
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
        return env;
    }

    public static PacmineEnvironment Create(string directory)
    {
        string databasePath = System.IO.Path.Combine(directory, DATABASE_DIR_NAME);
        if (Directory.Exists(databasePath))
        {
            throw new Exception("Environment already created");
        }
        Directory.CreateDirectory(databasePath);
        File.Create(System.IO.Path.Combine(databasePath, PACKLIST_FILE_NAME));
        return Access(directory);
    }

    public string[] PackageList
    {
        get => File.ReadAllLines(PackListFile.FullName);
        set => File.WriteAllLines(PackListFile.FullName, value);
    }

    public void Destroy()
    {
        Directory.Delete(Path);
    }

    public void Dispose()
    {
        Unlock();
    }
}