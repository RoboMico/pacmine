using System.Diagnostics;
using Downloader;

namespace Pacmine.PackageCraft;

public abstract class SourceFetcher
{
    public SourceFetcher(DirectoryInfo sourceDirectory)
    {
        SourceDirectory = sourceDirectory;
    }

    public DirectoryInfo SourceDirectory { get; }

    public abstract Task<FileSystemInfo> FetchAsync(string url);
}

public class LocalFileSourceFetcher : SourceFetcher
{
    public LocalFileSourceFetcher(DirectoryInfo sourceDirectory, DirectoryInfo workingDirectory)
    : base(sourceDirectory)
    {
        WorkingDirectory = workingDirectory;
    }

    public DirectoryInfo WorkingDirectory { get; }

    public override async Task<FileSystemInfo> FetchAsync(string url)
    {
        string srcFile = Path.Combine(WorkingDirectory.FullName, url);
        string destFile = Path.Combine(SourceDirectory.FullName, url);
        await Task.Run(() => File.Copy(srcFile, destFile, overwrite: true));
        return new FileInfo(destFile);
    }
}

public class RemoteSourceFetcher : SourceFetcher
{
    public RemoteSourceFetcher(DirectoryInfo sourceDirectory, DownloadConfiguration downloadConfig)
    : base(sourceDirectory)
    {
        DownloadConfig = downloadConfig;
    }

    public DownloadConfiguration DownloadConfig { get; }

    public override async Task<FileSystemInfo> FetchAsync(string url)
    {
        DownloadService dlService = new(DownloadConfig);
        string fileName = "";
        dlService.DownloadStarted += (s, e) =>
        {
            fileName = e.FileName;
        };
        await dlService.DownloadFileTaskAsync(url, SourceDirectory.FullName);
        if (string.IsNullOrEmpty(fileName))
            throw new InvalidOperationException($"Download completed but no file name was reported for '{url}'.");
        return new FileInfo(Path.Combine(SourceDirectory.FullName, fileName));
    }
}

public class GitSourceFetcher : SourceFetcher
{
    public GitSourceFetcher(DirectoryInfo sourceDirectory, string gitCommand)
    : base(sourceDirectory)
    {
        GitCommand = gitCommand;
    }

    public string GitCommand { get; }

    public override async Task<FileSystemInfo> FetchAsync(string url)
    {
        string? branch = null;
        string? refSpec = null;
        int hashIndex = url.IndexOf('#');
        int dollarIndex = url.IndexOf('$');

        if (hashIndex >= 0 && dollarIndex >= 0)
        {
            throw new Exception("URL contains both a branch and a ref spec");
        }

        if (hashIndex >= 0)
        {
            branch = url[(hashIndex + 1)..];
            url = url[..hashIndex];
        }
        if (dollarIndex >= 0)
        {
            refSpec = url[(dollarIndex + 1)..];
            url = url[..dollarIndex];
        }

        // Extract repo name from URL
        string repoName = Path.GetFileNameWithoutExtension(url);
        if (string.IsNullOrEmpty(repoName))
        {
            // Fallback: use last path segment
            repoName = url.TrimEnd('/').Split('/')[^1];
        }

        string clonePath = Path.Combine(SourceDirectory.FullName, repoName);

        // Build arguments using ArgumentList for proper escaping
        var psi = new ProcessStartInfo
        {
            FileName = GitCommand,
            WorkingDirectory = SourceDirectory.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        psi.ArgumentList.Add("clone");
        psi.ArgumentList.Add(url);
        psi.ArgumentList.Add(clonePath);
        psi.ArgumentList.Add("--depth");
        psi.ArgumentList.Add("1");

        if (branch != null)
        {
            psi.ArgumentList.Add("--branch");
            psi.ArgumentList.Add(branch);
        }

        if (refSpec != null)
        {
            psi.ArgumentList.Add("--revision");
            psi.ArgumentList.Add(refSpec);
        }

        using var process = new Process { StartInfo = psi };
        process.Start();
        string errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Git clone failed (exit code {process.ExitCode}): {errorOutput}");
        }
        return new DirectoryInfo(clonePath);
    }
}