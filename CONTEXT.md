# Pacmine Domain Context

This document defines the domain vocabulary for the Pacmine project. Use these terms consistently in code, documentation, and architecture discussions.

## Core Domain

### Package
A distributable unit of content (mod, resourcepack, shaderpack, etc.) identified by a name and version. Represented by [`PackageMeta`](src/Pacmine.Core/PackageMeta.cs).

### PackageMeta
The manifest describing a package's identity, version (with epoch/release), and relationships to other packages: depends, conflicts, replaces, provides, recommends.

### VersionIdentifier
A version string with SemVer 2.0 parsing semantics. Wraps `SemVersion` for comparison but falls back to raw string comparison for non-SemVer strings. See [`VersionIdentifier`](src/Pacmine.Core/VersionIdentifier.cs).

### VersionRange
A version constraint expression operating in three modes: Any (`*`), SemVer compatible (npm-style ranges), or literal match. See [`VersionRange`](src/Pacmine.Core/VersionRange.cs).

### Virtual Package
A package declared via the `Provides` field. A virtual package is satisfied by its providing package. For example, `sodium` provides `indium`, so anything depending on `indium` is satisfied by `sodium`.

### Environment Package
A package with `InstallReason.Environment` that tracks version info of game files not managed by Pacmine but required by other packages (e.g., Minecraft installation, mod loader). Environment packages contain no files and depend on no other packages.

## Environment Module

### Environment
A game instance directory managed by Pacmine, identified by a `.pacmine/` subdirectory. Represented by [`PacmineEnvironment`](src/Pacmine.Environment/PacmineEnvironment.cs).

### EnvironmentLock
A PID-based file lock that prevents concurrent modifications to an environment. Uses a `lock` file in `.pacmine/` containing the holding process's PID. One lock per environment.

### RegistryStore
The on-disk and in-memory store of installed package metadata. Each package gets a JSON file sharded by first character (e.g., `.pacmine/registry/s/sodium-mc26.1-fabric.json`). Provides atomic writes via tmp→target rename. Maintains a `package_list` plain-text index for fast loading.

### PackageRegistry
A registry entry for an installed package: its `PackageMeta`, file list with SHA-256 checksums, `InstallReason`, and timestamps. See [`PackageRegistry`](src/Pacmine.Environment/PackageRegistry.cs).

### FileManager
Handles filesystem operations within an environment: copying files from a source directory into the environment root, computing SHA-256 checksums, pruning stale files, removing package-owned files, and checking for file conflicts against managed and orphan files.

### InstallReasons
Why a package was installed: `Explicit` (user-requested), `AsDependency` (pulled in by another package), or `Environment` (tracking game files). See [`InstallReasons`](src/Pacmine.Environment/InstallReasons.cs).

## PackageCraft Module

### Recipe
A Lua script defining how to build a package: sources, checksums, metadata, and optional build-phase hooks (`prepare`, `get_version`, `build`, `check`, `package`). Represented by [`PackageCraftRecipe`](src/Pacmine.PackageCraft/PackageCraftRecipe.cs).

### Builder
Orchestrates the PackageCraft build pipeline: fetch sources → verify checksums → invoke Lua hooks → compress output. Represented by [`PackageBuilder`](src/Pacmine.PackageCraft/PackageBuilder.cs).

### BuilderFactory
Configures build permissions (filesys access, shell execution, Git) and creates configured `PackageBuilder` instances. Permissions are decided at configuration time — disabled features are never registered. See [`PackageBuilderFactory`](src/Pacmine.PackageCraft/PackageBuilderFactory.cs).

### SourceFetcher
Retrieves a source artifact (HTTP download, local file copy, or Git clone) into the source directory. Three implementations: `RemoteSourceFetcher`, `LocalFileSourceFetcher`, `GitSourceFetcher`. See [`SourceFetchers`](src/Pacmine.PackageCraft/SourceFetchers.cs).

## Console Module

### Command
A CLI operation exposed via `System.CommandLine`. Each command is a static class with a `Create()` factory method returning a `Command` instance. Commands follow a validate→plan→confirm→execute workflow.

## Architecture Terms

These terms are used when discussing code structure:

- **Module** — anything with an interface and an implementation (function, class, package, slice)
- **Interface** — everything a caller must know to use the module: types, invariants, error modes, ordering, config
- **Implementation** — the code inside
- **Depth** — leverage at the interface: a lot of behaviour behind a small interface
- **Seam** — where an interface lives; a place behaviour can be altered without editing in place
- **Adapter** — a concrete thing satisfying an interface at a seam
- **Leverage** — what callers get from depth
- **Locality** — what maintainers get from depth: change, bugs, knowledge concentrated in one place
