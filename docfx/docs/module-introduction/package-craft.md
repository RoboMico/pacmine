# Pacmine.PackageCraft

Lua-driven package build pipeline for the Pacmine ecosystem. This library parses PackageCraft recipes written in Lua, fetches and verifies source artifacts, executes build-phase hooks, and produces distributable `.pacminepack.zip` archives.

## Key Concepts

You may want to read [this artical that doesn't exist yet]() to know more about how PackageCraft works. This article only covers the C# module and how to properly use it from the software developer's perspective.

### Builder Factory

`PackageBuilderFactory` is the entry point for consumers. It uses a fluent API to configure directories, permissions, and download settings, then parses a Lua recipe script and produces a configured `PackageBuilder`.

Permissions are decided at **configuration time** — disabled features are never registered into the Lua state, so there are no runtime permission checks.

| Property | Default | Description |
|---|---|---|
| `AllowFilesysLib` | `true` | Whether the `filesys` Lua library is available |
| `AllowArbitraryFileOperation` | `false` | Whether filesys can access paths outside `src/` and `pkg/` |
| `AllowShellExecution` | `false` | Whether `shell()` is available in Lua |
| `GitCommand` | `null` | Git executable path; `null` disables Git integration |

### Build Pipeline

`PackageBuilder` orchestrates the build in these steps:

1. **Initialize directories** — creates `src/` and `pkg/`
2. **Fetch sources** — HTTP download, local file copy, or Git clone
3. **Verify checksums** — SHA-1, SHA-256, SHA-512, or MD5
4. **Invoke Lua hooks** — optional `prepare`, `get_version`, `build`, `check`, `package` functions
5. **Compress output** — writes `.PACMINE.META.json` and zips `pkg/` into a `.pacminepack.zip`
6. **Clean up** — removes temporary directories

Build output (stdout/stderr) is exposed via `ChannelReader<string>` properties (`StdoutReader` / `StderrReader`) for real-time async consumption.

### Source Fetchers

Three fetcher implementations handle different source URL schemes:

| Scheme | Class | Behavior |
|---|---|---|
| `http://` / `https://` | `RemoteSourceFetcher` | Downloads the file using the `Downloader` library |
| _(no scheme)_ | `LocalFileSourceFetcher` | Copies a file from the working directory into `src/` |
| `git://` | `GitSourceFetcher` | Clones a Git repository with `--depth 1`; supports `#branch` and `$revision` fragments |

### Lua Libraries

The `filesys` Lua object exposes `move`, `copy`, `delete`, `deletedir`, and `mkdir` operations. Two variants control the access scope:

- **`RestrictedFilesysLuaLibrary`** (default) — sandboxes all operations to `SourceDirectory` and `PackageDirectory`. Throws `UnauthorizedAccessException` for out-of-bounds paths.
- **`UnsafeFilesysLuaLibrary`** — no path restrictions. Only injected when `AllowArbitraryFileOperation` is set to `true`.

### Lua Global Functions

Global functions registered on the Lua state at builder creation:

| Function | Availability | Purpose |
|---|---|---|
| `print(msg)` | Always | Writes to the builder's stdout channel |
| `printerr(msg)` | Always | Writes to the builder's stderr channel |
| `shell(cmd, timeout?)` | When `AllowShellExecution` is `true` | Runs a system shell command |
| `git(args, timeout?)` | When `GitCommand` is configured | Runs Git with the given arguments |

## Usage

### If you come here for how to write a recipe script...

Nah, no Lua code snippets here. We have a [dedicated tutorial]()(which doesn't exist yet) covering that topic. For the complete Lua recipe API, see [this directory which doesn't exist yet either]().

### Build a package from a Lua recipe

```csharp
using Pacmine.PackageCraft;

var script = await File.ReadAllTextAsync("recipe.lua");

using var factory = new PackageBuilderFactory()
    .ConfigureWorkingDirectory("/tmp/build")
    .ConfigureGit("git")
    .ConfigureShellExecution(false)
    .ConfigureArbitraryFileOperation(false);

await factory.LoadRecipeAsync(script);
using var builder = factory.CreateBuilder();

builder.InitializeDirectories();

for (int i = 0; i < recipe.Sources.Count; i++)
{
    await builder.FetchSourceAsync(i);
    if (!await builder.VerifySourceAsync(i))
        throw new Exception($"Checksum mismatch for source {i}");
}

await builder.InvokePrepareAsync();
await builder.InvokeGetVersionAsync();
await builder.InvokeBuildAsync();
await builder.InvokeCheckAsync();
await builder.InvokePackageAsync();
await builder.CompressPackageAsync();
builder.CleanUp();
```

### Customize the download configuration

```csharp
using Downloader;

var factory = new PackageBuilderFactory()
    .ConfigureWorkingDirectory("/tmp/build")
    .ConfigureDownloadConfig(new DownloadConfiguration
    {
        ChunkCount = 16,
        ParallelDownload = true
    });
```

### Consume build output in real time

```csharp
using var builder = factory.CreateBuilder();

_ = Task.Run(async () =>
{
    await foreach (var line in builder.StdoutReader.ReadAllAsync())
        Console.WriteLine($"[stdout] {line}");
});

_ = Task.Run(async () =>
{
    await foreach (var line in builder.StderrReader.ReadAllAsync())
        Console.Error.WriteLine($"[stderr] {line}");
});
```

## Dependencies

- [Pacmine.Core](core.html)
- [Downloader](https://github.com/bezzad/Downloader) (NuGet v5.5.0) — HTTP/HTTPS file downloads
- [LuaCSharp](https://github.com/peeweek/LuaCSharp) (NuGet v0.5.5) — Lua scripting runtime

## Class Reference

For detailed documentation for all members in `Pacmine.PackageCraft` namespace, check the [API reference page](/api/Pacmine.PackageCraft.html).
