using Downloader;
using Lua;
using Pacmine.PackageCraft.LuaLibrary;

namespace Pacmine.PackageCraft;

/// <summary>
/// Configures build permissions and creates <see cref="PackageBuilder"/> instances with
/// the appropriate Lua libraries injected based on the factory's permission settings.
/// 
/// <para>Permissions are decided at factory-configuration time, not at runtime.
/// Disabled functions are never registered into the Lua state, and the correct
/// variant of <see cref="AbstractFilesysLuaLibrary"/> is selected before the builder is created.</para>
/// </summary>
public class PackageBuilderFactory
{
    private static readonly DownloadConfiguration _defaultDlConfig = new()
    {
        ChunkCount = 8,
        ParallelDownload = true
    };

    // ── Configuration properties ─────────────────────────────────────────

    /// <summary>
    /// Gets the working directory for the build.
    /// </summary>
    public DirectoryInfo? WorkingDirectory { get; private set; }

    /// <summary>
    /// Gets the source directory where downloaded or copied sources are placed.
    /// Defaults to <c>{WorkingDirectory}/src</c> when <see cref="ConfigureWorkingDirectory"/> is used.
    /// </summary>
    public DirectoryInfo? SourceDirectory { get; private set; }

    /// <summary>
    /// Gets the package staging directory where built files are assembled.
    /// Defaults to <c>{WorkingDirectory}/pkg</c> when <see cref="ConfigureWorkingDirectory"/> is used.
    /// </summary>
    public DirectoryInfo? PackageDirectory { get; private set; }

    /// <summary>
    /// Gets the output directory where the final package archive is written.
    /// Defaults to the working directory when <see cref="ConfigureWorkingDirectory"/> is used.
    /// </summary>
    public DirectoryInfo? OutputDirectory { get; private set; }

    /// <summary>
    /// Gets whether the <c>filesys</c> Lua library is available. Defaults to <c>true</c>.
    /// </summary>
    public bool AllowFilesysLib { get; private set; } = true;

    /// <summary>
    /// Gets whether the Lua OS library is available. Defaults to <c>true</c>.
    /// Currently reserved for future use.
    /// </summary>
    public bool AllowOsLib { get; private set; } = true;

    /// <summary>
    /// Gets whether arbitrary file system operations (outside source and package directories)
    /// are permitted. When <c>true</c>, an <see cref="UnsafeFilesysLuaLibrary"/> is injected;
    /// when <c>false</c> (default), a <see cref="RestrictedFilesysLuaLibrary"/> is used.
    /// Has no effect if <see cref="AllowFilesysLib"/> is <c>false</c>.
    /// </summary>
    public bool AllowArbitraryFileOperation { get; private set; } = false;

    /// <summary>
    /// Gets whether shell execution (<c>shell()</c> Lua function) is allowed. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowShellExecution { get; private set; } = false;

    /// <summary>
    /// Gets the command to execute for Git operations. <c>null</c> means Git is not available.
    /// </summary>
    public string? GitCommand { get; private set; } = null;

    /// <summary>
    /// Gets the Downloader configuration used for downloading sources.
    /// </summary>
    public DownloadConfiguration DownloadConfig { get; private set; } = _defaultDlConfig;

    // ── Fluent configuration API ─────────────────────────────────────────

    /// <summary>
    /// Configures the working directory and sets default source, package, and output directories.
    /// </summary>
    /// <param name="workingDir">The path to the working directory.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureWorkingDirectory(string workingDir)
    {
        WorkingDirectory = new DirectoryInfo(workingDir);
        SourceDirectory ??= new DirectoryInfo(Path.Combine(workingDir, "src"));
        PackageDirectory ??= new DirectoryInfo(Path.Combine(workingDir, "pkg"));
        OutputDirectory ??= new DirectoryInfo(workingDir);
        return this;
    }

    /// <summary>
    /// Configures the source directory.
    /// </summary>
    /// <param name="srcDir">The path to the source directory.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureSourceDirectory(string srcDir)
    {
        SourceDirectory = new DirectoryInfo(srcDir);
        return this;
    }

    /// <summary>
    /// Configures the package staging directory.
    /// </summary>
    /// <param name="pkgDir">The path to the package directory.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigurePackageDirectory(string pkgDir)
    {
        PackageDirectory = new DirectoryInfo(pkgDir);
        return this;
    }

    /// <summary>
    /// Configures the output directory for the final package archive.
    /// </summary>
    /// <param name="outDir">The path to the output directory.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureOutputDirectory(string outDir)
    {
        OutputDirectory = new DirectoryInfo(outDir);
        return this;
    }

