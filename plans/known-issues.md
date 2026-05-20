# Known issues

found by AI. human checked this and removed false alarms. already fixed issues have the details removed and a stroke on the title.

## Pacmine.Core

### Critical Bugs

- ~~BUG-C1: `GetHashCode` violates `Equals` contract for versions with build metadata~~

- ~~BUG-C2: `IsConflictingWith` misses conflicts on non-SemVer virtual package versions~~

### Design Issues

- ~~DES-C1: Mutable `VersionIdentifier.RawString` allows invalid state transitions~~

### "Restrict Ahead of Time" Violations

- ~~R-C1: `VersionIdentifier` is mutable but used as a dictionary key in index handlers~~

---

## Pacmine.Environment

### Critical Bugs

- ~~BUG-E1: Lock leak in `Access()` when handler registration or Load fails~~

#### BUG-E2: TOCTOU race in `Create()`

**File:** `PacmineEnvironment.cs:200-220`

Two processes calling `Create()` simultaneously can both pass `Directory.Exists(spFolderPath)` before either acquires the lock. Both write initial index files, then one fails on `Lock()`. The directory exists on disk with potentially corrupted index files from the process that never acquired the lock.

**Fix:** Acquire the lock first, then check/create directories.

#### BUG-E3: Non-deterministic file ownership in `ManagedFileListHandler.OnRebuild`

**File:** `ManagedFileListHandler.cs:78-82`

If two packages declare ownership of the same file path, the last registry iterated wins:

```csharp
mngFiles[kvp.Key] = new ManagedFileRecord(registry.Meta.Name, kvp.Value);
```

Since `registries` order depends on filesystem enumeration order (non-deterministic), two rebuilds can produce different ownership assignments. This is a silent data corruption bug.

**Fix:** Detect and report file ownership conflicts, or use deterministic ordering (e.g., sort by package name).

### Medium Bugs

#### BUG-E4: Two separate `IndexManager` instances created during `Create()`

**File:** `PacmineEnvironment.cs:210-219`

An `initIndex` manager writes skeleton files to disk, then is discarded. `Access()` creates a second manager that loads the recently-written files. If `Access()` fails, skeleton files remain on disk with no lock. The initial write should happen after lock acquisition with the same manager instance.

#### BUG-E5: `RemoveRegistry` null-forgiving operator masks deserialization failures

**File:** `PacmineEnvironment.cs:474`

```csharp
registry!   // null-forgiving
```

If a registry JSON file is empty or malformed, `Deserialize` returns null. The `!` operator suppresses the warning but doesn't handle the null, causing `NullReferenceException` in downstream handlers. Add a null check.

#### BUG-E6: `CheckCanUninstall` produces duplicate reasons for multiple virtual package providers

**File:** `PacmineEnvironment.cs:389-426`

If package P provides virtual-a and virtual-b, and both have the same depender that would be broken, a `BreakDependDenyReason` is added for each. The result array may contain duplicates the caller must deduplicate.

#### BUG-E7: `FileShare.ReadWrite` in `GetLockerPid()` reader is overly permissive

**File:** `PacmineEnvironment.cs:133`

The reader opens with `FileShare.ReadWrite` while the lock holder uses `FileShare.Read`. While not a bug per se, it allows concurrent readers with write-intent. Use `FileShare.Read` for consistency.

### Design Issues

#### DES-E1: `new T()` constraint is fragile for `Initialize()`

**File:** `IndexHandler.cs:60`

`IndexHandler<T>` requires `T : new()`, and `Initialize()` simply writes `JsonSerializer.Serialize(new T())`. This assumes the default value is the identity element for the index. For future handler types, this assumption may not hold. An explicit abstract `Initialize()` override per handler would be safer.

#### DES-E2: `PacmineEnvironment.Path` shadows `System.IO.Path`

**File:** `PacmineEnvironment.cs:50`

The property named `Path` forces the use of fully-qualified `System.IO.Path` throughout the file. Renaming to `RootPath` or `EnvironmentPath` would be cleaner.

#### DES-E3: No intra-process thread safety

**File:** Multiple

