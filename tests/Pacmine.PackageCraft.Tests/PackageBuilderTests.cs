using Xunit;
using Downloader;

namespace Pacmine.PackageCraft.Tests;

/// <summary>
/// Tests for <see cref="PackageBuilderFactory"/> configuration and builder creation.
/// The factory and builder can be tested in isolation — the factory does not require a Lua runtime
/// for configuration, and <see cref="PackageBuilder"/> has an internal constructor.
/// Full integration tests (Lua recipe parsing + pipeline execution) are better suited
/// for an integration test suite.
/// </summary>
public class PackageBuilderTests
{
    // ── PackageBuilderFactory: Configuration ─────────────────────────────

    [Fact]
    public void Factory_DefaultConfiguration_HasCorrectDefaults()
    {
        var factory = new PackageBuilderFactory();

        Assert.True(factory.AllowFilesysLib);
        Assert.False(factory.AllowArbitraryFileOperation);
        Assert.False(factory.AllowShellExecution);
        Assert.Null(factory.GitCommand);
        Assert.Null(factory.WorkingDirectory);
        Assert.Null(factory.SourceDirectory);
        Assert.Null(factory.PackageDirectory);
        Assert.Null(factory.OutputDirectory);
    }

    [Fact]
    public void Factory_ConfigureWorkingDirectory_SetsDefaults()
    {
        var factory = new PackageBuilderFactory();
        factory.ConfigureWorkingDirectory("/tmp/pacmine-build");

        Assert.NotNull(factory.WorkingDirectory);
        Assert.Equal("/tmp/pacmine-build", factory.WorkingDirectory!.FullName);
        Assert.NotNull(factory.SourceDirectory);
        Assert.EndsWith("src", factory.SourceDirectory!.FullName);
        Assert.NotNull(factory.PackageDirectory);
        Assert.EndsWith("pkg", factory.PackageDirectory!.FullName);
        Assert.NotNull(factory.OutputDirectory);
        Assert.Equal("/tmp/pacmine-build", factory.OutputDirectory!.FullName);
    }

    [Fact]
    public void Factory_FluentConfiguration_ReturnsSelf()
    {
        var factory = new PackageBuilderFactory();

        var result = factory
            .ConfigureWorkingDirectory("/tmp/work")
            .ConfigureFilesysLib(false)
            .ConfigureArbitraryFileOperation(true)
            .ConfigureShellExecution(true)
            .ConfigureGit("/usr/bin/git")
            .ConfigureSourceDirectory("/custom/src")
            .ConfigurePackageDirectory("/custom/pkg")
            .ConfigureOutputDirectory("/custom/out");

        Assert.Same(factory, result);
        Assert.False(factory.AllowFilesysLib);
        Assert.True(factory.AllowArbitraryFileOperation);
        Assert.True(factory.AllowShellExecution);
        Assert.Equal("/usr/bin/git", factory.GitCommand);
    }

    [Fact]
    public void Factory_ConfigureDownloadConfig_UsesCustomConfig()
    {
        var factory = new PackageBuilderFactory();
        var customConfig = new DownloadConfiguration
        {
            ChunkCount = 4,
            ParallelDownload = false
        };

        factory.ConfigureDownloadConfig(customConfig);

        Assert.Same(customConfig, factory.DownloadConfig);
    }

    // ── PackageBuilderFactory: CreateBuilder validation ──────────────────

    [Fact]
    public void Factory_CreateBuilder_MissingWorkingDirectory_Throws()
    {
        var factory = new PackageBuilderFactory();
        factory.ConfigureSourceDirectory("/tmp/boo/src");
        factory.ConfigurePackageDirectory("/tmp/boo/pkg");
        factory.ConfigureOutputDirectory("/tmp/boo/out");
        SetFactoryRecipe(factory, new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new() { Name = "test", Version = new("1.0.0") }
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            factory.CreateBuilder());

        Assert.Contains("WorkingDirectory", ex.Message);
    }

