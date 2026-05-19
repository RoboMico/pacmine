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
public class PackageBuilderFactory : IDisposable
{
    private static readonly DownloadConfiguration _defaultDlConfig = new()
    {
        ChunkCount = 8,
        ParallelDownload = true
    };

    /// <summary>
    /// Shared Lua state that is created by <see cref="LoadRecipeAsync"/> and consumed by
    /// <see cref="CreateBuilder"/>. This ensures the <see cref="LuaFunction"/> references
    /// extracted during recipe loading are bound to the same Lua state used during the build.
    /// </summary>
    private LuaState? _sharedLuaState;

    /// <summary>
    /// Recipe produced by <see cref="LoadRecipeAsync"/>.
    /// </summary>
    public PackageCraftRecipe? Recipe { get; private set; }

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
        // Dispose any previously cached state before creating a new one (e.g. on re-use)
        _sharedLuaState?.Dispose();
        // Create the shared Lua state — this same state will be transferred to the builder
        // via CreateBuilder, so that LuaFunction references extracted from the recipe
        // remain valid throughout the build pipeline.
        _sharedLuaState = LuaState.Create();
        var result = (await _sharedLuaState.DoStringAsync(script)).First().Read<LuaTable>();
        Recipe = PackageCraftRecipeLuaObject.FromLuaTable(result);
        return Recipe;
    }

    // ── Builder creation ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a fully-configured <see cref="PackageBuilder"/> with Lua libraries injected
    /// according to the factory's current permission settings.
    /// </summary>
    /// <returns>A configured <see cref="PackageBuilder"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when source or package directories are not configured,
    /// or when no recipe has been loaded/provided.</exception>
    public PackageBuilder CreateBuilder()
    {
        if (SourceDirectory == null)
            throw new InvalidOperationException("SourceDirectory is not configured. Call ConfigureWorkingDirectory or ConfigureSourceDirectory first.");
        if (PackageDirectory == null)
            throw new InvalidOperationException("PackageDirectory is not configured. Call ConfigureWorkingDirectory or ConfigurePackageDirectory first.");
        if (Recipe == null)
            throw new InvalidOperationException("No recipe has been loaded. Call LoadRecipeAsync or ConfigureRecipe first.");

        var recipe = Recipe;
        Recipe = null; // consume the recipe

        // 1. Use the shared Lua state from LoadRecipeAsync if available, otherwise create a fresh one.
        //    The shared state ensures LuaFunction references extracted during recipe loading
        //    belong to the same state that will be used throughout the build pipeline.
        var luaState = _sharedLuaState ?? LuaState.Create();
        _sharedLuaState = null; // ownership transferred to the builder

        // 2. Register filesys library (if enabled) — this does not need a builder reference
        if (AllowFilesysLib)
        {
            if (AllowArbitraryFileOperation)
                luaState.Environment["filesys"] = new UnsafeFilesysLuaLibrary(SourceDirectory, PackageDirectory);
            else
                luaState.Environment["filesys"] = new RestrictedFilesysLuaLibrary(SourceDirectory, PackageDirectory);
        }

        // 3. Create the builder with the partially-configured Lua state
        var builder = new PackageBuilder(
            luaState,
            recipe,
            WorkingDirectory,
            SourceDirectory,
            PackageDirectory,
            OutputDirectory,
            GitCommand,
            DownloadConfig);

        // 4. Create GlobalFunctions with the actual builder reference (no more null!)
        var globalFunctions = new GlobalFunctions(
            builder,
            GitCommand,
            AllowShellExecution);

        // 5. Register global functions on the builder's Lua state
        //    The builder reference is already available, so any Lua invocation will work immediately.
        builder.RegisterGlobalFunctions(globalFunctions);

        return builder;
    }

    // ── Resource cleanup ──────────────────────────────────────────────────

    /// <summary>
    /// Releases the shared Lua state if it was not consumed by <see cref="CreateBuilder"/>.
    /// </summary>
    public void Dispose()
    {
        _sharedLuaState?.Dispose();
        _sharedLuaState = null;
    }
}