`IndexManager._handlers` list and all handler `_content` dictionaries lack synchronization. While the file lock prevents cross-process races, multiple threads in the same process could produce torn reads. Either document single-threaded use or add synchronization.

### "Restrict Ahead of Time" Violations

#### R-E1: `_indexManager = null!` suppresses nullable reference type safety

**File:** `PacmineEnvironment.cs:15`

The field is null until `RegisterDefaultHandlers()` is called. Any code path accessing the index manager before registration gets NRE at runtime instead of compile-time. Solution: use `Lazy<IndexManager>` or initialize in the constructor.

#### R-E2: No validation on package name at API boundary

**File:** `PacmineEnvironment.cs:439, 459, 553`

Empty/null package names pass unchecked and crash at `packageName[0]` with `IndexOutOfRangeException`. Filesystem-illegal characters in package names crash in `Directory.Create()` or `File.WriteAllText`. Validate at entry points.

#### R-E3: Public `Path`, `SpecialFolder`, `RegistryFolder`, `IndexFolder`, `LockFile` exposed

**File:** `PacmineEnvironment.cs:50-61`

The internal directory structure and lock file path are publicly readable. The lock file exposure is particularly risky -- callers could accidentally touch it.

### DRY Violations

This is the **single largest code smell in the codebase**. All five index handlers (`PackageListHandler`, `ManagedFileListHandler`, `DenyListHandler`, `DependsOnHandler`, `VirtualPackagesHandler`) contain virtually identical code for:

1. **`OnLoad()`** — 14 lines each (~70 lines total), differing only in the type parameter to `JsonSerializer.Deserialize<>`
2. **`Content` property setter** — 5 lines each (~25 lines total), identical pattern: `set { _content = value; File.WriteAllText(...); }`
3. **`OnRebuild` diff-then-write pattern** — 3 lines each (~15 lines total): `JsonSerializer.Serialize` → string compare → `Content = ...`

All should be lifted into `IndexHandler<T>`:

- A protected abstract `string FileName { get; }` property
- A `protected bool SetIfChanged(T newContent)` helper
- Default implementations of `OnLoad()` and `Content` setter in the base class

**This refactor would eliminate ~110 lines of copy-pasted code and ensure consistent behavior across all handlers.**

### File I/O Error Handling Gaps

| Operation                | Location              | Handling                                     |
| ------------------------ | --------------------- | -------------------------------------------- |
| `WriteRegistry()`        | File.WriteAllText     | **Not caught** — disk-full/perms propagate   |
| `GetRegistry()`          | File.ReadAllText      | **Not caught** — file-in-use/perms propagate |
| `IndexManager.Rebuild()` | Directory enumeration | Silently swallowed — masks real errors       |
| All `OnLoad()`           | File.ReadAllText      | Silently swallowed → empty content           |

---

## Pacmine.PackageCraft

### Critical Bugs

#### BUG-P2: `LuaI_Groups` setter reads key instead of value

**File:** `PackageMetaLuaObject.cs:111-113`

```csharp
set {
    Groups = [];
    foreach (var item in value)
    {
        Groups.Add(item.Key.Read<string>());  // BUG: reads the numeric index "1", "2", etc.
    }
}
```

The getter writes group names as **values** with numeric keys (`table[1] = "group-a"`), but the setter reads `item.Key.Read<string>()` which returns the key (e.g., `"1"`, `"2"`) instead of the actual group name strings from `item.Value`. A recipe's `groups` are silently corrupted. Should be:

```csharp
Groups.Add(item.Value.Read<string>());
```

#### BUG-P3: `VerifySourceAsync` leaks `HashAlgorithm` instances

**File:** `PackageBuilder.cs:293-300`

```csharp
HashAlgorithm hashAlgo = algo switch { ... };
// hashAlgo is never disposed
```

`SHA1.Create()`, `SHA256.Create()`, etc. are `IDisposable`. Each call leaks native handles. Should use `using var hashAlgo = ...`.

#### BUG-P4: `VerifySourceAsync` uses culture-sensitive hex comparison

**File:** `PackageBuilder.cs:305`