    // ── PackageBuilder: Direct construction ──────────────────────────────

    [Fact]
    public void Builder_Properties_ReflectConstructorArgs()
    {
        var recipe = new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new() { Name = "test", Version = new("2.0.0") }
        };

        // Reflection-based construction since the constructor is internal
        var builder = CreateBuilderInstance(recipe);

        Assert.Same(recipe, builder.Recipe);
        Assert.NotNull(builder.SourceDirectory);
        Assert.NotNull(builder.PackageDirectory);
        Assert.Equal("/tmp/work/src", builder.SourceDirectory!.FullName);
        Assert.Equal("/tmp/work/pkg", builder.PackageDirectory!.FullName);
        Assert.Equal("/tmp/work", builder.WorkingDirectory!.FullName);
        Assert.Equal("/usr/bin/git", builder.GitCommand);
    }

    [Fact]
    public void Builder_InitializeDirectories_CreatesDirs()
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "PacmineTest", Guid.NewGuid().ToString());
        var srcDir = Path.Combine(tmpRoot, "src");
        var pkgDir = Path.Combine(tmpRoot, "pkg");

        var recipe = new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new() { Name = "test", Version = new("1.0.0") }
        };

        var builder = CreateBuilderInstance(recipe,
            workingDir: tmpRoot,
            sourceDir: srcDir,
            packageDir: pkgDir);

        try
        {
            builder.InitializeDirectories();

            Assert.True(Directory.Exists(srcDir));
            Assert.True(Directory.Exists(pkgDir));
        }
        finally
        {
            try { Directory.Delete(tmpRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Builder_CleanUpAsync_RemovesCreatedDirectories()
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "PacmineTest", Guid.NewGuid().ToString());
        var srcDir = Path.Combine(tmpRoot, "src");
        var pkgDir = Path.Combine(tmpRoot, "pkg");

        var recipe = new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new() { Name = "cleanup-test", Version = new("1.0.0") }
        };

        var builder = CreateBuilderInstance(recipe,
            workingDir: tmpRoot,
            sourceDir: srcDir,
            packageDir: pkgDir);

        builder.InitializeDirectories();

        Assert.True(Directory.Exists(srcDir));
        Assert.True(Directory.Exists(pkgDir));

        builder.CleanUp();

        Assert.False(Directory.Exists(srcDir));
        Assert.False(Directory.Exists(pkgDir));
    }

    [Fact]
    public void Builder_StandardOutput_ReflectsWrittenMessages()
    {
        var recipe = new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new() { Name = "stdout-test", Version = new("1.0.0") }
        };

        var builder = CreateBuilderInstance(recipe);

        InvokeWriteStdout(builder, "hello");
        InvokeWriteStdout(builder, " world");

        var sb = new System.Text.StringBuilder();
        while (builder.StdoutReader.TryRead(out var line))
            sb.Append(line);

        Assert.Equal("hello world", sb.ToString());
    }

    [Fact]
    public void Builder_StandardError_ReflectsWrittenMessages()
    {
        var recipe = new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new() { Name = "stderr-test", Version = new("1.0.0") }
        };

        var builder = CreateBuilderInstance(recipe);

        InvokeWriteStderr(builder, "error message");

        var sb = new System.Text.StringBuilder();
        while (builder.StderrReader.TryRead(out var line))
            sb.Append(line);

        Assert.Equal("error message", sb.ToString());
    }

    [Fact]
    public void Factory_AllowFilesysLibDisabled_DoesNotRegisterFilesys()
    {
        var factory = new PackageBuilderFactory();
        factory
            .ConfigureWorkingDirectory("/tmp/disable-filesys")
            .ConfigureFilesysLib(false);

        var recipe = new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new() { Name = "no-filesys", Version = new("1.0.0") }
        };

        SetFactoryRecipe(factory, recipe);
        var builder = factory.CreateBuilder();

        // The Lua state should not have "filesys" registered
        var luaStateField = typeof(PackageBuilder).GetField("luaState",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var luaState = luaStateField!.GetValue(builder) as Lua.LuaState;

        Assert.NotNull(luaState);
        Assert.Equal(Lua.LuaValueType.Nil, luaState!.Environment["filesys"].Type);
    }

    // ── CompressPackageAsync meta file test ──────────────────────────────

    [Fact]
    public async Task Builder_CompressPackageAsync_WritesValidMetaJson()
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "PacmineTest", Guid.NewGuid().ToString());
        var srcDir = Path.Combine(tmpRoot, "src");
        var pkgDir = Path.Combine(tmpRoot, "pkg");
        var outDir = Path.Combine(tmpRoot, "out");
        Directory.CreateDirectory(outDir);

        var recipe = new PackageCraftRecipe
        {
            Protocol = "1.0",
            Meta = new() { Name = "meta-test", Version = new("1.2.3") }
        };

        var builder = CreateBuilderInstance(recipe,
            workingDir: tmpRoot,
            sourceDir: srcDir,
            packageDir: pkgDir,
            outputDir: outDir);

        try
        {
            builder.InitializeDirectories();
            await builder.CompressPackageAsync();

            // Verify the meta JSON was written to the package directory before zipping
            var metaPath = Path.Combine(pkgDir, ".PACMINE.META.json");
            Assert.True(File.Exists(metaPath));

            var metaJson = File.ReadAllText(metaPath);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<Pacmine.Core.PackageMeta>(metaJson);
            Assert.NotNull(deserialized);
            Assert.Equal("meta-test", deserialized!.Name);
            Assert.Equal("1.2.3", deserialized.Version.RawString);

            // Verify zip was created
            var zipPath = Path.Combine(outDir, "meta-test-1.2.3#1.pacminepack.zip");
            Assert.True(File.Exists(zipPath));
        }
        finally
        {
            try { Directory.Delete(tmpRoot, recursive: true); } catch { }
        }
    }

    // ── Helper ───────────────────────────────────────────────────────────

    private static PackageBuilder CreateBuilderInstance(
        PackageCraftRecipe recipe,
        string? workingDir = "/tmp/work",
        string? sourceDir = "/tmp/work/src",
        string? packageDir = "/tmp/work/pkg",
        string? outputDir = "/tmp/work",
        string? gitCommand = "/usr/bin/git")
    {
        // Use LuaCSharp to create a minimal Lua state (required by the internal constructor).
        // We pass an empty Lua state; integration-level tests would use a real recipe.
        var luaState = Lua.LuaState.Create();

        var ctor = typeof(PackageBuilder).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var builder = (PackageBuilder)ctor[0].Invoke([
            luaState,
            recipe,
            workingDir != null ? new DirectoryInfo(workingDir) : null,
            sourceDir != null ? new DirectoryInfo(sourceDir) : null,
            packageDir != null ? new DirectoryInfo(packageDir) : null,
            outputDir != null ? new DirectoryInfo(outputDir) : null,
            gitCommand,
            null // DownloadConfig: use default
        ]);

        return builder;
    }

    private static void InvokeWriteStdout(PackageBuilder builder, string message)
    {
        var method = typeof(PackageBuilder).GetMethod("WriteStdout",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(builder, [message]);
    }

    private static void InvokeWriteStderr(PackageBuilder builder, string message)
    {
        var method = typeof(PackageBuilder).GetMethod("WriteStderr",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(builder, [message]);
    }

    /// <summary>
    /// Sets the internal <c>_recipe</c> field on a <see cref="PackageBuilderFactory"/>
    /// via reflection. Used by tests that need to provide a recipe without going through
    /// <see cref="PackageBuilderFactory.LoadRecipeAsync"/>.
    /// </summary>
    private static void SetFactoryRecipe(PackageBuilderFactory factory, PackageCraftRecipe recipe)
    {
        // Recipe is now an auto-property; the compiler generates a backing field with this name.
        var field = typeof(PackageBuilderFactory).GetField("<Recipe>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(factory, recipe);
    }
}
