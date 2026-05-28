# Pacmine.Environment

Package registry and filesystem management for Pacmine game instances. This library manages the lifecycle of `.pacmine` environments — the per-instance database that tracks which packages are installed, what files they own, their checksums, and inter-package dependency/conflict relationships.

The module is composed of three focused sub-modules, coordinated by [`PacmineEnvironment`](xref:Pacmine.Environment.PacmineEnvironment):

- **[`EnvironmentLock`](xref:Pacmine.Environment.EnvironmentLock)** — PID-based file locking
- **[`RegistryStore`](xref:Pacmine.Environment.RegistryStore)** — JSON registry CRUD with atomic writes
- **[`FileManager`](xref:Pacmine.Environment.FileManager)** — pure static filesystem operations

## Key Concepts

### Environment

An environment maps to a single game instance directory (usually a folder in `.minecraft/versions`). It stores all Pacmine metadata in a `.pacmine/` subdirectory.

```plaintext
<version-folder>/
├─ <game files, mod jars, etc.>
└─ .pacmine/
   ├─ lock                                  # PID-based concurrency lock
   ├─ package_list                          # plain-text index: one package name per line
   └─ registry/                             # Package metadata, sharded by first character
      ├─ a/
      ├─ b/
      ├─ ...
      └─ s/
         └─ sodium-mc26.1-fabric.json       # one JSON file per package
```

### Locking

Environments use PID-based file locking to prevent concurrent modifications. [`PacmineEnvironment.Create()`](xref:Pacmine.Environment.PacmineEnvironment.Create*) and [`Access()`](xref:Pacmine.Environment.PacmineEnvironment.Access*) acquire the lock via [`EnvironmentLock`](xref:Pacmine.Environment.EnvironmentLock); [`Dispose()`](xref:Pacmine.Environment.PacmineEnvironment.Dispose) releases it. Accessing a locked environment throws an `IOException` with the holding process's PID.

Use [`EnvironmentLock.GetLockerPid(directory)`](xref:Pacmine.Environment.EnvironmentLock.GetLockerPid*) to check whether a directory is locked without attempting to access it. Returns `-1` if not locked.

### Package Registry

Each installed package gets a JSON registry entry under `registry/{init-character}/{package-name}.json`. The registry stores the package's [`PackageMeta`](xref:Pacmine.Core.PackageMeta) (name, version, dependencies, conflicts, provides, replaces), file list with SHA-256 checksums, [`InstallReason`](xref:Pacmine.Environment.InstallReasons) (`Explicit`, `AsDependency`, or `Environment`), and timestamps.

The registry is managed by [`RegistryStore`](xref:Pacmine.Environment.RegistryStore), which provides:

| Method | Description |
|--------|-------------|
| `Write(registry)` | Atomic write (tmp → target rename) with rollback on failure |
| `Remove(name)` | Deletes the JSON file and updates in-memory state |
| `TryGet(name)` | Returns the registry entry, or `null` |
| `GetAll()` | Read-only view of all entries |
| `GetAllMetas()` | All package metadata as an array |
| `Contains(name)` | Checks existence |
| `Scan()` | Rebuilds in-memory state and `package_list` from disk enumeration |

All registry writes use an **atomic write pattern**: new content is serialized to a `.tmp` file, the existing file is backed up to `.old`, the temp file is atomically renamed over the target via `File.Move`, and the backup is deleted. If any step fails, the original file is restored from backup.

### File Management

File system operations are handled by the static [`FileManager`](xref:Pacmine.Environment.FileManager) class:

| Method | Description |
|--------|-------------|
| `UpdateFiles(rootPath, source, previouslyOwnedFiles)` | Copies files from source into the environment, computes SHA-256 checksums, prunes stale files |
| `RemoveFiles(rootPath, filePaths)` | Deletes specified files from disk (best-effort) |
| `CheckConflictFiles(rootPath, fileNames, managedFiles, ignoredOwners)` | Checks whether files conflict with managed or orphan files |

File operations are **independent of registry operations** — the caller sequences them explicitly, allowing retry of one without affecting the other.

## Usage

### Create an environment

```csharp
using var env = PacmineEnvironment.Create("/path/to/instance");
```

### Open an existing environment

```csharp
using var env = PacmineEnvironment.Access("/path/to/instance");
```

### Check if a directory is locked

```csharp
int pid = EnvironmentLock.GetLockerPid("/path/to/instance");
// pid is -1 if not locked, otherwise the process ID holding the lock
```

### Write a registry record (e.g., an environment package with no files)

Registry modification and file operations are handled separately, allowing fine-grained control over the environment management process.

[`RegistryStore.Write()`](xref:Pacmine.Environment.RegistryStore.Write*) returns `true` only if the registry entry was successfully persisted to disk. Returns `false` on I/O failure (disk full, permissions, etc.) — in that case, the original file is restored from backup.