```csharp
return Convert.ToHexString(hash).Equals(checksum, StringComparison.CurrentCultureIgnoreCase);
```

Should be `StringComparison.OrdinalIgnoreCase`. In cultures with aggressive case-folding rules (e.g., Turkish), this can produce incorrect results.

### Medium Bugs

#### BUG-P5: `CleanUpAsync` is synchronous but returns `Task`

**File:** `PackageBuilder.cs:401-404`

```csharp
public async Task CleanUpAsync()  // no await → CS1998 warning
{
    SourceDirectory?.Delete(true);
    PackageDirectory?.Delete(true);
}
```

Generates compiler warning CS1998. Callers expecting async deletion get synchronous blocking I/O. Should either use `await Task.Run(...)` or return `Task.CompletedTask` and remove `async`.

#### BUG-P6: Download filename race condition

**File:** `PackageBuilder.cs:167-171`

```csharp
string fileName = "";
dlService.DownloadStarted += (s, e) => { fileName = e.FileName; };
await dlService.DownloadFileTaskAsync(src, SourceDirectory.FullName);
if (string.IsNullOrEmpty(fileName))
    throw new InvalidOperationException(...);
```

The event handler closure races with `DownloadFileTaskAsync`. If the download is instant (e.g., cached), the event may not fire before the check.

#### BUG-P7: No timeout on git and shell processes

**File:** `PackageBuilder.cs:249, GlobalFunctions.cs:107, 163`

`process.WaitForExitAsync()` with no cancellation token. If a git server or shell command hangs, the build hangs indefinitely. Should use `WaitForExitAsync(CancellationToken)` with a configurable timeout.

#### BUG-P8: `VerifySourceAsync` loads entire file into memory

**File:** `PackageBuilder.cs:304`

```csharp
hashAlgo.ComputeHash(File.ReadAllBytes(file.FullName));
```

For large source files (game assets can be gigabytes), this causes OutOfMemoryException. Use `ComputeHash(Stream)` with `File.OpenRead`.

#### BUG-P9: `CompressPackageAsync` writes meta JSON then ZIPs — partial failure leaves corrupt state

**File:** `PackageBuilder.cs:391-394`

If the ZIP creation fails, the meta JSON file was already written. On retry, the meta file may be stale or incomplete. Write to a temp file first, move on success.

#### BUG-P10: No bounds check on `index` in `FetchSourceAsync` / `VerifySourceAsync`

**File:** `PackageBuilder.cs:161, 280-285`

No validation that `index < Sources.Count` or `index < SourceChecksums.Count`. If `SourceChecksums` has fewer entries than `Sources`, `VerifySourceAsync` throws `ArgumentOutOfRangeException`. Validate lengths match at recipe load time.

### Security Issues

#### SEC-1: Path check allows operations on source/package directory roots

**File:** `RestrictedFilesysLuaLibrary.cs:30-31`

```csharp
!resolvedPath.Equals(sourceDirectory.FullName, StringComparison.Ordinal)
```

A Lua script calling `filesys.delete("${SRCDIR}")` passes the assertion check. While `File.Delete` on a directory throws `UnauthorizedAccessException` in .NET (preventing actual damage), the path check itself is too permissive. The equality check was added so `${PKGDIR}` works without a trailing slash, but this permits root-directory operations. A better design requires `${PKGDIR}/` with trailing slash.

**Current test `Restricted_AssertsPath_EqualsDirectoryItself_Throws` passes for the wrong reason** — the exception comes from `File.Delete` rejecting a directory, NOT from `AssertPathAllowed`.

#### SEC-2: Case-sensitive path prefix check fails on Windows

**File:** `RestrictedFilesysLuaLibrary.cs:28-29`

```csharp
if (!resolvedPath.StartsWith(srcPrefix, StringComparison.Ordinal)
```

On case-insensitive filesystems (Windows, macOS default), `Path.GetFullPath` may return different casing than `DirectoryInfo.FullName`. Using `Ordinal` would block legitimate operations when case differs. Use `OrdinalIgnoreCase`.

### Design Issues

