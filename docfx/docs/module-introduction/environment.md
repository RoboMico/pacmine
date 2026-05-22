# Pacmine.Environment

Package registry and filesystem management for Pacmine game instances. This library manages the lifecycle of `.pacmine` environments — the per-instance database that tracks which packages are installed, what files they own, their checksums, and inter-package dependency/conflict relationships.

## Key Concepts

### Environment

An environment maps to a single game instance directory (usually a folder in `.minecraft/versions`). It stores all Pacmine metadata in a `.pacmine/` subdirectory.

```plaintext
<version-folder>/
├─ <game files, mod jars, etc.>
└─ .pacmine/
   ├─ lock                                  # PID-based concurrency lock
   ├─ index/                                # Fast-access cached index files
   │  ├─ package_list.json                  # package name -> version
   │  ├─ virtual_packages.json              # virtual package name -> versions -> providers
   │  ├─ depends_on.json                    # dependency name -> dependent packages (reverse dep index)
   │  ├─ managed_files.json                 # file path -> (owner, SHA-256)
   │  └─ deny_list.json                     # conflict source -> denied package -> version range
   └─ registry/                             # Package metadata, sharded by first character
      ├─ a/
      ├─ b/
      ├─ ...
      └─ s/
         └─ sodium-mc26.1-fabric.json       # one JSON file per package
```

### Locking

Environments use PID-based file locking to prevent concurrent modifications. `Create()` and `Access()` acquire the lock; `Dispose()` releases it. Accessing a locked environment throws an `Exception` with the holding process's PID.

Use `GetLockerPid(directory)` to check whether a directory is locked without attempting to access it. Returns `-1` if not locked.

### Package Registry

Each installed package gets a JSON registry entry under `registry/{init-character}/{package-name}.json`. The registry stores the package's `PackageMeta` (name, version, dependencies, conflicts, provides, replaces), file list with SHA-256 checksums, `InstallReason` (Explicit, AsDependency, or Environment), and timestamps.

### Index Files

The `index/` directory contains cached, denormalized views of registry data for fast lookups. Five index handlers are registered automatically:

| Index File | Handler | Content |
|---|---|---|
| `package_list.json` | `PackageListHandler` | Maps each package name to its installed `VersionIdentifier` |
| `virtual_packages.json` | `VirtualPackagesHandler` | Maps virtual package names to their versions and provider packages |
| `depends_on.json` | `DependsOnHandler` | Reverse dependency index — maps each dependency (real or virtual) to the list of packages that depend on it |
| `managed_files.json` | `ManagedFileListHandler` | Maps each file path to its owning package and SHA-256 checksum |
| `deny_list.json` | `DenyListHandler` | Maps each package to the packages and version ranges it conflicts with |

Index files are automatically kept in sync with registry writes and removals. If they get out of sync (e.g., due to manual modification or file corruption), call `Repair()` to rebuild them from registry data.

### Index Synchronization Guarantee

All index writes use an **atomic write pattern** to prevent in-memory/on-disk divergence:

1. New content is serialized to a temporary `.tmp` file
2. The temp file is atomically renamed over the target file via `File.Move` (atomic on the same filesystem)
3. In-memory state is only updated after the disk write succeeds

If a write fails (disk full, permissions, I/O error), the in-memory state remains unchanged and the original index file is intact.

Registry write/remove operations follow an **index-first** ordering:

- `TryWriteRegistry()` updates the index first; the registry JSON file is only written to disk if all index handlers succeed
- `TryRemoveRegistry()` updates the index first; the registry JSON file is only deleted if all index handlers succeed

This guarantees that if registry data is present on disk, the index is guaranteed to be consistent with it.

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
int pid = PacmineEnvironment.GetLockerPid("/path/to/instance");
// pid is -1 if not locked, otherwise the process ID holding the lock
```

### Write a registry record (e.g., an environment package with no files)

Registry modification and file operations are handled in separate methods, allowing fine-grained control over the environment management process.

`TryWriteRegistry()` returns `true` only if the index was successfully updated <b>and</b> the registry file was written. Returns `false` if any index handler failed to persist its data (disk full, I/O error, etc.) — in that case, the registry file is left unchanged. It is recommended to run `Repair()` afterwards in this case to avoid any inconsistency between different index handlers.

```csharp
bool success = env.TryWriteRegistry(new PackageRegistry
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
PackageRegistry? record = env.GetRegistry("sodium");
```

### Install a (real) package

Keep in mind that registry I/O and file system I/O are separate, so you can retry one without affecting the other.

```csharp
// Step 1: extract the .pacminepack.zip into a temporary directory
// (use System.IO.Compression.ZipFile.ExtractToDirectory)

// Step 2: install files into the game instance, getting SHA-256 checksums
var fileList = env.UpdateFiles("sodium", new DirectoryInfo(tempDir));

// Step 3: write the registry record to persist the file list
bool success = env.TryWriteRegistry(new PackageRegistry
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

Before writing a registry record, check whether the packages to be installed would conflict with already-installed packages or have missing dependencies:

```csharp
UnacceptReason[] reasons = env.CheckAcceptance(packagesToCheck);
if (reasons.Length > 0)
{
    foreach (var reason in reasons)
    {
        switch (reason)
        {
            case ConflictUnacceptReason c:
                // package c.RefusedPackageName conflicts with c.ConflictingPackageName at version range c.ConflictingVersions
                break;
            case MissingDependsUnacceptReason m:
                // package m.RefusedPackageName has unsatisfied dependency m.MissingDependName wanting m.DesiredVersions
                break;
            case PackageReplacedUnacceptReason r:
                // package r.RefusedPackageName replaces r.ReplacedPackageName
                break;
        }
    }
}
```

### Check if packages can be uninstalled

```csharp
UninstallDenyReason[] reasons = env.CheckCanUninstall(["sodium"]);
foreach (var reason in reasons)
{
    switch (reason)
    {
        case NotExistDenyReason:
            // package does not exist
            break;
        case BreakDependDenyReason b:
            // removing this package would break b.DependedBy which depends on it
            break;
    }
}
```

### Check for file conflicts

```csharp
// Check if files would conflict with files managed by other packages
// Pass an array of package names to ignore (e.g., the current package being updated)
var conflicts = env.CheckConflictFiles(["mods/sodium.jar"], []);

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

`TryRemoveRegistry()` returns `true` if the package was successfully removed from both the index and the registry. Returns `false` if the package does not exist, or if any index handler failed to persist (in which case the registry file is left intact).

```csharp
// Delete the files owned by the package
env.RemovePackageFiles("sodium");

// Remove the registry record
bool success = env.TryRemoveRegistry("sodium");
```

### Forget about a package but keep its files

```csharp
bool success = env.TryRemoveRegistry("sodium");
```

Note: `TryRemoveRegistry()` does **not** throw if the package is missing — it returns `false` instead.

### Repair the environment

Rebuilds index files from registry records. Returns `true` if repair is successful.

```csharp
bool repaired = env.Repair();
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
