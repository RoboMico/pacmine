# Pacmine Code Review Report

**Date:** 2026-05-21
**Scope:** All modules excluding `Pacmine.Console`
**Modules Reviewed:** Pacmine.Core, Pacmine.Environment, Pacmine.PackageCraft (source + tests)

---

## Executive Summary

The codebase is well-structured with clean separation of concerns across three modules: **Core** (domain models, parsing, versioning), **Environment** (filesystem layout, package registry, index management), and **PackageCraft** (recipe-based build pipeline with Lua scripting). The architecture follows a logical dependency chain (Core → Environment / PackageCraft) with no circular dependencies.

**Overall assessment:** The foundation is solid. The domain models are well thought out and the handler pattern in Environment is appropriately chosen. However, the implementation has several correctness bugs, missing test coverage in critical pipeline paths, and a pervasive lack of sealing on public classes.

---

## Cross-Cutting Themes

### 1. No Public Classes Are Sealed

Across all three modules, **zero** public concrete classes are sealed. Key types like `PackageMeta`, `VersionIdentifier`, `PacmineEnvironment`, `PackageBuilder`, and all handler classes should be sealed:
- Value-semantic types (`VersionIdentifier`, `VersionRange`) risk broken equality/hashing contracts through subclassing.
- Classes with locking/disposal protocols (`PacmineEnvironment`) risk resource leaks.
- All handlers and builders use composition, not inheritance, as their extension mechanism.

### 2. Defensive Coding Gaps

Several methods accept (non-nullable-by-type) parameters without null guards, rely on `null!` suppression for defacto-initialization, and have index operations without bounds checking. While C# nullability analysis catches many cases at compile time, reflection/deserialization/textual JSON can inject nulls.

### 3. Inconsistent Test Coverage

Happy-path test coverage is good. But across all modules, core pipeline methods are untested:
- `Pacmine.Environment`: `UpdateFiles`, `Repair`, `Destroy`, `RemovePackageFiles` — **zero coverage**
- `Pacmine.PackageCraft`: `FetchSourceAsync`, `VerifySourceAsync`, `CompressPackageAsync`, all Lua invocation methods — **zero coverage**

---

## Module-by-Module Summary

### Pacmine.Core

**Design rating:** Strong. `VersionIdentifier` with dual SemVer/raw-string modes is a pragmatic solution for Minecraft's non-SemVer versions. `VersionRange` auto-detection of three modes (SemVer/Any/Literal) from a single constructor is elegant.

**Critical Issues:**

| # | Severity | Location | Issue |
|---|----------|----------|-------|
| ~~C1~~ | Fixed | `VersionIdentifier.cs:73,151` | `CompareTo` can return `0` while `Equals` returns `false` — violates `IComparable<T>` contract. Will cause incorrect behavior in `SortedSet<T>` / `SortedDictionary<TKey,TValue>`. |
| ~~C2~~ | Fixed | `PackageParser.cs:21,28` | XML doc states `GetMeta` returns `null` for corrupt JSON, but it actually throws `JsonException`. |
| ~~C3~~ | Fixed | `VersionRange.cs:40` | Constructor hardcodes `includeAllPrerelease: false` — this major semantic behavior is undocumented. |
| ~~C4~~ | Ignored | `PackageMeta.cs:46,51,56,61` | Inconsistent sentinel default strings: `"No description"`, `"N/A"`, `""`. Should be consistent (empty string or null) in the domain model. |
| ~~C5~~ | Fixed | `VersionIdentifier.cs:48` | `Segments` getter allocates a new `List<string>` per call. Should be cached. |

**Test Gaps:**
- No test for prerelease exclusion in SemVer range matching
- No full `PackageMeta` JSON roundtrip test
- Missing edge cases: empty-string version, whitespace in version, operator overloads with `null`

**Verdict:** Core domain model is sound but has a real `IComparable` contract bug and a misleading XML doc. Tests cover basics but miss the prerelease dimension.

---

### Pacmine.Environment

