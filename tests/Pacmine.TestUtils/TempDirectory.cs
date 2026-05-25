namespace Pacmine.TestUtils;

/// <summary>
/// Creates a temporary directory and cleans it up on disposal.
/// Used across all test projects for file-system-based tests.
/// </summary>
public class TempDirectory : IDisposable
{
    public string Path { get; }
    public DirectoryInfo DirInfo => new(Path);

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PacmineTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch { /* ignore cleanup failures */ }
    }
}
