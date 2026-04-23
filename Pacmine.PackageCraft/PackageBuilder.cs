using System.IO.Compression;
using System.Security.Cryptography;
using Downloader;
using Lua;
using Pacmine.PackageCraft.LuaLibrary;

namespace Pacmine.PackageCraft;

public class PackageBuilder
{
    private LuaState luaState;
    private FileInfo?[] trackedSources;
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

    public DirectoryInfo? WorkingDirectory { get; private set; }

    public DirectoryInfo? SourceDirectory { get; private set; }

    public DirectoryInfo? PackageDirectory { get; private set; }

    public DirectoryInfo? OutputDirectory { get; private set; }

    public PackageCraftRecipe Recipe { get; private set; }

    public bool AllowFileLib { get; private set; } = true;

    public bool AllowOsLib { get; private set; } = true;

    public bool AllowArbitaryFileOperation { get; private set; } = false;

    public bool AllowShellExceution { get; private set; } = false;

    public static async Task<PackageBuilder> CreateAsync(string script)
    {
        PackageBuilder builder = new();
        var result = (await builder.luaState.DoStringAsync(script)).First().Read<LuaTable>();
        builder.Recipe = PackageCraftRecipeLuaObject.FromLuaTable(result);
        builder.trackedSources = new FileInfo?[builder.Recipe.Sources.Count];
        return builder;
    }

    public PackageBuilder ConfigureWorkingDirector(string workingDir)
    {
        WorkingDirectory = new DirectoryInfo(workingDir);
        SourceDirectory = new DirectoryInfo(Path.Combine(workingDir, "src"));
        PackageDirectory = new DirectoryInfo(Path.Combine(workingDir, "pkg"));
        OutputDirectory = new DirectoryInfo(workingDir);
        return this;
    }

    public PackageBuilder ConfigureSourceDirectory(string srcDir)
    {
        SourceDirectory = new DirectoryInfo(srcDir);
        return this;
    }

    public PackageBuilder ConfigurePackageDirectory(string pkgDir)
    {
        PackageDirectory = new DirectoryInfo(pkgDir);
        return this;
    }

    public PackageBuilder ConfigureOutputDirectory(string outDir)
    {
        OutputDirectory = new DirectoryInfo(outDir);
        return this;
    }

    public PackageBuilder ConfigureFileLib(bool allow)
    {
        AllowFileLib = allow;
        return this;
    }

    public PackageBuilder ConfigureOsLib(bool allow)
    {
        AllowOsLib = allow;
        return this;
    }

    public PackageBuilder ConfigureArbitaryFileOperation(bool allow)
    {
        AllowArbitaryFileOperation = allow;
        return this;
    }

    public PackageBuilder ConfigureShellExceution(bool allow)
    {
        AllowShellExceution = allow;
        return this;
    }

    public void InitEnvironment()
    {
        if (SourceDirectory == null || PackageDirectory == null)
        {
            throw new Exception("Source or Package directory is not configured");
        }
        SourceDirectory.Create();
        PackageDirectory.Create();
        luaState.Environment["file"] = new FileLuaLibrary(this);
    }

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
            // TODO: git support
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

    public async Task<bool> VerifySourceAsync(int index)
    {
        if (Recipe.SourceChecksums[index] == "SKIP")
        {
            return true;
        }
        var seg = Recipe.SourceChecksums[index].Split(':');
        var algo = seg[0];
        var checksum = seg[1];
        var file = trackedSources[index];
        if (file == null)
        {
            return false;
        }

        HashAlgorithm hashAlgo = algo switch
        {
            "sha1" => SHA1.Create(),
            "sha256" => SHA256.Create(),
            "sha512" => SHA512.Create(),
            "md5" => MD5.Create(),
            _ => throw new Exception($"Unsupported checksum algorithm: {algo}"),
        };
        var hash = hashAlgo.ComputeHash(File.ReadAllBytes(file.FullName));
        return Convert.ToHexString(hash).Equals(checksum, StringComparison.CurrentCultureIgnoreCase);
    }

    public async Task<bool> InvokePrepareAsync()
    {
        if (Recipe.Prepare == null)
            return false;
        await luaState.CallAsync(Recipe.Prepare, []);
        return true;
    }

    public async Task<bool> InvokeGetVersionAsync()
    {
        if (Recipe.GetVersion == null)
            return false;
        var result = await luaState.CallAsync(Recipe.GetVersion, []);
        Recipe.Meta.Version = new(result[0].Read<string>());
        return true;
    }

    public async Task<bool> InvokeBuildAsync()
    {
        if (Recipe.Build == null)
            return false;
        await luaState.CallAsync(Recipe.Build, []);
        return true;
    }

    public async Task<bool> InvokeCheckAsync()
    {
        if (Recipe.Check == null)
            return false;
        await luaState.CallAsync(Recipe.Check, []);
        return true;
    }

    public async Task<bool> InvokePackageAsync()
    {
        if (Recipe.Package == null)
            return false;
        await luaState.CallAsync(Recipe.Package, []);
        return true;
    }

    public async Task CompressPackageAsync()
    {
        if (PackageDirectory == null || OutputDirectory == null)
        {
            throw new Exception("Package or Output directory is not configured");
        }
        ZipFile.CreateFromDirectory(
            PackageDirectory.FullName,
            Path.Combine(OutputDirectory.FullName, $"{Recipe.Meta.Name}-{Recipe.Meta.GetFullVersionString()}.pacminepack.zip"));
    }

    public async Task CleanUpAsync()
    {
        SourceDirectory?.Delete();
        PackageDirectory?.Delete();
    }
}