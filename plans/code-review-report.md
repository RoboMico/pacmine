# Pacmine Codebase Review Report

**Date:** 2026-05-19
**Scope:** `Pacmine.Core`, `Pacmine.Environment`, `Pacmine.PackageCraft`, and their test suites (`Pacmine.Console` excluded)
**Review Criteria:** Core concept & pipeline design quality, implementation accuracy, DRY, Restrict-Ahead-of-Time, atomic method design, test case quality & edge case coverage

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Pacmine.Core](#pacminecore)
3. [Pacmine.Environment](#pacmineenvironment)
4. [Pacmine.PackageCraft](#pacminepackagecraft)
5. [Test Quality](#test-quality)
6. [CLI Readiness Assessment](#cli-readiness-assessment)

---

## Executive Summary

The codebase demonstrates **solid architectural thinking**: a clean layered design (Core → Environment/PackageCraft), a well-conceived Lua-driven build system with pre-runtime permission gating, and a pluggable index subsystem. The overall direction is sound.

However, the review found:

| Category                            | Pacmine.Core | Pacmine.Environment   | Pacmine.PackageCraft  |
| ----------------------------------- | ------------ | --------------------- | --------------------- |
| Critical bugs                       | 2            | 3                     | 4                     |
| Medium bugs                         | 5            | 2                     | 6                     |
| Design issues                       | 3            | 6                     | 5                     |
| DRY violations                      | 0            | ~110 duplicated lines | ~100 duplicated lines |
| "Restrict ahead of time" violations | 2            | 5                     | 4                     |
| Atomic method violations            | 0            | 0                     | 4                     |
| Test gaps                           | 7 scenarios  | 11 scenarios          | 10 scenarios          |

**Verdict:** The libraries are approximately **70% ready**. Critical bug fixes and the DRY refactor in the indexing subsystem should be completed before implementing the CLI application. Details below.

---

## Pacmine.Core

### Core Concepts & Design

**Well-designed:** `VersionIdentifier` wrapping `SemVersion` with fallback to string comparison; `VersionRange` wrapping npm-style ranges; `PackageMeta` with its relation properties; `PackageParser` as a static utility.

**Well-structured:** JSON converters are cleanly separated; `PackageNameRegex` uses `[GeneratedRegex]` for compile-time optimization.

### Critical Bugs

#### BUG-C1: `GetHashCode` violates `Equals` contract for versions with build metadata

**File:** `VersionIdentifier.cs:158-160, 170-172`

`Equals` uses `ComparePrecedenceTo` (which ignores build metadata per SemVer 2.0), but `GetHashCode` delegates to `SemVersion.GetHashCode()` which includes build metadata identifiers. Two semantically equivalent versions like `"1.0.0+abc"` and `"1.0.0+xyz"` will be equal via `Equals` but produce different hash codes. This breaks the fundamental .NET hash code contract and will cause silent failures in `Dictionary<VersionIdentifier, ...>`, `HashSet<VersionIdentifier>`, and LINQ operations like `Distinct()`.

#### BUG-C2: `IsConflictingWith` misses conflicts on non-SemVer virtual package versions

**File:** `PackageMeta.cs:143-155, 166-174`

When checking if `other.Provides` contains a virtual package that conflicts with this package, the method calls `virtualRange.Contains(virtualVersion)`. If the virtual package's version is a non-SemVer string (e.g., a Minecraft snapshot like `"25w14a"`), `Contains()` returns `false` even when the conflict range is `"*"` (aliased to `VersionRange.Any`). A package declaring `Conflicts: { "virtual-pkg": "*" }` will fail to detect a conflict against a package providing `virtual-pkg` at `"25w14a"`.

### Medium Bugs

#### BUG-C3: Null constructor argument crashes `VersionIdentifier`

**File:** `VersionIdentifier.cs:27`

`new VersionIdentifier(null)` passes null to `SemVersion.TryParse`, which throws `ArgumentNullException`. Expected behavior would be a non-SemVer instance with `RawString` = null or throwing `ArgumentNullException` with a clearer message.

#### BUG-C4: `IsNewerThan` and `IsConflictingWith` lack null guards

**File:** `PackageMeta.cs:122, 140`

`IsNewerThan(null)` → `NullReferenceException` at `other.Epoch`
`IsConflictingWith(null)` → `NullReferenceException` at `other.Name`

#### BUG-C5: `Version` setter accepts null

**File:** `PackageMeta.cs:41`

The `required` keyword prevents omission at object-initializer time, but nothing prevents setting it to null afterward via property assignment, reflection, or edge cases in deserialization. `GetFullVersionString()` would throw if this occurs.

#### BUG-C6: Null `archive` parameter not guarded in `PackageParser`

**File:** `PackageParser.cs:23, 39`

`GetMeta(null)` and `GetPackagedTime(null)` throw `NullReferenceException`.

### Design Issues

#### DES-C1: Mutable `VersionIdentifier.RawString` allows invalid state transitions

**File:** `VersionIdentifier.cs:35-43`

After construction with a valid SemVer, setting `RawString` to an invalid string changes the object's semantics while existing references to `SemVersion` become stale. Consider making the class immutable or a record.

#### DES-C2: Collection properties accept null via setter

**File:** `PackageMeta.cs:78-106`

`Groups`, `Provides`, `Depends`, `Conflicts`, `Replaces`, `Recommends` are auto-properties initialized to empty collections, but JSON deserialization with `"Provides": null` would replace them with null, causing `NullReferenceException` in any `foreach` over them. Use a property pattern with a null-coalescing setter.

#### DES-C3: `InvalidDataException` used for parse errors

**File:** `VersionRange.cs:26`

`InvalidDataException` (`System.IO`) is semantically incorrect for an invalid format string. `FormatException` or `ArgumentException` would be standard.

### "Restrict Ahead of Time" Violations

#### R-C1: `VersionIdentifier` is mutable but used as a dictionary key in index handlers

If a `VersionIdentifier` stored as a key in `VirtualPackagesHandler`'s dictionaries were mutated after insertion, the dictionary would be corrupted. Mitigation: make `VersionIdentifier` immutable.

#### R-C2: `VersionRange.Any` is a mutable reference type singleton

`VersionRange.Any` exposes a mutable class instance. Reflection could modify its internal `_range` field. Mitigation: freeze it with a readonly field or use a struct.

---

## Pacmine.Environment

### Core Concepts & Design

**Well-designed:** The `IndexHandler`/`IndexHandler<T>`/`IndexManager` abstraction is a clean observer/broadcast pattern. Five specialized handlers maintain derived cache files from a sharded JSON registry. The PID-based advisory file lock prevents concurrent access. The `UnacceptReason`/`UninstallDenyReason` discriminated union pattern is clean.

### Critical Bugs

#### BUG-E1: Lock leak in `Access()` when handler registration or Load fails

**File:** `PacmineEnvironment.cs:154-165`

```csharp
env.Lock();                      // lock acquired, _lockStream set
env.RegisterDefaultHandlers();   // if this throws...
env._indexManager.Load();        // ...or this throws...
return env;                      // caller never gets env, can never call Dispose()
```

If `RegisterDefaultHandlers()` or `_indexManager.Load()` throws, the `FileStream` held by `_lockStream` is leaked until GC finalization, and the lock file persists on disk with no owner. Other processes are permanently blocked.

**Fix:** Wrap in try/catch and call `Dispose()` on failure:

```csharp
env.Lock();
try {
    env.RegisterDefaultHandlers();
    env._indexManager.Load();
    return env;
} catch {
    env.Dispose();
    throw;
}
```

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

### Atomic Method Design

**No significant violations found.** Each public method maps to one conceptual operation. Methods like `CheckAcceptance` and `CheckCanUninstall` instantiate local registry caches and iterate indexes -- these are implementation details, not multiple responsibilities.

---

## Pacmine.PackageCraft

### Core Concepts & Design

**Well-designed:** The factory-based permission model is the right approach. Permissions are decided at factory configuration time, and disabled functions are never registered into the Lua state -- compile-time safety, not runtime checks. The Lua integration via `LuaCSharp` is cleanly abstracted through `[LuaMember]` attributes and wrapper classes.

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
| Git clone test (build end-to-end with git source)              | BUG-P1             |
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

---

## CLI Readiness Assessment

### What the CLI Design Commentary Shows

The comments in `Pacmine.Console/Program.cs` outline an ambitious CLI application with:

- 7 top-level commands (`init`, `destroy`, `install`, `build`, `uninstall`, `list`, `env`, `repair`)
- Interactive onboard wizard with environment auto-detection
- Install pipeline: accept → resolve → acquire → deploy → integrate
- Build pipeline: initialize → fetch → verify → prepare → get-version → build → check → package → compress → clean

This is a well-thought-out design. The stub already wires up command classes using `System.CommandLine`, so the CLI skeleton is in place.

### Is It the Right Moment to Implement the CLI?

**No. The libraries are not ready.** Here is the ordered checklist of prerequisites:

#### Must-fix before CLI (Critical Blockers)

1. **BUG-P2 (LuaI_Groups setter)** — Any recipe with `groups` produces corrupted data. The CLI's `build` command depends on correct recipe parsing.
2. **BUG-P1 (git --revision)** — Git-sourced recipes are completely broken. Any build using `git://url$refSpec` will fail.
3. **BUG-C1 (GetHashCode/Equals contract)** — `Dictionary<VersionIdentifier, ...>` silently corrupts when build metadata differs. The install pipeline depends on correct version comparison in index handlers.
4. **BUG-E1 (Lock leak in Access)** — If index loading fails after Lock, the lock file persists forever. Users would need to manually delete the lock file.
5. **BUG-E2 (TOCTOU race in Create)** — Concurrent `pacmine init` calls can corrupt the environment directory.
6. **DRY-E (Index handler code duplication)** — Before adding more features to the environment (which CLI commands will do), refactor the 110+ lines of duplicated JSON I/O into `IndexHandler<T>`. This reduces the surface for bugs when handlers are extended.

#### Should-fix before CLI (Quality Blockers)

7. **BUG-C2 (non-SemVer virtual conflict)** — Minecraft mods sometimes use non-SemVer names for virtual packages (e.g., Minecraft snapshots). The CLI `install` command's acceptance checks would incorrectly allow conflicting packages.
8. **BUG-P3 (HashAlgorithm leak)** — `VerifySourceAsync` leaks native handles. Multiple builds in sequence (CLI loop) would exhaust resources.
9. **BUG-P4 (culture-sensitive hex comparison)** — Hash verification can fail on non-English locales. The `build` command would produce false verification failures.
10. **SEC-1 (directory root path bypass)** — The restricted filesys library allows wildcards that, while not currently exploitable with `File.Delete`, could become exploitable if `Directory.Delete` is added later.
11. **SEC-2 (case-sensitive path check on Windows)** — Blocked legitimate operations on case-insensitive filesystems. The `build` command would break on Windows/macOS.

#### Should-fix before CLI (Architectural Blockers)

12. **R-E1 (null \_indexManager)** — Any code path that accesses `IndexManager` before `RegisterDefaultHandlers()` crashes. Document the lifecycle or use `Lazy<IndexManager>`.
13. **R-E2 (package name validation)** — Empty or filesystem-illegal package names crash `WriteRegistry` and `RemoveRegistry`. Validate at the CLI entry points.
14. **Package name case sensitivity** — Not defined whether `"MyMod"` and `"mymod"` are the same package. On Linux, they'd be separate (different shard directory). On Windows, they'd collide. This must be settled before the `install` command is implemented.
15. **DES-P1 (FetchSourceAsync splitting)** — The `build` pipeline's first real step is too monolithic. Splitting into `FetchHttpAsync`, `FetchGitAsync`, `FetchLocalAsync` makes the pipeline testable and the CLI progress reporting granular.

#### Nice-to-have before CLI

16. Add missing test coverage for the gap areas listed above.
17. Make `VersionIdentifier` immutable or a `record`.
18. Resolve `PackageMeta` collection property null-acceptance.
19. Add timeouts to git/shell process invocations.
20. Refactor multi-responsibility methods (`CompressPackageAsync`, `CreateBuilder`, `LoadRecipeAsync`).

### Recommended Sequence

```
Phase 1: Fix critical bugs (C1, C2, E1, E2, E3, P1, P2)           → ~2-3 days
Phase 2: DRY refactor (IndexHandler base class consolidation)       → ~1 day
Phase 3: Fix medium bugs and security issues (P3, P4, SEC-1, SEC-2) → ~1-2 days
Phase 4: Add missing validation and error handling                   → ~1 day
Phase 5: Add missing test coverage for gap areas                     → ~2-3 days
Phase 6: Implement CLI commands (starting with `init`, `install`, `list`)
```

### What CAN Be Done in Parallel with CLI

The `help`, `version`, and `list` commands (read-only, no state mutation) have the fewest dependencies and could be implemented early to provide feedback on the CLI architecture. However, even `list` requires the index subsystem (specifically `PackageListHandler`), which should be refactored first.

---

## Summary of All Issues

| Fixed? | #       | File                      | Line(s)    | Issue                                                      | Severity |
| ------ | ------- | ------------------------- | ---------- | ---------------------------------------------------------- | -------- |
| ✅     | BUG-C1  | VersionIdentifier.cs      | 158-172    | GetHashCode violates Equals contract                       | Critical |
| ✅     | BUG-C2  | PackageMeta.cs            | 143-174    | Missing conflict detection for non-SemVer virtual versions | Critical |
|        | BUG-E1  | PacmineEnvironment.cs     | 154-165    | Lock leak in Access() on handler error                     | Critical |
|        | BUG-E2  | PacmineEnvironment.cs     | 200-220    | TOCTOU race in Create()                                    | Critical |
|        | BUG-E3  | ManagedFileListHandler.cs | 78-82      | Non-deterministic file ownership                           | Critical |
|        | BUG-P2  | PackageMetaLuaObject.cs   | 113        | Groups setter reads Key instead of Value                   | Critical |
|        | BUG-P3  | PackageBuilder.cs         | 293-300    | HashAlgorithm instances not disposed                       | High     |
|        | BUG-P4  | PackageBuilder.cs         | 305        | Culture-sensitive hex comparison                           | High     |
|        | BUG-C3  | VersionIdentifier.cs      | 27         | Null constructor argument crashes                          | Medium   |
|        | BUG-C4  | PackageMeta.cs            | 122, 140   | No null guards on IsNewerThan/IsConflictingWith            | Medium   |
|        | BUG-C5  | PackageMeta.cs            | 41         | Version setter accepts null                                | Medium   |
|        | BUG-C6  | PackageParser.cs          | 23, 39     | No null guard on archive parameter                         | Medium   |
|        | BUG-E4  | PacmineEnvironment.cs     | 210-219    | Dual IndexManager creation during Create()                 | Medium   |
|        | BUG-E5  | PacmineEnvironment.cs     | 474        | Null-forgiving masks deserialization failures              | Medium   |
|        | BUG-E6  | PacmineEnvironment.cs     | 389-426    | Duplicate uninstall deny reasons                           | Medium   |
|        | BUG-P5  | PackageBuilder.cs         | 401-404    | CleanUpAsync is sync but returns Task                      | Medium   |
|        | BUG-P6  | PackageBuilder.cs         | 167-171    | Download filename race condition                           | Medium   |
|        | BUG-P7  | PackageBuilder.cs         | 249        | No timeout on git/shell processes                          | Medium   |
|        | BUG-P8  | PackageBuilder.cs         | 304        | Entire file loaded into memory for hashing                 | Medium   |
|        | BUG-P9  | PackageBuilder.cs         | 391-394    | Partial failure leaves corrupt state                       | Medium   |
|        | BUG-P10 | PackageBuilder.cs         | 161, 285   | No bounds check on source index                            | Medium   |
|        | DRY-E   | 5 handlers                | ~110 lines | Duplicated OnLoad/Content setter/Rebuild diff              | High     |
|        | DRY-P   | Multiple files            | ~100 lines | Duplicated Lua patterns, process execution                 | Medium   |
|        | SEC-1   | RestrictedFilesysLib      | 30-31      | Path check allows directory root operations                | Medium   |
|        | SEC-2   | RestrictedFilesysLib      | 28-29      | Case-sensitive path check on Windows                       | Medium   |