**Design rating:** Good, with caveats. The handler/manager pattern for index management is clean and extensible. The two factory methods (`Create`/`Access`) enforce proper lifecycle.

**Critical Issues:**

| # | Severity | Location | Issue |
|---|----------|----------|-------|
| E1 | **HIGH** | All handlers' `OnWriteRegistry` | In-memory state is mutated **before** `File.WriteAllText`. If the write fails, in-memory and on-disk state diverge. Affects all 5 handlers. |
| E2 | **MEDIUM** | `PacmineEnvironment.cs:488-494` | `RemoveRegistry` skips `OnRemoveRegistry` when registry JSON is corrupt but file exists — leaves stale index entries. |
| E3 | **MEDIUM** | `PacmineEnvironment.cs:458` | No guard on empty package name — `Name[0]` throws `IndexOutOfRangeException`. |
| E4 | **MEDIUM** | `PacmineEnvironment.cs:251-253` | Missing required handlers silently produces empty dictionaries, changing behavior without error. |
| E5 | **LOW** | `PacmineEnvironment.cs:82-97` | Lock detection catches all `IOException` types, not just sharing violations. Disk-full or permission errors produce misleading "locked" message. |
| E6 | **LOW** | `IndexManager.cs:12` | Handler list has no synchronization — not thread-safe, but this isn't documented. |

**Test Gaps:**
- `UpdateFiles`, `Repair`, `Destroy`, `RemovePackageFiles`, `GetLockerPid` — completely untested
- No integration test for `IndexManager.Rebuild` with actual disk files
- Minimal `OnLoad` tests across handlers (only `PackageListHandler` tested)
- `CheckAcceptance` / `CheckCanUninstall` missing virtual-package interaction edge cases

**Verdict:** The handler architecture is well-designed, but the on-disk state mutation order is a systemic bug pattern. Critical pipeline methods have no test coverage.

---

### Pacmine.PackageCraft

**Design rating:** Good. The recipe → builder → Lua pipeline is coherent. Source fetcher strategy pattern and filesys library restrictions are appropriate.

**Critical Issues:**

| # | Severity | Location | Issue |
|---|----------|----------|-------|
| ~~P1~~ | Invalid | `SourceFetchers.cs:221` | `git clone --revision <refspec>` — `--revision` is not a valid git flag. Git source fetching is broken. |
| ~~P2~~ | Fixed | `RestrictedFilesysLuaLibrary.cs:90`, `UnsafeFilesysLuaLibrary.cs:42` | `File.Copy` without `overwrite: true`. Existing destination files cause `IOException` — build pipeline will fail on re-runs. |
| ~~P3~~ | Works as Intended | `SourceFetchers.cs:72-73` | `LocalFileSourceFetcher` uses `Path.Combine` without normalization — vulnerable to path traversal (`../` segments escape the working directory). |
| P4 | **MEDIUM** | `PackageMetaLuaObject.cs:172`, `PackageCraftRecipeLuaObject.cs:123` | `FromLuaTable` calls `.Read<T>()` on every field without checking for `LuaValueType.Nil` — absent optional fields cause runtime exceptions. |
| P5 | **MEDIUM** | `PackageBuilder.cs:151,191,196` | No bounds checking on `index` parameter in `FetchSourceAsync` and `VerifySourceAsync`. |
| P6 | **MEDIUM** | `PackageBuilder.cs:127-136` | `OutputDirectory` is never created before `CompressPackageAsync` writes to it. |
| P7 | **LOW** | `SourceFetchers.cs:114` | `DownloadService` (IDisposable) never disposed in `RemoteSourceFetcher`. |
| P8 | **LOW** | `PackageBuilder.cs:129,146,294` | Dead null checks on non-nullable-typed properties — misleading code. |

**Test Gaps:**
- `FetchSourceAsync`, `VerifySourceAsync`, `CompressPackageAsync` — untested
- All Lua invocation methods — untested
- Lua object conversion with missing optional fields — untested
- `PackageBuilderFactory` with `AllowArbitraryFileOperation` — minimal coverage
- Tests use reflection on internal members (fragile to renaming)

