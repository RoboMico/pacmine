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
/// </summary>
public class PackageBuilder
{
    private LuaState luaState;
    private FileSystemInfo?[] trackedSources;
    private readonly StringBuilder _stdoutBuffer = new();
    private readonly StringBuilder _stderrBuffer = new();
    private static readonly DownloadConfiguration defaultDlConfig = new()
    {
        ChunkCount = 8,
        ParallelDownload = true
    };

    private PackageBuilder()
    {
        luaState = LuaState.Create();
        trackedSources = [];
    }

    /// <summary>
    /// Gets the working directory for the build.
    /// </summary>
    public DirectoryInfo? WorkingDirectory { get; private set; } = null;

    /// <summary>
    /// Gets the source directory where downloaded or copied sources are placed.
    /// </summary>
    public DirectoryInfo? SourceDirectory { get; private set; } = null;

    /// <summary>
    /// Gets the package staging directory where built files are assembled.
    /// </summary>
    public DirectoryInfo? PackageDirectory { get; private set; } = null;

    /// <summary>
    /// Gets the output directory where the final package archive is written.
    /// </summary>
    public DirectoryInfo? OutputDirectory { get; private set; } = null;

    /// <summary>
    /// Gets the recipe that defines the build configuration.
    /// </summary>
    public PackageCraftRecipe Recipe { get; private set; } = null!;
    // it is guaranteeed that Recipe is not null after CreateAsync() is called

    /// <summary>
    /// Gets whether the Lua file system library (<c>filesys</c>) is available. Defaults to <c>true</c>.
    /// </summary>
    public bool AllowFilesysLib { get; private set; } = true;

    /// <summary>
    /// Gets whether the Lua OS library is available. Defaults to <c>true</c>.
    /// </summary>
    public bool AllowOsLib { get; private set; } = true;

    /// <summary>
    /// Gets whether arbitrary file operations outside the source and package directories are allowed. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowArbitraryFileOperation { get; private set; } = false;

    /// <summary>
    /// Gets whether shell execution is allowed from Lua scripts. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowShellExecution { get; private set; } = false;

    /// <summary>
    /// Gets the command to execute for Git operations. <c>null</c> if Git is not available.
    /// </summary>
    public string? GitCommand { get; private set; } = null;

    /// <summary>
    /// Gets the Downloader configuration used for downloading sources.
    /// </summary>
    public DownloadConfiguration DownloadConfig { get; private set; } = defaultDlConfig;

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
    /// Creates a new <see cref="PackageBuilder"/> instance by executing the specified Lua script
    /// and parsing the resulting recipe table.
    /// </summary>
    /// <param name="script">The Lua script content containing the PackageCraft recipe.</param>
    /// <returns>A task representing the asynchronous operation, returning the configured builder.</returns>
    public static async Task<PackageBuilder> CreateAsync(string script)
    {
        PackageBuilder builder = new();
        var result = (await builder.luaState.DoStringAsync(script)).First().Read<LuaTable>();
        builder.Recipe = PackageCraftRecipeLuaObject.FromLuaTable(result);
        builder.trackedSources = new FileSystemInfo?[builder.Recipe.Sources.Count];
        return builder;
    }

    /// <summary>
    /// Configures the working directory and sets default source, package, and output directories.
    /// </summary>
    /// <param name="workingDir">The path to the working directory.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureWorkingDirectory(string workingDir)
    {
        WorkingDirectory = new DirectoryInfo(workingDir);
        SourceDirectory = new DirectoryInfo(Path.Combine(workingDir, "src"));
        PackageDirectory = new DirectoryInfo(Path.Combine(workingDir, "pkg"));
        OutputDirectory = new DirectoryInfo(workingDir);
        return this;
    }

    /// <summary>
    /// Configures the source directory.
    /// </summary>
    /// <param name="srcDir">The path to the source directory.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureSourceDirectory(string srcDir)
    {
        SourceDirectory = new DirectoryInfo(srcDir);
        return this;
    }

