# Roadmap to v0.1.0 — First Functional Release

The goal for 0.1.0 is a working CLI that can init environments, build packages
from Lua recipes, install/uninstall packages with dependency resolution, and
manage installed packages.

Current state: the core library and PackageCraft build pipeline are ~90%
complete. All 8 CLI commands have argument plumbing wired via
`System.CommandLine` but every handler throws `NotImplementedException`.

---

## M1 — Foundation Fixes

Eliminate known bugs in already-implemented code. The build pipeline must be
rock-solid before building anything on top of it.

- [x] **FilesysLuaLibrary sandbox logic**
- [x] **Recursive directory deletion**
- [x] **File handle leak**
- [x] **Cross-process lock safety**
- [ ] **Git source support in PackageBuilder** — `PackageBuilder.cs:224`: the
  `git://` branch silently does nothing, leaving `trackedSources[index]` null.
  Either implement clone + checkout, or throw `NotSupportedException` with a
  clear message.

---

## M2 — Registry Persistence

Packages installed to disk must be remembered across process restarts.

- [ ] **PackageRegistry JSON serialization** — add `Save()` and `Load()` to
  `PackageRegistry.cs` using `System.Text.Json`. Write each entry to
  `{RegistryFolder}/<packageNameInitialLetter>/{package-name}.json`. Ensure `VersionIdentifier` and
  `VersionRange` round-trip correctly through their `ToString()` / constructor
  pattern.

- [ ] **Registry management API on PacmineEnvironment** — add methods to
  `PacmineEnvironment.cs`:
  - `PackageRegistry? GetInstalledPackage(string name)`
  - `IEnumerable<PackageRegistry> GetAllInstalledPackages()`
  - `void AddInstalledPackage(PackageRegistry registry)`
  - `void RemoveInstalledPackage(string name)`

- [ ] **Packlist synchronization** — keep the `packlist` text file in sync with
  registry entries. The `PackageList` property already reads/writes the file;
  integrate it into the new registry methods.

---

## M3 — Package Installation Engine

Core logic to install a `.pacminepack.zip` into a game instance: dependency
checking, file extraction, registry update.

- [ ] **Package archive reader** — new `PackageArchiveReader.cs` in
  `Pacmine.Environment`: open a `.pacminepack.zip`, extract the embedded
  `meta.json`, enumerate files and their contents. Reuse the
  `ZipFile` API already used in `PackageBuilder.CompressPackageAsync`.

- [ ] **Dependency resolver** — new `DependencyResolver.cs` in
  `Pacmine.Environment`: given a candidate package's `Depends`, `Conflicts`,
  `Provides`, and `Replaces` dictionaries, verify against all installed
  packages. Return a result with pass/fail and a human-readable reason string
  for failures.

- [ ] **Package installer** — new `PackageInstaller.cs` in
  `Pacmine.Environment`: orchestrate the full flow — open archive → resolve
  dependencies → extract files to instance root → compute and record SHA-256
  checksums → write `PackageRegistry` entry → update packlist. On failure,
  roll back any extracted files before writing the registry.

- [ ] **Package uninstaller** — method on `PackageInstaller` or
  `PacmineEnvironment`: remove package files (skip any file also owned by
  another installed package), remove registry entry, update packlist. Refuse
  to uninstall packages that are still depended on by others unless
  `--force`.

- [ ] **Topological install ordering** — in the dependency resolver, sort
  packages so dependents are installed after their dependencies. Simple tree
  walk suffices for 0.1.0; a full SAT solver is not needed yet.

- [ ] **Transactional safety** — both install and uninstall must be atomic from
  the registry's perspective: if anything fails mid-operation, the registry
  and packlist are left unchanged, and any extracted files are cleaned up.

---

## M4 — CLI Command Implementations

Wire all 8 commands to the engine. The CLI becomes functional end-to-end.

- [ ] **`init`** — `InitCommand.cs`: call `PacmineEnvironment.Create(path)`.
  Support `--root` / `-r` to specify the instance directory.

