using System.Diagnostics;
using Downloader;

namespace Pacmine.PackageCraft;

/// <summary>
/// Abstract base class for source fetchers. Each fetcher is responsible for
/// retrieving a source artifact (file or directory) from a given URL or path
/// and placing it under the configured <see cref="SourceDirectory"/>.
/// </summary>
public abstract class SourceFetcher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceFetcher"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The directory where fetched sources will be placed.</param>
    protected SourceFetcher(DirectoryInfo sourceDirectory)
    {
        SourceDirectory = sourceDirectory;
    }

    /// <summary>
    /// Gets the directory where fetched sources are stored.
    /// </summary>
    public DirectoryInfo SourceDirectory { get; }

    /// <summary>
    /// Fetches a source from the specified URL or path.
    /// </summary>
    /// <param name="url">The source URL, file path, or repository identifier.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The result is a <see cref="FileInfo"/>
    /// for file-based sources or a <see cref="DirectoryInfo"/> for repository clones.
    /// </returns>
    public abstract Task<FileSystemInfo> FetchAsync(string url);
}

/// <summary>
/// Fetches a source by copying a local file from the working directory into the source directory.
/// The URL is treated as a relative path resolved against <see cref="WorkingDirectory"/>.
/// </summary>
public class LocalFileSourceFetcher : SourceFetcher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileSourceFetcher"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The destination directory for the copied file.</param>
    /// <param name="workingDirectory">The working directory used to resolve relative source paths.</param>
    public LocalFileSourceFetcher(DirectoryInfo sourceDirectory, DirectoryInfo workingDirectory)
    : base(sourceDirectory)
    {
        WorkingDirectory = workingDirectory;
    }

    /// <summary>
    /// Gets the working directory against which relative source paths are resolved.
    /// </summary>
    public DirectoryInfo WorkingDirectory { get; }

    /// <summary>
    /// Copies a local file from the working directory into the source directory.
    /// The <paramref name="url"/> parameter is treated as a path relative to <see cref="WorkingDirectory"/>.
    /// If the destination file already exists, it is overwritten.
    /// </summary>
    /// <param name="url">The relative path of the source file within the working directory.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning a <see cref="FileInfo"/>
    /// pointing to the copied file in <see cref="SourceFetcher.SourceDirectory"/>.
    /// </returns>
    public override async Task<FileSystemInfo> FetchAsync(string url)
    {
        string srcFile = Path.Combine(WorkingDirectory.FullName, url);
        string destFile = Path.Combine(SourceDirectory.FullName, url);
        await Task.Run(() => File.Copy(srcFile, destFile, overwrite: true));
        return new FileInfo(destFile);
    }
}

/// <summary>
/// Fetches a source by downloading a remote file over HTTP or HTTPS using the Downloader library.
/// The file is saved into <see cref="SourceFetcher.SourceDirectory"/> with the file name reported by the server.
/// </summary>
public class RemoteSourceFetcher : SourceFetcher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteSourceFetcher"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The destination directory for the downloaded file.</param>
    /// <param name="downloadConfig">The Downloader configuration controlling chunk count, parallelism, etc.</param>
    public RemoteSourceFetcher(DirectoryInfo sourceDirectory, DownloadConfiguration downloadConfig)
    : base(sourceDirectory)
    {
        DownloadConfig = downloadConfig;
    }

    /// <summary>
    /// Gets the Downloader configuration used for HTTP/HTTPS downloads.
    /// </summary>
    public DownloadConfiguration DownloadConfig { get; }

    /// <summary>
    /// Downloads a remote file from the specified HTTP/HTTPS URL into <see cref="SourceFetcher.SourceDirectory"/>.
    /// The file name is determined from the server-provided response headers captured during the
    /// <see cref="AbstractDownloadService.DownloadStarted"/> event.
    /// </summary>
    /// <param name="url">The HTTP or HTTPS URL of the file to download.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning a <see cref="FileInfo"/>
    /// pointing to the downloaded file in <see cref="SourceFetcher.SourceDirectory"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the download completes but no file name was reported by the server.</exception>
    public override async Task<FileSystemInfo> FetchAsync(string url)
    {
        using DownloadService dlService = new(DownloadConfig);
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

/// <summary>
/// Fetches a source by cloning a Git repository into <see cref="SourceFetcher.SourceDirectory"/>.
/// Supports optional branch selection via the <c>#</c> fragment and optional revision specification
/// via the <c>$</c> fragment in the URL. Clones with <c>--depth 1</c> for efficiency.
/// </summary>
public class GitSourceFetcher : SourceFetcher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitSourceFetcher"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The parent directory under which the cloned repository folder will be created.</param>
    /// <param name="gitCommand">The path or command name of the Git executable.</param>
    public GitSourceFetcher(DirectoryInfo sourceDirectory, string gitCommand)
    : base(sourceDirectory)
    {
        GitCommand = gitCommand;
    }

    /// <summary>
    /// Gets the command used to invoke Git.
    /// </summary>
    public string GitCommand { get; }

    /// <summary>
    /// Clones a Git repository into a subdirectory of <see cref="SourceFetcher.SourceDirectory"/>.
    /// </summary>
    /// <param name="url">The repository URL. Append <c>#branch</c> to clone a specific branch,
    /// or <c>$revision</c> to specify a revision. Using both <c>#</c> and <c>$</c> in the same URL
    /// is not allowed.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning a <see cref="DirectoryInfo"/>
    /// pointing to the cloned repository folder.
    /// </returns>
    /// <exception cref="Exception">
    /// Thrown when the URL contains both a branch (<c>#</c>) and a revision specifier (<c>$</c>),
    /// or when the Git clone process exits with a non-zero exit code.
    /// </exception>
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