    /// <summary>
    /// Configures the package staging directory.
    /// </summary>
    /// <param name="pkgDir">The path to the package directory.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigurePackageDirectory(string pkgDir)
    {
        PackageDirectory = new DirectoryInfo(pkgDir);
        return this;
    }

    /// <summary>
    /// Configures the output directory for the final package archive.
    /// </summary>
    /// <param name="outDir">The path to the output directory.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureOutputDirectory(string outDir)
    {
        OutputDirectory = new DirectoryInfo(outDir);
        return this;
    }

    /// <summary>
    /// Configures whether the Lua file system library is available.
    /// </summary>
    /// <param name="allow"><c>true</c> to allow; otherwise, <c>false</c>.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureFilesysLib(bool allow)
    {
        AllowFilesysLib = allow;
        return this;
    }

    /// <summary>
    /// Configures whether the Lua OS library is available.
    /// </summary>
    /// <param name="allow"><c>true</c> to allow; otherwise, <c>false</c>.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureOsLib(bool allow)
    {
        AllowOsLib = allow;
        return this;
    }

    /// <summary>
    /// Configures whether arbitrary file operations outside the source and package directories are allowed.
    /// </summary>
    /// <param name="allow"><c>true</c> to allow; otherwise, <c>false</c>.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureArbitraryFileOperation(bool allow)
    {
        AllowArbitraryFileOperation = allow;
        return this;
    }

    /// <summary>
    /// Configures whether shell execution is allowed from Lua scripts.
    /// </summary>
    /// <param name="allow"><c>true</c> to allow; otherwise, <c>false</c>.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureShellExecution(bool allow)
    {
        AllowShellExecution = allow;
        return this;
    }

    /// <summary>
    /// Configures the command to use when invoking Git.
    /// </summary>
    /// <param name="command">The Git command to use. Set <c>null</c> to disable Git.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureGit(string? command)
    {
        GitCommand = command;
        return this;
    }

    /// <summary>
    /// Configures the Downloader service with the specified configuration.
    /// </summary>
    /// <param name="config">The <see cref="DownloadConfiguration"/> to use.</param>
    /// <returns>This <see cref="PackageBuilder"/> instance for chaining.</returns>
    public PackageBuilder ConfigureDownloadConfig(DownloadConfiguration config)
    {
        DownloadConfig = config;
        return this;
    }

    /// <summary>
    /// Initializes the build environment by creating necessary directories
    /// and injecting the enabled Lua library into the Lua state.
    /// </summary>
    /// <exception cref="Exception">Thrown when source or package directories are not configured.</exception>
    public void InitEnvironment()
    {
        if (SourceDirectory == null || PackageDirectory == null)
        {
            throw new Exception("Source or Package directory is not configured");
        }
        SourceDirectory.Create();
        PackageDirectory.Create();
        GlobalFunctions gf = new(this);
        gf.RegisterFunctions(luaState);
        luaState.Environment["filesys"] = new FilesysLuaLibrary(this);
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
            DownloadService dlService = new(defaultDlConfig);
            string fileName = "";
            dlService.DownloadStarted += (s, e) =>
            {
                fileName = e.FileName;
            };
            await dlService.DownloadFileTaskAsync(src, SourceDirectory.FullName);
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

            // Build arguments
            var argsBuilder = new List<string>
            {
                "clone",
                url,
                clonePath,
                "--depth",
                "1"
            };

            if (branch != null)
            {
                argsBuilder.Add("--branch");
                argsBuilder.Add(branch);
            }

            if (refSpec != null)
            {
                argsBuilder.Add("--revision");
                argsBuilder.Add(refSpec);
            }

            string arguments = string.Join(" ", argsBuilder.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

            var psi = new ProcessStartInfo
            {
                FileName = GitCommand,
                Arguments = arguments,
                WorkingDirectory = SourceDirectory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

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
            File.Copy(srcFile, destFile);
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