- [ ] **`build`** — `BuildCommand.cs`: call `PackageBuilder.CreateAsync(script)`
  → `InitEnvironment()` → `FetchSourceAsync` for each source → `VerifySourceAsync`
  → `InvokePrepareAsync` → `InvokeGetVersionAsync` → `InvokeBuildAsync` →
  `InvokeCheckAsync` → `InvokePackageAsync` → `CompressPackageAsync` →
  `CleanUpAsync`. Support `--output` / `-o`, `--no-checksum`, and
  `--keep-workspace` flags.

- [ ] **`install`** — `InstallCommand.cs`: resolve package names to local
  `.pacminepack.zip` files (remote repository comes later), then call the
  install engine. Support `--root` / `-r` and `--reinstall`.

- [ ] **`uninstall`** — `UninstallCommand.cs`: call the uninstall engine for
  each named package. Prompt for confirmation when removing packages that are
  dependencies of others. Support `--force`.

- [ ] **`list`** — `ListCommand.cs`: enumerate installed packages from the
  registry. Default mode shows name and version; `--verbose` / `-v` also shows
  description, install reason, packaged/installed timestamps, and full version
  string including epoch and release.

- [ ] **`env`** — `EnvCommand.cs`: maintain a simple key-value config file at
  `{SpecialFolder}/env.conf`. `env set <name> <version>` writes an entry;
  `env unset <name>` removes one. Used by the onboard wizard and the `repair`
  command to know the target Minecraft/loader/Java versions.

- [ ] **`repair`** — `RepairCommand.cs`: walk every installed package's
  `FileList` and `FileSHA256Sums`, verify each file on disk. For any missing
  or mismatched file, report it and optionally reinstall that package (if
  `--reinstall` is set). Support `--root` / `-r`.

- [ ] **`destroy`** — `DestroyCommand.cs`: call `PacmineEnvironment.Destroy()`.
  Warn if packages are still installed; `--force` / `-f` skips the prompt.
  Support `--root` / `-r`.

---

## M5 — Onboard Wizard

New users should be able to set up a game instance interactively.

- [ ] **Implement `OnboardWizard.RunAsync()`** — `OnboardWizard.cs:17`:
  prompt for instance path → detect Minecraft version from existing launcher
  metadata (`.minecraft/`, Fabric's `instance.json`, NeoForge configs) →
  prompt for loader type and version if not detected → prompt for Java path →
  write all values via `env set` equivalents → call `init` under the hood.

- [ ] **Launcher metadata detection** — parse common launcher directory
  structures to auto-detect the Minecraft version, reducing manual input.

---

## M6 — Polish & Release

Quality-of-life improvements, documentation, and the 0.1.0 tag.

- [ ] **User-friendly error messages** — replace raw `Exception` throws in
  command handlers with formatted console messages. Full stack traces should
  only appear when a `--debug` flag is passed.

- [ ] **`--help` coverage audit** — every command, subcommand, argument, and
  option must have a `Description` set in the `System.CommandLine` definition.
  Verify by running `pacmine --help`, `pacmine install --help`, etc.

- [ ] **README usage section** — add a "Getting Started" section to
  `README.md` and `README-zh.md` with example commands that a new user would
  actually run: `pacmine init`, `pacmine build example/sodium.lua`,
  `pacmine install sodium`, `pacmine list`.

- [ ] **Example recipe smoke test** — add a CI script or manual test that runs
  `pacmine build example/sodium-mc26.1-fabric.lua --output /tmp/pacmine-smoke`
  and asserts that the output `.pacminepack.zip` exists and is a valid ZIP.

- [ ] **Version bump** — set `<Version>0.1.0</Version>` in all four `.csproj`
  files (`Pacmine.Core`, `Pacmine.Environment`, `Pacmine.PackageCraft`,
  `Pacmine.Console`). Tag the release commit with `v0.1.0`.

---

## Out of Scope for 0.1.0

These features are explicitly deferred past the 0.1.0 release:

- Local package database of remote repositories
- Download from remote package repository
- Package search and remote index
- Full SAT-based dependency resolution (tree-walk is sufficient for v0.1)
- Plugin system
- GUI or TUI frontend
- Package signing and signature verification
