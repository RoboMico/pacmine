# Roadmap to v0.1.0 — First Functional Release

The goal for 0.1.0 is a working CLI that can init environments, build packages
from Lua recipes, install/uninstall packages with dependency resolution, and
manage installed packages.

## Current State (May 2026)

The core library and PackageCraft build pipeline are ~95% complete.
`Pacmine.Environment` has lock/lifecycle and basic persistence (`PackageList`,
`ManagedFiles`) but no registry management API. All 8 CLI commands have
`System.CommandLine` argument plumbing but every handler is a stub. No package
installation engine, no dependency resolver, no onboard wizard, no tests exist.

| Layer | % Done | Notes |
|---|---|---|
| Pacmine.Core | 100% | `VersionIdentifier`, `VersionRange`, `PackageMeta` — all complete with SemVer 2.0 + npm ranges via `semver` 3.0.0 |
| Pacmine.PackageCraft | 95% | Build pipeline complete (HTTP download, git clone, checksum verify, Lua phases, ZIP compression). Only `GlobalFunctions.Print()` and `GitCall()` are stubs. |
| Pacmine.Environment | 40% | Lock/unlock/create/destroy lifecycle done. `PackageList` and `ManagedFiles` persist to disk. `PackageRegistry` data model defined but no save/load. No registry management API. |
| Pacmine.Console (CLI) | 5% | 8 commands registered with argument/option plumbing. Every handler throws `NotImplementedException`. |
| Onboard Wizard | 0% | `RunAsync()` is a stub. |
| Tests | 0% | No test project. |

---

## M1 — Finish the Build Pipeline

Close the last remaining stubs in PackageCraft so the build pipeline is
100% complete.

- [ ] **`GlobalFunctions.Print()`** — forward the message to the builder's
  output (console write or an output callback). Currently an empty
  `// TODO: Implement` stub at `GlobalFunctions.cs:46`.

- [ ] **`GlobalFunctions.GitCall()`** — launch the `git` process (path stored in
  `PackageBuilder.GitCommand`) with the given arguments, capture stdout/stderr,
  return the exit code. Currently returns hardcoded `-1` at
  `GlobalFunctions.cs:61`.

---

## M2 — Registry Persistence

Packages installed to disk must be remembered across process restarts.
(`PackageList` and `ManagedFiles` already persist via `PacmineEnvironment`
properties — this milestone adds the per-package registry layer.)

- [ ] **PackageRegistry JSON serialization** — add `Save()` and `Load()` to
  `PackageRegistry.cs` using `System.Text.Json`. Write each entry to
  `{RegistryFolder}/{package-name}.json`. Verify `VersionIdentifier` and
  `VersionRange` round-trip correctly through their `ToString()` / constructor
  pattern.

- [ ] **Registry management API on PacmineEnvironment** — add methods:
  - `PackageRegistry? GetInstalledPackage(string name)`
  - `IEnumerable<PackageRegistry> GetAllInstalledPackages()`
  - `void AddInstalledPackage(PackageRegistry registry)`
  - `void RemoveInstalledPackage(string name)`
  Each method reads/writes the `{RegistryFolder}/{name}.json` file and keeps
  `PackageList` in sync.

- [ ] **Environment package config** — implement the `env` command's config
  store as a simple JSON key-value file at `{SpecialFolder}/env.conf` with
  `GetEnvPackage(string name)` and `SetEnvPackage(string name, string version)`
  methods on `PacmineEnvironment`. Used by the dependency resolver to satisfy
  packages that depend on Minecraft/Fabric/Java versions.

---

## M3 — Package Installation Engine

Core logic to install a `.pacminepack.zip` into a game instance: dependency
checking, file extraction, registry update.

- [ ] **Package archive reader** — new `PackageArchiveReader.cs` in
  `Pacmine.Environment`: open a `.pacminepack.zip`, extract the embedded
  `meta.json` into a `PackageMeta` object, enumerate files and their contents.
  Reuse the `ZipFile` API already used in `PackageBuilder.CompressPackageAsync`.

- [ ] **Dependency resolver** — new `DependencyResolver.cs` in
  `Pacmine.Environment`: given a candidate package's `Depends`, `Conflicts`,
  `Provides`, and `Replaces` dictionaries, verify against installed packages
  (from registry) AND env packages (from env.conf). Return a result with
  pass/fail and a human-readable reason string for each failure.

- [ ] **Package installer** — new `PackageInstaller.cs` in
  `Pacmine.Environment`: orchestrate the full flow:
  1. Open the archive and read metadata.
  2. Resolve dependencies via `DependencyResolver`.
  3. Extract files to the instance root.
  4. Compute SHA-256 checksums for each extracted file.
  5. Record files in `ManagedFiles` (using `ManagedFileRecord`).
  6. Write `PackageRegistry` entry via the registry API.
  7. Update `PackageList`.
  On failure, roll back extracted files and do not write registry.

- [ ] **Package uninstaller** — method on `PackageInstaller` or
  `PacmineEnvironment`: for each named package, remove its files (skip any file
  also owned by another package per `ManagedFiles`), remove its
  `ManagedFileRecord` entries, remove the registry entry, update `PackageList`.
  Refuse to uninstall packages still depended on by others unless `--force`.

- [ ] **Topological install ordering** — in the dependency resolver, sort
  packages so dependents are installed after their dependencies. Simple tree
  walk suffices for 0.1.0; a full SAT solver is not needed yet.

