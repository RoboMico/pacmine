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
        Assert.True(factory.AllowOsLib);
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
    public void Factory_CreateBuilder_MissingSourceDirectory_Throws()
    {
        var factory = new PackageBuilderFactory();
        factory.ConfigurePackageDirectory("/tmp/pkg");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            factory.CreateBuilder(new PackageCraftRecipe
            {
                Protocol = "1.0",
                Meta = new() { Name = "test", Version = new("1.0.0") }
            }));

        Assert.Contains("SourceDirectory", ex.Message);
    }

    [Fact]
    public void Factory_CreateBuilder_MissingPackageDirectory_Throws()
    {
        var factory = new PackageBuilderFactory();
        factory.ConfigureSourceDirectory("/tmp/src");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            factory.CreateBuilder(new PackageCraftRecipe
            {
                Protocol = "1.0",
                Meta = new() { Name = "test", Version = new("1.0.0") }
            }));

        Assert.Contains("PackageDirectory", ex.Message);
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
}
