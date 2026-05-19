using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Downloader;
using Lua;
using Pacmine.Core;
using Pacmine.PackageCraft.LuaLibrary;

namespace Pacmine.PackageCraft;

/// <summary>
/// Orchestrates the PackageCraft build pipeline: fetching sources, verifying checksums,
/// invoking Lua build phases, and compressing the output package.
/// 
/// <para>This class contains <b>no permission flags</b>. Permissions are configured on
/// <see cref="PackageBuilderFactory"/> and the appropriate Lua libraries are injected
/// at construction time by the factory.</para>
/// </summary>
public class PackageBuilder
{
    private readonly LuaState luaState;
    private readonly FileSystemInfo?[] trackedSources;
    private readonly StringBuilder _stdoutBuffer = new();
    private readonly StringBuilder _stderrBuffer = new();
    private static readonly DownloadConfiguration defaultDlConfig = new()
    {
        ChunkCount = 8,
        ParallelDownload = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageBuilder"/> class.
    /// </summary>
    /// <param name="luaState">A pre-configured Lua state (libraries already registered by the factory).</param>
    /// <param name="recipe">The recipe defining the build configuration.</param>
    /// <param name="workingDirectory">The working directory, or <c>null</c>.</param>
    /// <param name="sourceDirectory">The source directory.</param>
    /// <param name="packageDirectory">The package staging directory.</param>
    /// <param name="outputDirectory">The output directory, or <c>null</c> (defaults to working directory).</param>
    /// <param name="gitCommand">The Git command path, or <c>null</c> if Git is disabled.</param>
    /// <param name="downloadConfig">The download configuration.</param>
    internal PackageBuilder(
        LuaState luaState,
        PackageCraftRecipe recipe,
        DirectoryInfo? workingDirectory,
        DirectoryInfo? sourceDirectory,
        DirectoryInfo? packageDirectory,
        DirectoryInfo? outputDirectory,
        string? gitCommand,
        DownloadConfiguration? downloadConfig)
    {
        this.luaState = luaState;
        Recipe = recipe;
        WorkingDirectory = workingDirectory;
        SourceDirectory = sourceDirectory;
        PackageDirectory = packageDirectory;
        OutputDirectory = outputDirectory;
        GitCommand = gitCommand;
        DownloadConfig = downloadConfig ?? defaultDlConfig;
        trackedSources = new FileSystemInfo?[recipe.Sources.Count];
    }

    /// <summary>
    /// Registers global Lua functions (print, printerr, git, shell) on the builder's internal Lua state.
    /// Must be called before any build pipeline methods are invoked.
    /// </summary>
    /// <param name="globalFunctions">The <see cref="GlobalFunctions"/> instance to register.</param>
    internal void RegisterGlobalFunctions(GlobalFunctions globalFunctions)
    {
        globalFunctions.RegisterFunctions(luaState);
    }

    /// <summary>
    /// Gets the working directory for the build.
    /// </summary>
    public DirectoryInfo? WorkingDirectory { get; }

    /// <summary>
    /// Gets the source directory where downloaded or copied sources are placed.
    /// </summary>
    public DirectoryInfo? SourceDirectory { get; }

    /// <summary>
    /// Gets the package staging directory where built files are assembled.
    /// </summary>
    public DirectoryInfo? PackageDirectory { get; }

    /// <summary>
    /// Gets the output directory where the final package archive is written.
    /// </summary>
    public DirectoryInfo? OutputDirectory { get; }

    /// <summary>
    /// Gets the recipe that defines the build configuration.
    /// </summary>
    public PackageCraftRecipe Recipe { get; }

    /// <summary>
    /// Gets the command to execute for Git operations. <c>null</c> if Git is not available.
    /// </summary>
    public string? GitCommand { get; }

    /// <summary>
    /// Gets the Downloader configuration used for downloading sources.
    /// </summary>
    public DownloadConfiguration DownloadConfig { get; }

    /// <summary>
    /// Gets the standard output stream of the build process.
    /// </summary>
    public StreamReader StandardOutput
    {
        get
        {
            var bytes = Encoding.UTF8.GetBytes(_stdoutBuffer.ToString());
            return new StreamReader(new MemoryStream(bytes));
        }
    }

    /// <summary>
    /// Gets the standard error stream of the build process.
    /// </summary>
    public StreamReader StandardError
    {
        get
        {
            var bytes = Encoding.UTF8.GetBytes(_stderrBuffer.ToString());
            return new StreamReader(new MemoryStream(bytes));
        }
    }

    /// <summary>
    /// Creates the source and package directories if they don't exist.
    /// Must be called before fetching sources.
    /// </summary>
    public void InitializeDirectories()
    {
        if (SourceDirectory == null)
            throw new InvalidOperationException("SourceDirectory is not configured.");
        if (PackageDirectory == null)
            throw new InvalidOperationException("PackageDirectory is not configured.");

        SourceDirectory.Create();
        PackageDirectory.Create();
    }

    /// <summary>
    /// Fetches a source by index. Supports HTTP/HTTPS downloads, local file copies, and Git repository clones.
    /// </summary>
    /// <param name="index">The index of the source in the recipe's <see cref="PackageCraftRecipe.Sources"/> list.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown when the required directories are not configured.</exception>
    public async Task FetchSourceAsync(int index)
    {
        if (SourceDirectory == null)
        {
            throw new Exception("Source directory is not configured");
        }
        var src = Recipe.Sources[index];
        if (src.StartsWith("http://") || src.StartsWith("https://"))
        {
            // download the file
            DownloadService dlService = new(DownloadConfig);
            string fileName = "";
            dlService.DownloadStarted += (s, e) =>
            {
                fileName = e.FileName;
            };
            await dlService.DownloadFileTaskAsync(src, SourceDirectory.FullName);
            if (string.IsNullOrEmpty(fileName))
                throw new InvalidOperationException($"Download completed but no file name was reported for '{src}'.");
            trackedSources[index] = new FileInfo(Path.Combine(SourceDirectory.FullName, fileName));
        }
        else if (src.StartsWith("git://"))
        {
            // Guards
            if (GitCommand == null)
            {
                throw new Exception("Git is disabled for this builder.");
            }

            // Parse the git:// URL
            string url = src["git://".Length..];

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

            trackedSources[index] = new DirectoryInfo(clonePath);
        }
        else
        {
            // local file, copy from working directory
            if (WorkingDirectory == null)
            {
                throw new Exception("Working directory is not configured");
            }
            string srcFile = Path.Combine(WorkingDirectory.FullName, src);
            string destFile = Path.Combine(SourceDirectory.FullName, src);
            File.Copy(srcFile, destFile, overwrite: true);
            trackedSources[index] = new FileInfo(destFile);
        }
    }

    /// <summary>
    /// Verifies the checksum of a fetched source file at the specified index.
    /// </summary>
    /// <param name="index">The index of the source to verify.</param>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the checksum is valid or skipped; otherwise, <c>false</c>.</returns>
    /// <exception cref="Exception">Thrown when the checksum algorithm is not supported.</exception>
    public async Task<bool> VerifySourceAsync(int index)
    {
        var entry = trackedSources[index];
        if (entry == null)
        {
            return false;
        }
        if (Recipe.SourceChecksums[index] == "SKIP")
        {
            return true;
        }

        var seg = Recipe.SourceChecksums[index].Split(':');
        var algo = seg[0];
        var checksum = seg[1];
        HashAlgorithm hashAlgo = algo switch
        {
            "sha1" => SHA1.Create(),
            "sha256" => SHA256.Create(),
            "sha512" => SHA512.Create(),
            "md5" => MD5.Create(),
            _ => throw new Exception($"Unsupported checksum algorithm: {algo}"),
        };

        if (entry is FileInfo file)
        {
            var hash = hashAlgo.ComputeHash(File.ReadAllBytes(file.FullName));
            return Convert.ToHexString(hash).Equals(checksum, StringComparison.CurrentCultureIgnoreCase);
        }

        if (entry is DirectoryInfo)
        {
            // Always consider the folder source invalid; checks of folders must be explicitly skipped
            return false;
        }

        return false;
    }

    /// <summary>
    /// Invokes the prepare Lua function from the recipe, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the function was called; otherwise, <c>false</c>.</returns>
    public async Task<bool> InvokePrepareAsync()
    {
        if (Recipe.Prepare == null)
            return false;
        await luaState.CallAsync(Recipe.Prepare, []);
        return true;
    }

    /// <summary>
    /// Invokes the get-version Lua function from the recipe and updates the package version, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the function was called; otherwise, <c>false</c>.</returns>
    public async Task<bool> InvokeGetVersionAsync()
    {
        if (Recipe.GetVersion == null)
            return false;
        var result = await luaState.CallAsync(Recipe.GetVersion, []);
        if (result.Length == 0)
            throw new InvalidOperationException("get_version must return a version string.");
        Recipe.Meta.Version = new(result[0].Read<string>());
        return true;
    }

    /// <summary>
    /// Invokes the build Lua function from the recipe, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the function was called; otherwise, <c>false</c>.</returns>
    public async Task<bool> InvokeBuildAsync()
    {
        if (Recipe.Build == null)
            return false;
        await luaState.CallAsync(Recipe.Build, []);
        return true;
    }

    /// <summary>
    /// Invokes the check Lua function from the recipe, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the function was called; otherwise, <c>false</c>.</returns>
    public async Task<bool> InvokeCheckAsync()
    {
        if (Recipe.Check == null)
            return false;
        await luaState.CallAsync(Recipe.Check, []);
        return true;
    }

    /// <summary>
    /// Invokes the package Lua function from the recipe, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the function was called; otherwise, <c>false</c>.</returns>
    public async Task<bool> InvokePackageAsync()
    {
        if (Recipe.Package == null)
            return false;
        await luaState.CallAsync(Recipe.Package, []);
        return true;
    }

    /// <summary>
    /// Compresses the package staging directory into a ZIP archive in the output directory.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown when package or output directories are not configured.</exception>
    public async Task CompressPackageAsync()
    {
        if (PackageDirectory == null || OutputDirectory == null)
        {
            throw new Exception("Package or Output directory is not configured");
        }
        File.WriteAllText(Path.Combine(PackageDirectory.FullName, PackageParser.META_FILE_NAME), JsonSerializer.Serialize(Recipe.Meta));
        ZipFile.CreateFromDirectory(
            PackageDirectory.FullName,
            Path.Combine(OutputDirectory.FullName, $"{Recipe.Meta.Name}-{Recipe.Meta.GetFullVersionString()}.pacminepack.zip"));
    }

    /// <summary>
    /// Cleans up the source and package directories.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CleanUpAsync()
    {
        SourceDirectory?.Delete(true);
        PackageDirectory?.Delete(true);
    }

    internal void WriteStdout(string message)
    {
        lock (_stdoutBuffer)
        {
            _stdoutBuffer.Append(message);
        }
    }

    internal void WriteStderr(string message)
    {
        lock (_stderrBuffer)
        {
            _stderrBuffer.Append(message);
        }
    }
}