- [ ] **Transactional safety** — both install and uninstall must be atomic from
  the registry's perspective: if anything fails mid-operation, the registry
  and packlist are left unchanged, and any extracted files are cleaned up.

---

## M4 — CLI Command Implementations

Wire all 8 commands to the engine. The CLI becomes functional end-to-end.
Option names below match the current `System.CommandLine` definitions.

- [ ] **`init [dir]`** — `InitCommand.cs`: call `PacmineEnvironment.Create(dir)`.
  If `--skip-onboard` / `-s` is not set, run the onboard wizard (M5).
  Defaults `dir` to the shell's working directory.

- [ ] **`build <pathToLua>`** — `BuildCommand.cs`: invoke the full
  `PackageBuilder` pipeline:
  1. `CreateAsync(script)` to parse the Lua recipe.
  2. `ConfigureWorkingDirector(workingDirectory)`.
  3. `InitEnvironment()`.
  4. `FetchSourceAsync` + `VerifySourceAsync` for each source.
  5. `InvokePrepareAsync()` → `InvokeGetVersionAsync()` →
     `InvokeBuildAsync()` → `InvokeCheckAsync()` → `InvokePackageAsync()`.
  6. `CompressPackageAsync()` (unless `--install` / `-i`).
  7. `CleanUpAsync()` (unless `--no-clean` / `-n`).
  Support `--working-directory` / `-w`, `--install` / `-i`, and
  `--no-clean` / `-n`.

- [ ] **`install <pkgNameList>`** — `InstallCommand.cs`: resolve each package
  name to a `.pacminepack.zip` (local file path when `--local` / `-l` is set;
  remote repository comes later). Call the install engine for each.
  Support `--root` / `-r`.

- [ ] **`uninstall <pkgNameList>`** — `UninstallCommand.cs`: call the
  uninstall engine for each named package. Prompt for confirmation when
  removing packages that are dependencies of others (add `--force` option).
  Alias: `remove`.

- [ ] **`list`** — `ListCommand.cs`: enumerate installed packages from the
  registry. Default mode shows name and version; `--verbose` / `-v` also shows
  description, install reason, packaged/installed timestamps, and full version
  string including epoch and release.

- [ ] **`env set <name> <version>` / `env unset <name>`** — `EnvCommand.cs`:
  read/write env package entries via the env config API added in M2. These
  are separate runtime packages (Minecraft version, loader version, Java
  version) that regular packages can depend on but are not installed as files.

- [ ] **`repair [dir]`** — `RepairCommand.cs`: walk every installed package's
  `FileList` and `FileSHA256Sums`, verify each file on disk against its
  checksum and `ManagedFiles` record. Report missing/mismatched files.
  If `--print` / `-p` is NOT set, also reinstall affected packages.

- [ ] **`destroy [dir]`** — `DestroyCommand.cs`: uninstall all packages (if
  `--keep` / `-k` is set, skip file deletion), then call
  `PacmineEnvironment.Destroy()`. Prompt for confirmation unless `--force`
  (add this option).

---

## M5 — Onboard Wizard

New users should be able to set up a game instance interactively.

- [ ] **Implement `OnboardWizard.RunAsync()`** — `OnboardWizard.cs:17`:
  1. Prompt for instance path.
  2. Detect Minecraft version from existing launcher metadata (`.minecraft/`,
     Fabric `instance.json`, NeoForge configs).
  3. Prompt for loader type and version if not detected.
  4. Prompt for Java path/version.
  5. Write all detected values via the env config API (M2).
  6. Call `PacmineEnvironment.Create(path)` under the hood.
  Display the interactive flow described in the comment block at
  `Program.cs:43-98`.

- [ ] **Launcher metadata detection** — parse common launcher directory
  structures to auto-detect the Minecraft version and loader configuration,
  reducing manual input during onboarding.

---

## M6 — Polish & Release

Quality-of-life improvements, documentation, and the 0.1.0 tag.

- [ ] **User-friendly error messages** — replace raw `Exception` throws in
  command handlers with formatted console messages. Full stack traces should
  only appear when a `--debug` global option is passed.

- [ ] **`--help` coverage audit** — every command, subcommand, argument, and
  option must have a `Description` set in the `System.CommandLine` definition.
  Verify by running `pacmine --help`, `pacmine install --help`, etc.

- [ ] **README usage section** — add a "Getting Started" section to
  `README.md` and `README-zh.md` with example commands a new user would
  actually run: `pacmine init`, `pacmine build example/sodium.lua`,
  `pacmine install sodium`, `pacmine list`.

- [ ] **Example recipe smoke test** — add a CI script or manual test that runs
  `pacmine build example/sodium-mc26.1-fabric.lua --working-directory /tmp/pacmine-smoke`
  and asserts that the output `.pacminepack.zip` exists and is a valid ZIP.

- [ ] **Version bump** — set `<Version>0.1.0</Version>` in all four `.csproj`
  files (`Pacmine.Core`, `Pacmine.Environment`, `Pacmine.PackageCraft`,
  `Pacmine.Console`). Tag the release commit with `v0.1.0`.

---

## Out of Scope for 0.1.0

These features are explicitly deferred past the 0.1.0 release:

- Local package database of remote repositories
- Download from remote package repositories
- Package search and remote index
- Full SAT-based dependency resolution (tree-walk is sufficient for v0.1)
- Plugin system
- GUI or TUI frontend
- Package signing and signature verification