#### DES-P1: `FetchSourceAsync` does three distinct operations in one method

**File:** `PackageBuilder.cs:155-270`

HTTP download, git clone, and local file copy — three distinct source types in a single 115-line method. Each has different preconditions, error handling, and side effects. Should be separated into `FetchHttpAsync`, `FetchGitAsync`, `FetchLocalAsync`.

#### DES-P2: Lua object wrappers inherit from domain models rather than composing

**File:** `PackageCraftRecipeLuaObject.cs:10, PackageMetaLuaObject.cs:11`

```csharp
public partial class PackageCraftRecipeLuaObject : PackageCraftRecipe
public partial class PackageMetaLuaObject : PackageMeta
```

Inheritance couples the Lua serialization layer to the domain model. Properties like `LuaI_Name` pollute the type hierarchy. While `JsonSerializer.Serialize(Recipe.Meta)` uses the compile-time type `PackageMeta` (excluding LuaI\_ properties), this is fragile — if someone later uses `Serialize<object>(...)` or a runtime-typed serializer, Lua properties appear in the output. Composition would be cleaner.

#### DES-P3: `StandardOutput`/`StandardError` are properties that allocate

**File:** `PackageBuilder.cs:113-132`

Each access creates a new `StreamReader` wrapping a new `MemoryStream`. Callers are unlikely to dispose the returned `StreamReader`. Properties should not have side effects like allocations; use methods (e.g., `GetStandardOutput()`).

#### DES-P4: Async pipeline methods have undocumented ordering dependencies

**File:** `PackageBuilder.cs`

- `InitializeDirectories()` must be called before `FetchSourceAsync()` — no guard
- `FetchSourceAsync()` must be called before `VerifySourceAsync()` for same index — no guard
- `InvokeGetVersionAsync()` must be called before `CompressPackageAsync()` — mutation order dependency
- `CompressPackageAsync()` writes meta regardless of whether package phases ran

### DRY Violations

#### DRY-1: Dict-to-Lua-array and Lua-array-to-dict patterns repeated 6 times

**File:** `PackageMetaLuaObject.cs`, `PackageCraftRecipeLuaObject.cs`

`LuaI_Depends`, `LuaI_Conflicts`, `LuaI_Replaces`, `LuaI_Provides` (in PackageMetaLuaObject) plus `LuaI_Sources` and `LuaI_SourceChecksums` (in PackageCraftRecipeLuaObject) all implement the same 1-indexed Lua-array ↔ C# collection pattern. A helper method pair would eliminate ~80 lines.

#### DRY-2: Five Lua function property wrappers are identical

**File:** `PackageCraftRecipeLuaObject.cs:86-132`

`Prepare`, `GetVersion`, `Build`, `Check`, `Package` properties share the exact same getter/setter pattern differing only in the backing field name. A helper or code generator would help.

#### DRY-3: `GitCall` and `ShellExecute` share 80% identical code

**File:** `GlobalFunctions.cs:93-177`

Both create `ProcessStartInfo`, configure redirection, wire up output events, start, and wait. Only `FileName` and `Arguments` differ. Extract a `RunProcessAsync` helper.

#### DRY-4: `RestrictedFilesysLuaLibrary` and `UnsafeFilesysLuaLibrary` duplicate all method signatures

**File:** `RestrictedFilesysLuaLibrary.cs` vs `UnsafeFilesysLuaLibrary.cs`

All four operations (Move, Copy, Delete, CreateDirectory) have identical method signatures. The only difference is the path assertion step. Use the Template Method pattern with a virtual `AssertOperation(string source, string dest)` in the base class (no-op in unsafe, restrictive in restricted).

#### DRY-5: `DownloadConfiguration` defined in both `PackageBuilder.cs` and `PackageBuilderFactory.cs`

**File:** `PackageBuilder.cs:28-31, PackageBuilderFactory.cs:17-21`

Same `{ ChunkCount = 8, ParallelDownload = true }` config defined twice.

### Atomic Method Design Violations