    /// <summary>
    /// Configures whether the <c>filesys</c> Lua library is available.
    /// </summary>
    /// <param name="allow"><c>true</c> to allow; otherwise, <c>false</c>.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureFilesysLib(bool allow)
    {
        AllowFilesysLib = allow;
        return this;
    }

    /// <summary>
    /// Configures whether the Lua OS library is available. Currently reserved for future use.
    /// </summary>
    /// <param name="allow"><c>true</c> to allow; otherwise, <c>false</c>.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureOsLib(bool allow)
    {
        AllowOsLib = allow;
        return this;
    }

    /// <summary>
    /// Configures whether arbitrary file operations outside the source and package directories are permitted.
    /// When <c>true</c>, an <see cref="UnsafeFilesysLuaLibrary"/> is injected instead of the restricted variant.
    /// </summary>
    /// <param name="allow"><c>true</c> to allow arbitrary (unsafe) operations; otherwise, <c>false</c> (restricted).</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureArbitraryFileOperation(bool allow)
    {
        AllowArbitraryFileOperation = allow;
        return this;
    }

    /// <summary>
    /// Configures whether shell execution is allowed from Lua scripts.
    /// </summary>
    /// <param name="allow"><c>true</c> to allow; otherwise, <c>false</c>.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureShellExecution(bool allow)
    {
        AllowShellExecution = allow;
        return this;
    }

    /// <summary>
    /// Configures the command to use when invoking Git.
    /// </summary>
    /// <param name="command">The Git command to use. Set <c>null</c> to disable Git.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureGit(string? command)
    {
        GitCommand = command;
        return this;
    }

    /// <summary>
    /// Configures the Downloader service with the specified configuration.
    /// </summary>
    /// <param name="config">The <see cref="DownloadConfiguration"/> to use.</param>
    /// <returns>This factory instance for chaining.</returns>
    public PackageBuilderFactory ConfigureDownloadConfig(DownloadConfiguration config)
    {
        DownloadConfig = config;
        return this;
    }

    // ── Recipe loading ───────────────────────────────────────────────────

    /// <summary>
    /// Parses a PackageCraft Lua recipe script and produces a <see cref="PackageCraftRecipe"/>.
    /// </summary>
    /// <param name="script">The Lua script content containing the PackageCraft recipe.</param>
    /// <returns>A task representing the asynchronous operation, returning the parsed recipe.</returns>
    public async Task<PackageCraftRecipe> LoadRecipeAsync(string script)
    {
        using var tempState = LuaState.Create();
        var result = (await tempState.DoStringAsync(script)).First().Read<LuaTable>();
        return PackageCraftRecipeLuaObject.FromLuaTable(result);
    }

    // ── Builder creation ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a fully-configured <see cref="PackageBuilder"/> with Lua libraries injected
    /// according to the factory's current permission settings.
    /// </summary>
    /// <param name="recipe">The recipe to build with.</param>
    /// <returns>A configured <see cref="PackageBuilder"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when source or package directories are not configured.</exception>
    public PackageBuilder CreateBuilder(PackageCraftRecipe recipe)
    {
        if (SourceDirectory == null)
            throw new InvalidOperationException("SourceDirectory is not configured. Call ConfigureWorkingDirectory or ConfigureSourceDirectory first.");
        if (PackageDirectory == null)
            throw new InvalidOperationException("PackageDirectory is not configured. Call ConfigureWorkingDirectory or ConfigurePackageDirectory first.");

        // 1. Create a fresh Lua state
        var luaState = LuaState.Create();

        // 2. Register global functions (only enabled ones)
        var globalFunctions = new GlobalFunctions(
            // A temporary reference; the builder will be set properly after construction
            null!,
            GitCommand,
            AllowShellExecution);
        globalFunctions.RegisterFunctions(luaState);

        // 3. Register filesys library (if enabled)
        if (AllowFilesysLib)
        {
            if (AllowArbitraryFileOperation)
                luaState.Environment["filesys"] = new UnsafeFilesysLuaLibrary(SourceDirectory, PackageDirectory);
            else
                luaState.Environment["filesys"] = new RestrictedFilesysLuaLibrary(SourceDirectory, PackageDirectory);
        }

        // 4. Create the builder with the pre-configured Lua state
        var builder = new PackageBuilder(
            luaState,
            recipe,
            WorkingDirectory,
            SourceDirectory,
            PackageDirectory,
            OutputDirectory,
            GitCommand,
            DownloadConfig);

        // 5. Patch the GlobalFunctions with the actual builder reference (circular dependency)
        //    Since GlobalFunctions only accesses builderContext for stdout/stderr/wd,
        //    the reference is stable after construction.
        globalFunctions.SetBuilderContext(builder);

        return builder;
    }
}
