using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
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
///
/// <para>Build output (stdout/stderr) is exposed via <see cref="ChannelReader{T}"/>
/// properties (<see cref="StdoutReader"/> and <see cref="StderrReader"/>), enabling
/// callers to consume output asynchronously using <c>await foreach</c>.
/// The channels are completed when the builder is disposed.</para>
/// </summary>
public class PackageBuilder : IDisposable
{
    private readonly LuaState luaState;
    private readonly FileSystemInfo?[] trackedSources;
    private readonly Channel<string> _stdoutChannel = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _stderrChannel = Channel.CreateUnbounded<string>();
    private static JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageBuilder"/> class.
    /// </summary>
    /// <param name="luaState">A pre-configured Lua state (libraries already registered by the factory).</param>
    /// <param name="recipe">The recipe defining the build configuration.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="sourceDirectory">The source directory.</param>
    /// <param name="packageDirectory">The package staging directory.</param>
    /// <param name="outputDirectory">The output directory.</param>
    /// <param name="gitCommand">The Git command path, or <c>null</c> if Git is disabled.</param>
    /// <param name="downloadConfig">The download configuration.</param>
    internal PackageBuilder(
        LuaState luaState,
        PackageCraftRecipe recipe,
        DirectoryInfo workingDirectory,
        DirectoryInfo sourceDirectory,
        DirectoryInfo packageDirectory,
        DirectoryInfo outputDirectory,
        string? gitCommand,
        DownloadConfiguration downloadConfig)
    {
        this.luaState = luaState;
        Recipe = recipe;
        WorkingDirectory = workingDirectory;
        SourceDirectory = sourceDirectory;
        PackageDirectory = packageDirectory;
        OutputDirectory = outputDirectory;
        GitCommand = gitCommand;
        DownloadConfig = downloadConfig;
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
    public DirectoryInfo WorkingDirectory { get; }

    /// <summary>
    /// Gets the source directory where downloaded or copied sources are placed.
    /// </summary>
    public DirectoryInfo SourceDirectory { get; }

    /// <summary>
    /// Gets the package staging directory where built files are assembled.
    /// </summary>
    public DirectoryInfo PackageDirectory { get; }

    /// <summary>
    /// Gets the output directory where the final package archive is written.
    /// </summary>
    public DirectoryInfo OutputDirectory { get; }

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
    /// Gets a <see cref="ChannelReader{T}"/> that provides asynchronous access to the
    /// standard output stream of the build process. Use <c>await foreach</c> with
    /// <see cref="ChannelReader{T}.ReadAllAsync"/> to consume output in real time.
    /// </summary>
    public ChannelReader<string> StdoutReader => _stdoutChannel.Reader;

    /// <summary>
    /// Gets a <see cref="ChannelReader{T}"/> that provides asynchronous access to the
    /// standard error stream of the build process. Use <c>await foreach</c> with
    /// <see cref="ChannelReader{T}.ReadAllAsync"/> to consume output in real time.
    /// </summary>
    public ChannelReader<string> StderrReader => _stderrChannel.Reader;

    /// <summary>
    /// Creates the source, package and output directories if they don't exist.
    /// Must be called before fetching sources.
    /// </summary>
    public void InitializeDirectories()
    {
        Directory.CreateDirectory(SourceDirectory.FullName);
        Directory.CreateDirectory(PackageDirectory.FullName);
        Directory.CreateDirectory(OutputDirectory.FullName);
    }

    /// <summary>
    /// Fetches a source by index. Supports HTTP/HTTPS downloads, local file copies, and Git repository clones.
    /// </summary>
    /// <param name="index">The index of the source in the recipe's <see cref="PackageCraftRecipe.Sources"/> list.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown when the required directories are not configured.</exception>
    public async Task FetchSourceAsync(int index)
    {
        var src = Recipe.Sources[index];
        SourceFetcher fetcher;

        if (src.StartsWith("http://") || src.StartsWith("https://"))
        {
            fetcher = new RemoteSourceFetcher(SourceDirectory, DownloadConfig);
        }
        else if (src.StartsWith("git://"))
        {
            if (GitCommand == null)
            {
                throw new Exception("Git is disabled for this builder");
            }
            fetcher = new GitSourceFetcher(SourceDirectory, GitCommand);
            src = src["git://".Length..];
        }
        else
        {
            // Local file copy
            fetcher = new LocalFileSourceFetcher(SourceDirectory, WorkingDirectory);
        }

        trackedSources[index] = await fetcher.FetchAsync(src);
    }

    /// <summary>
    /// Verifies the checksum of a fetched source file at the specified index.
    /// </summary>
    /// <param name="index">The index of the source to verify.</param>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the checksum is valid or skipped;
    /// otherwise, <c>false</c>. Checks on folders always return <c>false</c> unless they are explicitly skipped.</returns>
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
        using HashAlgorithm hashAlgo = algo switch
        {
            "sha1" => SHA1.Create(),
            "sha256" => SHA256.Create(),
            "sha512" => SHA512.Create(),
            "md5" => MD5.Create(),
            _ => throw new Exception($"Unsupported checksum algorithm: {algo}"),
        };

        if (entry is FileInfo file)
        {
            using var fs = file.OpenRead();
            var hash = hashAlgo.ComputeHash(fs);
            return Convert.ToHexString(hash).Equals(checksum, StringComparison.OrdinalIgnoreCase);
        }

        if (entry is DirectoryInfo)
        {
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
        if (Recipe.LuaFuncPrepare == null)
            return false;
        await luaState.CallAsync(Recipe.LuaFuncPrepare, []);
        return true;
    }

    /// <summary>
    /// Invokes the <c>get_version</c> Lua function from the recipe and updates the package version, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning a <see cref="Tuple{T1, T2}"/> where the first item
    /// indicates whether the Lua function was invoked (<c>true</c>) or was not defined (<c>false</c>), and the second item
    /// is the parsed <see cref="VersionIdentifier"/> if the function returned a valid version string; otherwise, <c>null</c>.</returns>
    public async Task<Tuple<bool, VersionIdentifier?>> InvokeGetVersionAsync()
    {
        if (Recipe.LuaFuncGetVersion == null)
            return new(false, null);

        var result = await luaState.CallAsync(Recipe.LuaFuncGetVersion, []);
        if (result.Length == 0 || result[0].Type != LuaValueType.String)
            return new(true, null);
        Recipe.Meta.Version = new(result[0].Read<string>());
        return new(true, Recipe.Meta.Version);
    }

    /// <summary>
    /// Invokes the build Lua function from the recipe, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the function was called; otherwise, <c>false</c>.</returns>
    public async Task<bool> InvokeBuildAsync()
    {
        if (Recipe.LuaFuncBuild == null)
            return false;
        await luaState.CallAsync(Recipe.LuaFuncBuild, []);
        return true;
    }

    /// <summary>
    /// Invokes the <c>check</c> Lua function from the recipe, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning a <see cref="Tuple{T1, T2}"/> where the first item
    /// indicates whether the Lua function was invoked (<c>true</c>) or was not defined (<c>false</c>), and the second item
    /// is the Boolean result returned by the Lua function; <c>null</c> if the function did not return a valid Boolean value.</returns>
    public async Task<Tuple<bool, bool?>> InvokeCheckAsync()
    {
        if (Recipe.LuaFuncCheck == null)
            return new(false, null);

        var result = await luaState.CallAsync(Recipe.LuaFuncCheck, []);
        if (result.Length == 0 || result[0].Type != LuaValueType.Boolean)
            return new(true, null);
        return new(true, result[0].Read<bool>());
    }

    /// <summary>
    /// Invokes the package Lua function from the recipe, if defined.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning <c>true</c> if the function was called; otherwise, <c>false</c>.</returns>
    public async Task<bool> InvokePackageAsync()
    {
        if (Recipe.LuaFuncPackage == null)
            return false;
        await luaState.CallAsync(Recipe.LuaFuncPackage, []);
        return true;
    }

    /// <summary>
    /// Compresses the package staging directory into a ZIP archive in the output directory.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning the path to the created ZIP file.</returns>
    public async Task<string> CompressPackageAsync()
    {
        await File.WriteAllTextAsync(
            Path.Combine(PackageDirectory.FullName, PackageParser.META_FILE_NAME),
            JsonSerializer.Serialize(Recipe.Meta, jsonOptions));
        string packagePath = Path.Combine(
            OutputDirectory.FullName,
            $"{Recipe.Meta.Name}-{Recipe.Meta.GetFullVersionString()}.pacminepack.zip");
        await ZipFile.CreateFromDirectoryAsync(PackageDirectory.FullName, packagePath);
        return packagePath;
    }

    /// <summary>
    /// Cleans up the source and package directories.
    /// </summary>
    public void CleanUp()
    {
        SourceDirectory.Delete(true);
        PackageDirectory.Delete(true);
    }

    /// <summary>
    /// Dispose the builder and free resources.
    /// </summary>
    public void Dispose()
    {
        _stdoutChannel.Writer.TryComplete();
        _stderrChannel.Writer.TryComplete();
        luaState.Dispose();
    }

    internal void WriteStdout(string message)
    {
        _stdoutChannel.Writer.TryWrite(message);
    }

    internal void WriteStderr(string message)
    {
        _stderrChannel.Writer.TryWrite(message);
    }
}