**Verdict:** The pipeline design is solid but has 3 HIGH-severity bugs (broken git fetch, no-overwrite copy, path traversal). Test coverage of the build pipeline is essentially absent.

---

## XML Doc Comment Quality Summary

| Module | Public Members Documented | Redundant "Gets/Sets" | Missing Docs | Accuracy Issues |
|--------|--------------------------|----------------------|--------------|-----------------|
| Core | All | 7 properties | None | 1 (GetMeta null claim) |
| Environment | All but 3 records | Few | `UninstallDenyReason` + sub-records | None critical |
| PackageCraft | All but 2 internal methods | None | `WriteStdout`, `WriteStderr` | Minor header errors |

**Overall:** Documentation coverage is good. The main issue is redundant "Gets or sets the X" summaries on obvious properties (especially `PackageMeta`) and the false claim in `PackageParser.GetMeta`. The `remarks` sections in Environment handlers (e.g., `DependsOnHandler`) are exemplary.

---

## Test Quality Summary

| Criteria | Core | Environment | PackageCraft |
|----------|------|-------------|--------------|
| Naming Convention | Consistent | Consistent | Consistent |
| Happy Path Coverage | Good | Good | Adequate |
| Edge Case Coverage | Fair | Fair | Minimal |
| Null/Invalid Input Tests | Partial | Partial | Near absent |
| Pipeline/Integration Tests | N/A | Missing | Missing |
| Reflection Usage | None | Clean test base | Heavy (fragile) |

---

## Prioritized Action Items

### Must-Fix (bugs that produce incorrect behavior)

1. **P1 — Broken git clone** (`SourceFetchers.cs:221`): Replace `--revision` with a checkout step.
2. **P3 — Path traversal** (`SourceFetchers.cs:72-73`): Normalize and validate paths in `LocalFileSourceFetcher`.
3. **P2 — No-overwrite File.Copy** (`RestrictedFilesysLuaLibrary.cs:90`, `UnsafeFilesysLuaLibrary.cs:42`): Add `overwrite: true`.
4. **C1 — IComparable contract violation** (`VersionIdentifier.cs`): Align `CompareTo` and `Equals` semantics.
5. **E1 — State mutation before disk write** (all handlers' `OnWriteRegistry`): Clone before mutating, then write, then swap.

### Should-Fix (design flaws and robustness gaps)

6. **P4 — Missing optional field handling** (Lua object conversions): Check for `LuaValueType.Nil` before reading.
7. **P5 — No bounds checking** (`PackageBuilder.cs`): Validate `index` parameter.
8. **E2 — Stale indexes after corrupt registry removal** (`PacmineEnvironment.cs`): Call `OnRemoveRegistry` before deletion.
9. **E3 — Empty name guard** (`PacmineEnvironment.cs`): Validate `Name` is non-empty.
10. **C2 — Misleading XML doc** (`PackageParser.cs`): Fix or implement the documented behavior.
11. **C3 — Undocumented prerelease exclusion** (`VersionRange.cs`): Add documentation.

### Consider (improvements and polish)

12. **Seal** all concrete public classes across the codebase.
13. **Add missing tests** for `UpdateFiles`, `Repair`, `Destroy`, `RemovePackageFiles`, `FetchSourceAsync`, `VerifySourceAsync`, `CompressPackageAsync`, and Lua invocation methods.
14. **Add test** for full `PackageMeta` JSON roundtrip.
15. **Fix** `Segments` getter allocation (`VersionIdentifier.cs`).
16. **Dispose** `DownloadService` in `RemoteSourceFetcher` (`SourceFetchers.cs`).
17. **Remove** dead null checks in `PackageBuilder`.
18. **Add** `sealed` to all handler classes, `PacmineEnvironment`, `IndexManager`, and all Core domain types.
19. **Add** `[InternalsVisibleTo]` for test projects to eliminate reflection-based test setup.
