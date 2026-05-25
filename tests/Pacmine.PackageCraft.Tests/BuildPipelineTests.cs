using Xunit;
using System.Security.Cryptography;
using Pacmine.Core;
using Pacmine.TestUtils;

namespace Pacmine.PackageCraft.Tests;

/// <summary>
/// Integration-style tests for the PackageCraft build pipeline.
/// Uses real Lua scripts (embedded as strings) to avoid network calls.
/// </summary>
public class BuildPipelineTests : IDisposable
{
    private readonly TempDirectory _tempDir;

    public BuildPipelineTests()
    {
        _tempDir = new TempDirectory();
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    // ── LoadRecipeAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadRecipeAsync_MinimalLuaRecipe_ParsesCorrectly()
    {
        var recipe = await LoadRecipe(@"
return {
    protocol = ""1.0"",
    meta = " + MetaLua("test-pkg", "1.0.0", @"description = ""A test Lua recipe""") + @",
    sources = {},
    source_checksums = {}
}
");

        Assert.NotNull(recipe);
        Assert.Equal("1.0", recipe.Protocol);
        Assert.Equal("test-pkg", recipe.Meta.Name);
        Assert.Equal("1.0.0", recipe.Meta.Version.RawString);
        Assert.Equal("A test Lua recipe", recipe.Meta.Description);
    }

    [Fact]
    public async Task LoadRecipeAsync_LuaWithSources_ParsesSources()
    {
        var recipe = await LoadRecipe(@"
return {
    protocol = ""1.0"",
    meta = " + MetaLua("src-pkg", "2.0.0") + @",
    sources = { ""file1.zip"", ""file2.tar.gz"" },
    source_checksums = { ""SKIP"", ""sha256:abc123"" }
}
");

        Assert.Equal(2, recipe.Sources.Count);
        Assert.Equal("file1.zip", recipe.Sources[0]);
        Assert.Equal("file2.tar.gz", recipe.Sources[1]);
        Assert.Equal(2, recipe.SourceChecksums.Count);
        Assert.Equal("SKIP", recipe.SourceChecksums[0]);
        Assert.Equal("sha256:abc123", recipe.SourceChecksums[1]);
    }

    // ── CreateBuilder ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBuilder_ValidConfig_CreatesBuilder()
    {
        var (builder, _) = await CreateBuilder("builder-test", "1.0.0");

        Assert.NotNull(builder);
        Assert.Equal("builder-test", builder.Recipe.Meta.Name);
        Assert.NotNull(builder.WorkingDirectory);
        Assert.NotNull(builder.SourceDirectory);
        Assert.NotNull(builder.PackageDirectory);
        Assert.NotNull(builder.OutputDirectory);
    }

    [Fact]
    public void CreateBuilder_MissingRecipe_ThrowsInvalidOperationException()
    {
        var factory = new PackageBuilderFactory();
        factory.ConfigureWorkingDirectory(_tempDir.Path);

        Assert.Throws<InvalidOperationException>(() => { factory.CreateBuilder(); });
    }

    // ── FetchSource (local file) ─────────────────────────────────────────

    [Fact]
    public async Task FetchSource_LocalFile_CopiesToSourceDir()
    {
        var localFile = Path.Combine(_tempDir.Path, "data.bin");
        await File.WriteAllTextAsync(localFile, "test data");

        var (builder, _) = await CreateBuilder("fetch-test", "1.0.0",
            sources: @"{ ""data.bin"" }",
            checksums: @"{ ""SKIP"" }");

        builder.InitializeDirectories();
        await builder.FetchSourceAsync(0);

        var expectedDest = Path.Combine(builder.SourceDirectory.FullName, "data.bin");
        Assert.True(File.Exists(expectedDest));
        Assert.Equal("test data", await File.ReadAllTextAsync(expectedDest));
    }

    // ── VerifySource ─────────────────────────────────────────────────────

    [Fact]
    public async Task VerifySource_SkipChecksum_ReturnsTrue()
    {
        var localFile = Path.Combine(_tempDir.Path, "verify.bin");
        await File.WriteAllTextAsync(localFile, "verify me");

        var (builder, _) = await CreateBuilder("verify-test", "1.0.0",
            sources: @"{ ""verify.bin"" }",
            checksums: @"{ ""SKIP"" }");

        builder.InitializeDirectories();
        await builder.FetchSourceAsync(0);

        var result = await builder.VerifySourceAsync(0);
        Assert.True(result);
    }

    [Fact]
    public async Task VerifySource_Sha256Match_ReturnsTrue()
    {
        var localFile = Path.Combine(_tempDir.Path, "match.bin");
        var content = "match this data";
        await File.WriteAllTextAsync(localFile, content);

        var sha256 = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        var expectedHash = Convert.ToHexString(sha256).ToLowerInvariant();

        var (builder, _) = await CreateBuilder("hash-test", "1.0.0",
            sources: @"{ ""match.bin"" }",
            checksums: $@"{{ ""sha256:{expectedHash}"" }}");

        builder.InitializeDirectories();
        await builder.FetchSourceAsync(0);

        var result = await builder.VerifySourceAsync(0);
        Assert.True(result);
    }

    [Fact]
    public async Task VerifySource_Sha256Mismatch_ReturnsFalse()
    {
        var localFile = Path.Combine(_tempDir.Path, "mismatch.bin");
        await File.WriteAllTextAsync(localFile, "wrong data");

        var (builder, _) = await CreateBuilder("mismatch-test", "1.0.0",
            sources: @"{ ""mismatch.bin"" }",
            checksums: @"{ ""sha256:0000000000000000000000000000000000000000000000000000000000000000"" }");

        builder.InitializeDirectories();
        await builder.FetchSourceAsync(0);

        var result = await builder.VerifySourceAsync(0);
        Assert.False(result);
    }

    // ── InvokePrepareAsync ───────────────────────────────────────────────

    [Fact]
    public async Task InvokePrepare_NullFunction_ReturnsFalse()
    {
        var (builder, _) = await CreateBuilder("no-prepare", "1.0.0");

        var result = await builder.InvokePrepareAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task InvokePrepare_DefinedFunction_ReturnsTrue()
    {
        var (builder, _) = await CreateBuilder("with-prepare", "1.0.0",
            extraFields: "prepare = function() end");

        var result = await builder.InvokePrepareAsync();
        Assert.True(result);
    }

    // ── InvokeBuildAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task InvokeBuild_NullFunction_ReturnsFalse()
    {
        var (builder, _) = await CreateBuilder("no-build", "1.0.0");

        var result = await builder.InvokeBuildAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task InvokeBuild_DefinedFunction_ReturnsTrue()
    {
        var (builder, _) = await CreateBuilder("with-build", "1.0.0",
            extraFields: "build = function() end");

        var result = await builder.InvokeBuildAsync();
        Assert.True(result);
    }

    // ── InvokePackageAsync ───────────────────────────────────────────────

    [Fact]
    public async Task InvokePackage_NullFunction_ReturnsFalse()
    {
        var (builder, _) = await CreateBuilder("no-package", "1.0.0");

        var result = await builder.InvokePackageAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task InvokePackage_DefinedFunction_ReturnsTrue()
    {
        var (builder, _) = await CreateBuilder("with-package", "1.0.0",
            extraFields: "package = function() end");

        var result = await builder.InvokePackageAsync();
        Assert.True(result);
    }

    // ── InvokeCheckAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task InvokeCheck_NullFunction_ReturnsFalseNull()
    {
        var (builder, _) = await CreateBuilder("no-check", "1.0.0");

        var (invoked, result) = await builder.InvokeCheckAsync();
        Assert.False(invoked);
        Assert.Null(result);
    }

    [Fact]
    public async Task InvokeCheck_DefinedReturningTrue_ReturnsTrueTrue()
    {
        var (builder, _) = await CreateBuilder("check-pass", "1.0.0",
            extraFields: "check = function() return true end");

        var (invoked, result) = await builder.InvokeCheckAsync();
        Assert.True(invoked);
        Assert.True(result);
    }

    // ── InvokeGetVersionAsync ────────────────────────────────────────────

    [Fact]
    public async Task InvokeGetVersion_NullFunction_ReturnsFalseNull()
    {
        var (builder, _) = await CreateBuilder("no-version", "1.0.0");

        var (invoked, version) = await builder.InvokeGetVersionAsync();
        Assert.False(invoked);
        Assert.Null(version);
    }

    [Fact]
    public async Task InvokeGetVersion_DefinedReturningString_ReturnsVersion()
    {
        var (builder, _) = await CreateBuilder("version-func", "1.0.0",
            extraFields: "get_version = function() return \"3.2.1-beta\" end");

        var (invoked, version) = await builder.InvokeGetVersionAsync();
        Assert.True(invoked);
        Assert.NotNull(version);
        Assert.Equal("3.2.1-beta", version!.RawString);
    }

    // ── CompressPackageAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CompressPackage_WritesMetaAndCreatesZip()
    {
        var (builder, _) = await CreateBuilder("zip-test", "1.2.3");

        builder.InitializeDirectories();
        var zipPath = await builder.CompressPackageAsync();

        Assert.True(File.Exists(zipPath));
        Assert.EndsWith(".pacminepack.zip", zipPath);

        using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
        var metaEntry = archive.GetEntry(PackageParser.META_FILE_NAME);
        Assert.NotNull(metaEntry);
    }

    // ── CleanUp ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CleanUp_RemovesSourceAndPackageDirs()
    {
        var (builder, _) = await CreateBuilder("cleanup-test", "1.0.0");

        builder.InitializeDirectories();
        Assert.True(Directory.Exists(builder.SourceDirectory.FullName));
        Assert.True(Directory.Exists(builder.PackageDirectory.FullName));

        builder.CleanUp();

        Assert.False(Directory.Exists(builder.SourceDirectory.FullName));
        Assert.False(Directory.Exists(builder.PackageDirectory.FullName));
    }

    // ── Full pipeline smoke test ─────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_FetchBuildPackage_Succeeds()
    {
        var localFile = Path.Combine(_tempDir.Path, "input.txt");
        await File.WriteAllTextAsync(localFile, "pipeline test data");

        var sha256 = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("pipeline test data"));
        var expectedHash = Convert.ToHexString(sha256).ToLowerInvariant();

        var (builder, _) = await CreateBuilder("full-pipeline", "1.0.0",
            sources: @"{ ""input.txt"" }",
            checksums: $@"{{ ""sha256:{expectedHash}"" }}");

        builder.InitializeDirectories();

        // Fetch
        await builder.FetchSourceAsync(0);
        Assert.True(await builder.VerifySourceAsync(0));

        // Build (no-op build function)
        var buildResult = await builder.InvokeBuildAsync();
        Assert.False(buildResult);

        // Package
        var zipPath = await builder.CompressPackageAsync();
        Assert.True(File.Exists(zipPath));

        builder.CleanUp();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a Lua table literal for package meta with all required fields.
    /// </summary>
    private static string MetaLua(string name, string version, string? extra = null)
    {
        var fields = $@"
        name = ""{name}"",
        version = ""{version}"",
        description = ""Test package"",
        upstream_url = ""https://example.com"",
        category = ""mod"",
        license = ""MIT"",
        release = 1,
        epoch = 0,
        groups = {{}},
        provides = {{}},
        depends = {{}},
        conflicts = {{}},
        replaces = {{}},
        recommends = {{}}";

        if (extra != null)
            fields += ",\n        " + extra;

        return "{ " + fields + " }";
    }

    /// <summary>
    /// Loads a recipe from the given Lua script string (containing a full return table).
    /// </summary>
    private async Task<PackageCraftRecipe> LoadRecipe(string luaScript)
    {
        var factory = new PackageBuilderFactory();
        return await factory.LoadRecipeAsync(luaScript);
    }

    /// <summary>
    /// Creates a fully configured builder with a Lua recipe having all required meta fields.
    /// </summary>
    private async Task<(PackageBuilder builder, PackageBuilderFactory factory)> CreateBuilder(
        string name,
        string version,
        string? sources = null,
        string? checksums = null,
        string? extraFields = null)
    {
        var sourcesLine = sources != null ? $",\n    sources = {sources}" : ", sources = {}";
        var checksumsLine = checksums != null ? $",\n    source_checksums = {checksums}" : ", source_checksums = {}";
        var extraLine = extraFields != null ? $",\n    {extraFields}" : "";

        var luaScript = $@"
return {{
    protocol = ""1.0"",
    meta = {MetaLua(name, version)}{sourcesLine}{checksumsLine}{extraLine}
}}
";

        var factory = new PackageBuilderFactory();
        factory.ConfigureWorkingDirectory(_tempDir.Path);
        await factory.LoadRecipeAsync(luaScript);
        var builder = factory.CreateBuilder();
        return (builder, factory);
    }
}
