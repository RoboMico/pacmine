using System.Text;

namespace Pacmine.Environment;

/// <summary>
/// Manages PID-based file locking for Pacmine environments to prevent concurrent modifications.
/// Acquires a lock file containing the current process's PID on construction,
/// and releases it on disposal.
/// </summary>
public class EnvironmentLock : IDisposable
{
    private FileStream? _lockStream;
    private readonly FileInfo _lockFile;

    /// <summary>
    /// Acquires the lock for the specified environment root directory.
    /// Writes the current process ID to the lock file.
    /// </summary>
    /// <param name="directory">The environment root directory.</param>
    /// <exception cref="IOException">Thrown when the environment is already locked by another process.</exception>
    public EnvironmentLock(string directory)
    {
        _lockFile = new FileInfo(Path.Combine(
            directory,
            PacmineEnvironment.SPECIAL_FOLDER_NAME,
            PacmineEnvironment.LOCKFILE_NAME));

        try
        {
            _lockStream = new FileStream(
                _lockFile.FullName,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read);
            _lockStream.SetLength(0);
            var pidBytes = Encoding.UTF8.GetBytes(System.Environment.ProcessId.ToString());
            _lockStream.Write(pidBytes);
            _lockStream.Flush();
        }
        catch (IOException ex)
        {
            var pid = GetLockerPid(directory);
            var extra = pid >= 0 ? $"(locked by process {pid})" : "";
            throw new IOException($"Unable to access the environment{extra}.", ex);
        }
    }

    /// <summary>
    /// Gets the process ID of the process that currently holds the lock for the specified directory.
    /// </summary>
    /// <param name="directory">The environment root directory.</param>
    /// <returns>The process ID of the locker, or -1 if the lock file does not exist or cannot be read.</returns>
    public static int GetLockerPid(string directory)
    {
        var lockFile = new FileInfo(Path.Combine(
            directory,
            PacmineEnvironment.SPECIAL_FOLDER_NAME,
            PacmineEnvironment.LOCKFILE_NAME));
        if (!lockFile.Exists)
            return -1;

        try
        {
            using var fs = new FileStream(
                lockFile.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var text = reader.ReadToEnd();
            return int.TryParse(text.Trim(), out var pid) ? pid : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Releases the lock and deletes the lock file.
    /// </summary>
    public void Dispose()
    {
        _lockStream?.Dispose();
        _lockStream = null;
        try
        {
            if (_lockFile.Exists)
                _lockFile.Delete();
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
