# Pacmine.Environment

Package registry and filesystem management for Pacmine game instances. This library manages the lifecycle of `.pacmine` environments — the per-instance database that tracks which packages are installed, what files they own, and their checksums.

## Key Concepts

### Environment
An environment maps to a single game instance directory (usually a folder in `.minecraft/versions`). It stores all Pacmine metadata in a `.pacmine/` subdirectory.

```
<version-folder>/
├─ <other game files>
└─ .pacmine/
   ├─ lock
   ├─ packlist
   ├─ managed_files.json
   └─ registry/
      ├─ a/   (first layer, sort the packages by the initial character of package name)
      ├─ b/
      ├─ ...
      └─ s/
         └─ sodium-mc26.1-fabric.json   (one file -> one registry record of a package)
```

### Locking
Environments use PID-based file locking to prevent concurrent modifications. `Create()` and `Access()` acquire the lock; `Dispose()` releases it. Accessing locked environments throw `IOException` with the holding process's PID.

### Package Registry
Each installed package gets a JSON registry entry under `registry/{init-character}/{package-name}.json`. The registry stores the package's `PackageMeta`, file list with SHA-256 checksums, install reason, and timestamps.

### Registry Cache

`packlist` and `managed_files.json` are cache files storing neccessary information for fast lookups. Normally they should be synchronized with the actual data in the registry(which means users should not modify them manually). If anything goes wrong, use `Repair()` to regenerate them.

## Usage

### Create an environment

```csharp
using var env = PacmineEnvironment.Create("/path/to/instance");
```

### Open an existing environment

```csharp
using var env = PacmineEnvironment.Access("/path/to/instance");
```

### Write a registry record of a dummy package containing no files

Registry modification and file operations are handled in separate methods, allowing fine-grained control over the environment management process.

```csharp
env.WriteRegistry(new PackageRegistry
{
    Meta = new PackageMeta
    {
        Name = "minecraft",
        Version = new VersionIdentifier("26.1.2")
        Description = "environment package minecraft",
        Category = "env",
    },
    FileList = [],
    InstallReason = InstallReasons.Environment,
    InstalledTime = DateTime.UtcNow
});
```

### Install a (real) package

Keep in mind that registry I/O and file system I/O are separate, so you can retry one without affecting the other.

```csharp
// Step 1: extract the .pacminepack.zip into a temporary directory
// (use System.IO.Compression.ZipFile.ExtractToDirectory)

// Step 2: install files into the game instance, getting SHA-256 checksums
var fileList = env.UpdateFiles("sodium", new DirectoryInfo(tempDir));

// Step 3: write the registry record to persist the file list
env.WriteRegistry(new PackageRegistry
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

### Uninstall a package

```csharp
// Delete the files owned by the package
env.RemovePackageFiles("sodium");

// Remove the registry record
env.RemoveRegistry("sodium");
```

### Forget about a package but keep its files

```csharp
env.RemoveRegistry("sodium");
```

### Destroying an environment

```csharp
env.Destroy();  // releases lock and deletes .pacmine/
                // note that package files stay untouched
```

## Dependencies

- [Pacmine.Core](../Pacmine.Core/)
- `System.Text.Json` (built into .NET 10)