1. **`FetchSourceAsync(int index)`** — fetches by HTTP, git, or file copy. Three distinct source types.
2. **`CompressPackageAsync()`** — writes meta JSON + creates ZIP. Two distinct operations.
3. **`CreateBuilder()`** — creates Lua state + registers filesys lib + creates builder + creates GlobalFunctions + registers them. Five distinct operations.
4. **`LoadRecipeAsync(string script)`** — disposes old state + creates new state + executes Lua + extracts recipe. Four operations.

### Async Pipeline Issues

- No enforcement that `FetchSourceAsync(index)` is called before `VerifySourceAsync(index)`. Calling out of order silently returns false.
- No enforcement that `InitializeDirectories()` is called before `FetchSourceAsync()`. Without it, directory-not-found errors occur.
- `CompressPackageAsync()` does not verify that package phases ran successfully. Missing a phase silently produces incomplete packages.

---

## Test Quality

### Overall Assessment

The test infrastructure (xUnit, temporary directories with `IDisposable` cleanup, shared `IndexHandlerTestBase`) is well-organized. Tests cover the happy path and basic error conditions. However, **critical bugs exist in production code that the test suite fails to detect**, indicating significant coverage gaps.

### Core Gaps

| Missing Test                                                          | Would Catch |
| --------------------------------------------------------------------- | ----------- |
| `new VersionIdentifier(null)`                                         | BUG-C3      |
| Hash code consistency for `"1.0.0+build.1"` vs `"1.0.0+other"`        | BUG-C1      |
| `IsConflictingWith` against non-SemVer virtual version with `*` range | BUG-C2      |
| `IsNewerThan(null)` / `IsConflictingWith(null)`                       | BUG-C4      |
| `Version` setter = null                                               | BUG-C5      |
| `PackageParser.GetMeta(null)` / `GetPackagedTime(null)`               | BUG-C6      |
| `Provides = null` via setter → `IsConflictingWith` crash              | DES-C2      |

### Environment Gaps

| Missing Test                                                 | Would Catch |
| ------------------------------------------------------------ | ----------- |
| `Access()` with handler registration that throws (lock leak) | BUG-E1      |
| Concurrent `Create()` calls (TOCTOU race)                    | BUG-E2      |
| Rebuild with file ownership conflicts from two packages      | BUG-E3      |
| `Repair()` method                                            | No coverage |
| `Destroy()` method                                           | No coverage |
| `UpdateFiles()` with actual file operations                  | No coverage |
| `RemovePackageFiles()` with actual file operations           | No coverage |
| `GetLockerPid()`                                             | No coverage |
| `CheckAcceptance` with virtual package conflicts             | No coverage |
| `CheckCanUninstall` with virtual deps + fallback provider    | No coverage |
| Lock contention (two concurrent `Access` calls)              | No coverage |

### PackageCraft Gaps

| Missing Test                                                   | Would Catch        |
| -------------------------------------------------------------- | ------------------ |
| `LuaI_Groups` round-trip with actual group names               | BUG-P2             |
| `VerifySourceAsync` with Turkish locale                        | BUG-P4             |
| `CleanUpAsync` with populated directories                      | BUG-P5             |
| `CompressPackageAsync` with very large files                   | BUG-P8             |
| Recipe with missing mandatory Lua fields                       | Missing validation |
| Concurrent stdout/stderr writes from multiple Lua invocations  | Thread safety      |
| `InvokeGetVersionAsync` Lua integration                        | No coverage        |
| `InvokeBuildAsync` / `InvokeCheckAsync` / `InvokePackageAsync` | No coverage        |
| `FetchSourceAsync` download path (HTTP)                        | No coverage        |

### Test Infrastructure Issues

- **`IndexHandlerTestBase.Dispose()`** catches no exceptions. If `Directory.Delete` fails (e.g., file in use), it cascades into other test failures.
- **`Restricted_AssertsPath_EqualsDirectoryItself_Throws`** passes for the wrong reason (see SEC-1).
- **`CreateTestRegistry` helpers** are duplicated between `IndexHandlerTestBase` (no `replaces` parameter) and `PacmineEnvironmentTests` (has `replaces`). Consolidate.