```csharp
bool success = env.Registry.Write(new PackageRegistry
{
    Meta = new PackageMeta
    {
        Name = "minecraft",
        Version = new VersionIdentifier("26.1.2"),
        Description = "environment package minecraft",
        Category = "env",
    },
    FileList = [],
    InstallReason = InstallReasons.Environment,
    InstalledTime = DateTime.UtcNow
});

if (!success)
{
    // Handle failure — e.g., retry or report insufficient disk space
}
```

### Retrieve a registry record

```csharp
PackageRegistry? record = env.Registry.TryGet("sodium");

// Or get all metadata
PackageMeta[] allMetas = env.Registry.GetAllMetas();

// Or iterate all entries
foreach (var (name, reg) in env.Registry.GetAll())
{
    Console.WriteLine($"{name} {reg.Meta.GetFullVersionString()}");
}
```

### Install a (real) package

Keep in mind that registry I/O and file system I/O are separate, so you can retry one without affecting the other.

```csharp
// Step 1: extract the .pacminepack.zip into a temporary directory
// (use System.IO.Compression.ZipFile.ExtractToDirectory)

// Step 2: install files into the game instance, getting SHA-256 checksums
var previouslyOwned = env.Registry.TryGet("sodium")
    ?.FileList.Keys.ToHashSet();
var fileList = FileManager.UpdateFiles(
    env.RootPath, new DirectoryInfo(tempDir), previouslyOwned);

// Step 3: write the registry record to persist the file list
bool success = env.Registry.Write(new PackageRegistry
{
    Meta = meta,
    FileList = fileList,
    InstallReason = InstallReasons.Explicit,
    PackagedTime = packagedAt,
    InstalledTime = DateTime.UtcNow
});

// Step 4: clean up the temp directory
Directory.Delete(tempDir, recursive: true);
```

### Check package acceptance before installing

Before writing a registry record, check whether the packages to be installed would conflict with already-installed packages or have missing dependencies using [`PackageRelationUtil`](xref:Pacmine.Core.PackageRelationUtil):

```csharp
var existingMetas = env.Registry.GetAllMetas();
var newMetas = packagesToCheck.Select(p => p.Meta).ToArray();

// Full check on the combined set
var reasons = PackageRelationUtil.CheckSet(
    existingMetas.Concat(newMetas).ToArray());

// Or use incremental checks
var addReasons = PackageRelationUtil.CheckAdd(existingMetas, newMetas);

foreach (var reason in reasons)
{
    switch (reason)
    {
        case ConflictInvalidReason c:
            // c.TargetPackageName conflicts with c.ConflictingPackageName
            break;
        case MissingDependsInvalidReason m:
            // m.TargetPackageName has unsatisfied dependency m.MissingDependName
            break;
        case PackageReplacedInvalidReason r:
            // r.TargetPackageName replaces r.ReplacedPackageName
            break;
    }
}
```

### Check if packages can be uninstalled

```csharp
var existingMetas = env.Registry.GetAllMetas();
var removeReasons = PackageRelationUtil.CheckRemove(existingMetas, ["sodium"]);

foreach (var reason in removeReasons)
{
    if (reason is MissingDependsInvalidReason m)
    {
        // removing this package would break m.TargetPackageName
        // which depends on m.MissingDependName
    }
}
```

### Check for file conflicts

Build a file-to-owner mapping from the registry, then check for conflicts:

```csharp
// Build managed file map from registry
var managedFiles = new Dictionary<string, string>();
foreach (var (pkgName, pkgReg) in env.Registry.GetAll())
{
    foreach (var filePath in pkgReg.FileList.Keys)
        managedFiles[filePath] = pkgName;
}

// Check if files would conflict with files managed by other packages
// Pass an array of package names to ignore (e.g., the current package being updated)
var conflicts = FileManager.CheckConflictFiles(
    env.RootPath, ["mods/sodium.jar"], managedFiles, []);

foreach (var (fileName, owner) in conflicts)
{
    if (string.IsNullOrEmpty(owner))
    {
        // orphan file on disk (not managed by any package)
    }
    else
    {
        // file is owned by `owner`
    }
}
```

### Uninstall a package

```csharp
var reg = env.Registry.TryGet("sodium");
if (reg != null)
{
    // Delete the files owned by the package
    FileManager.RemoveFiles(env.RootPath, reg.FileList.Keys);

    // Remove the registry record
    bool success = env.Registry.Remove("sodium");
}
```

### Forget about a package but keep its files

```csharp
bool success = env.Registry.Remove("sodium");
```

Note: [`RegistryStore.Remove()`](xref:Pacmine.Environment.RegistryStore.Remove*) does **not** throw if the package is missing — it returns `false` instead.

### Rebuild registry from disk

Rebuilds the in-memory registry state and `package_list` file by enumerating all JSON files in the registry folder:

```csharp
env.Registry.Scan();
```

### Destroying an environment

```csharp
env.Destroy();  // releases lock and deletes .pacmine/
                // note that package files stay untouched
```

## Dependencies

- [Pacmine.Core](core.html)
- `System.Text.Json` (built into .NET 10)

## Class Reference

For detailed documentation for all members in `Pacmine.Environment` namespace, check the [API reference page](/api/Pacmine.Environment.html).
