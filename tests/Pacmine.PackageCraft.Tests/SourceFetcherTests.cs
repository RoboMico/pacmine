using Xunit;
using Downloader;
using Pacmine.TestUtils;

namespace Pacmine.PackageCraft.Tests;

public class SourceFetcherTests : IDisposable
{
    private readonly TempDirectory _tempDir;

    public SourceFetcherTests()
    {
        _tempDir = new TempDirectory();
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    // ── LocalFileSourceFetcher ───────────────────────────────────────────

    [Fact]
    public async Task LocalFileFetcher_CopiesFileToSourceDir()
    {
        var workingDir = new DirectoryInfo(Path.Combine(_tempDir.Path, "work"));
        workingDir.Create();
        var srcDir = new DirectoryInfo(Path.Combine(_tempDir.Path, "src"));
        srcDir.Create();

        // Create a source file in the working directory
        var sourceFile = Path.Combine(workingDir.FullName, "input.txt");
        await File.WriteAllTextAsync(sourceFile, "hello world");

        var fetcher = new LocalFileSourceFetcher(srcDir, workingDir);
        var result = await fetcher.FetchAsync("input.txt");

        Assert.IsType<FileInfo>(result);
        Assert.True(File.Exists(result.FullName));
        Assert.Equal("hello world", await File.ReadAllTextAsync(result.FullName));
    }

    [Fact]
    public async Task LocalFileFetcher_OverwritesExistingFile()
    {
        var workingDir = new DirectoryInfo(Path.Combine(_tempDir.Path, "work"));
        workingDir.Create();
        var srcDir = new DirectoryInfo(Path.Combine(_tempDir.Path, "src"));
        srcDir.Create();

        var sourceFile = Path.Combine(workingDir.FullName, "input.txt");
        await File.WriteAllTextAsync(sourceFile, "version 2");

        // Pre-create a file in the source dir
        var existingDest = Path.Combine(srcDir.FullName, "input.txt");
        await File.WriteAllTextAsync(existingDest, "version 1");

        var fetcher = new LocalFileSourceFetcher(srcDir, workingDir);
        await fetcher.FetchAsync("input.txt");

        Assert.Equal("version 2", await File.ReadAllTextAsync(existingDest));
    }

    [Fact]
    public async Task LocalFileFetcher_FetchesFromWorkingDirRoot()
    {
        var workingDir = new DirectoryInfo(Path.Combine(_tempDir.Path, "work"));
        workingDir.Create();
        var srcDir = new DirectoryInfo(Path.Combine(_tempDir.Path, "src"));
        srcDir.Create();

        var sourceFile = Path.Combine(workingDir.FullName, "root.txt");
        await File.WriteAllTextAsync(sourceFile, "root content");

        var fetcher = new LocalFileSourceFetcher(srcDir, workingDir);
        var result = await fetcher.FetchAsync("root.txt");

        Assert.IsType<FileInfo>(result);
        Assert.Equal("root content", await File.ReadAllTextAsync(result.FullName));
    }

    // ── RemoteSourceFetcher ──────────────────────────────────────────────

    [Fact]
    public void RemoteFetcher_Construction_ConfiguresCorrectly()
    {
        var srcDir = new DirectoryInfo(Path.Combine(_tempDir.Path, "src"));
        srcDir.Create();
        var config = new DownloadConfiguration { ChunkCount = 4 };

        var fetcher = new RemoteSourceFetcher(srcDir, config);

        Assert.Same(srcDir, fetcher.SourceDirectory);
        Assert.Same(config, fetcher.DownloadConfig);
    }

    // ── GitSourceFetcher ─────────────────────────────────────────────────

    [Fact]
    public void GitFetcher_Construction_ConfiguresCorrectly()
    {
        var srcDir = new DirectoryInfo(Path.Combine(_tempDir.Path, "src"));
        srcDir.Create();

        var fetcher = new GitSourceFetcher(srcDir, "/usr/bin/git");

        Assert.Same(srcDir, fetcher.SourceDirectory);
        Assert.Equal("/usr/bin/git", fetcher.GitCommand);
    }
}
