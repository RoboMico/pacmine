# Pacmine.Core

Core data models and version management for the Pacmine package ecosystem. This library defines the foundational types used by all other modules — package metadata, semantic versioning primitives, version constraint expressions, and archive parsing.

## Key Concepts

### Version Identifier

`VersionIdentifier` wraps a version string with SemVer 2.0 parsing and comparison semantics. It accepts a leading `v` and makes the patch segment optional, so `"1.12"`, `"v2.3.4"`, and `"v6.0"` are all valid.

| Property | Type | Description |
|---|---|---|
| `RawString` | `string` | The original version string |
| `SemVersion` | `SemVersion?` | Parsed SemVer object; `null` for non-standard strings |
| `Segments` | `string[]` | For SemVer: `[Major, Minor, Patch, ...Prerelease]`; otherwise `[raw]` |

Equality is performed by exact raw-string comparison. For semantic equivalence (e.g. `"1.0.0"` and `"v1.0.0"`), use `IsEquivalentTo()`. The class implements `IComparable<VersionIdentifier>` and overloads `==`, `!=`, `<`, `>`, `<=`, `>=`.

### Version Range

`VersionRange` represents a version constraint expression operating in one of three modes:

| Mode | Trigger | Behavior |
|---|---|---|
| **Any** | `"*"` | Matches any version |
| **SemVer compatible** | Valid npm-style range (`^`, `~`, `>=`, `>`, `<=`, `<`, exact, `x`, hyphen, `\|\|`) | Evaluated using `SemVersionRange` with standard npm prerelease exclusion rules |
| **Literal match** | None of the above | Exact ordinal `==` against `VersionIdentifier.RawString` |

When using SemVer mode, prerelease versions (e.g. `1.0.0-alpha`) are excluded unless the range expression itself contains a prerelease identifier. For example, `^1.0.0` matches `1.2.3` but not `1.2.3-beta`; `^1.0.0-alpha` matches both.

### Package Meta

`PackageMeta` is the manifest for a Pacmine package. It defines identity, version, and relationships to other packages.

```text
PackageMeta
├─ Name            (string, required)    — Must match ^[a-z0-9][a-z0-9\-_\.]*$
├─ Version         (VersionIdentifier)   — Package version
├─ Epoch           (int, default: 0)     — Resets comparison (higher always wins)
├─ Release         (int, default: 1)     — Increment for same-upstream rebuilds
├─ Description     (string)
├─ UpstreamUrl     (string)              — Project homepage or repository
├─ Category        (string)              — mod, resourcepack, shaderpack, etc.
├─ License         (string)
├─ Groups          (List<string>)        — Groups this package belongs to
├─ Provides        (Dict<string, VersionIdentifier>) — Virtual packages with their own versions
├─ Depends         (Dict<string, VersionRange>)      — Required dependencies
├─ Conflicts       (Dict<string, VersionRange>)      — Incompatible packages
├─ Replaces        (Dict<string, VersionRange>)      — Packages this supersedes
└─ Recommends      (Dict<string, string>)            — Optional recommendations
```

Key methods:
- `GetFullVersionString()` — returns `"epoch:version-release"` (epoch omitted if zero)
- `IsNewerThan(PackageMeta)` — compares by epoch, version, then release
- `IsConflictingWith(PackageMeta)` — bidirectional conflict check covering both direct names and virtual packages

### Package Parser

`PackageParser` is a static utility for reading metadata from a ZIP package archive. Pacmine packages are standard ZIP files containing a `.PACMINE.META.json` entry with serialized `PackageMeta`.

## Usage

### Create and compare version identifiers

```csharp
var v1 = new VersionIdentifier("1.12.2");
var v2 = new VersionIdentifier("1.20.1");
var v3 = new VersionIdentifier("v2.0.0");

// Comparison operators
bool isNewer = v2 > v1;          // true

// Let SemVer decide precedence
int cmp = v1.CompareTo(v2);      // -1 (v1 is older)

// Semantic equivalence (ignores leading "v")
bool equiv = v2.IsEquivalentTo(new VersionIdentifier("v1.20.1"));  // true

// Strict equality (raw-string match)
bool exact = v2.Equals(new VersionIdentifier("v1.20.1"));          // false
```

### Check version ranges

```csharp
var caret = new VersionRange("^1.12");
var tilde = new VersionRange("~1.12.2");
var compound = new VersionRange(">=1.16.0 <1.17 || >=1.18.0");
var wildcard = VersionRange.Any;         // matches anything
var literal = new VersionRange("2.0");   // exact match against "2.0"

caret.Contains(new VersionIdentifier("1.20.1"));      // true
caret.Contains(new VersionIdentifier("v1.20.1"));     // true
caret.Contains(new VersionIdentifier("2.0.0"));       // false

// Prerelease exclusion (npm standard)
var range = new VersionRange("^1.0.0");
range.Contains(new VersionIdentifier("1.2.3"));       // true
range.Contains(new VersionIdentifier("1.2.3-beta"));  // false — excluded
```

### Define package metadata

```csharp
var meta = new PackageMeta
{
    Name = "sodium-mc26.1-fabric",
    Version = new VersionIdentifier("0.8.9"),
    Description = "The fastest and most compatible rendering optimization mod for Minecraft.",
    Category = "mod",
    License = "Polyform-Shield-1.0.0",
    UpstreamUrl = "https://github.com/CaffeineMC/sodium",
    Depends = new Dictionary<string, VersionRange>
    {
        ["minecraft"] = new VersionRange("~26.1")
    },
    Conflicts = new Dictionary<string, VersionRange>
    {
        ["optifabric"] = new VersionRange("*")
    },
    Provides = new Dictionary<string, VersionIdentifier>
    {
        ["sodium"] = new VersionIdentifier("0.8.9")
        ["indium"] = new VersionIdentifier("0.8.9")
    },
    Recommends = new Dictionary<string, string>
    {
        ["sodium-extra"] = "For extra settings and features"
    }
};
```

### Check conflicts between packages

```csharp
var optifabricMeta = new PackageMeta
{
    Name = "optifabric-mc26.1-fabric",
    Version = new VersionIdentifier("HD_U_M9"),
    Conflicts = new Dictionary<string, VersionRange>
    {
        ["sodium"] = VersionRange.Any
    },
    Provides = new Dictionary<string, VersionIdentifier>
    {
        ["optifabric"] = new VersionIdentifier("HD_U_M9")
    },
};

bool conflict = meta.IsConflictingWith(optifineMeta);  // true (bidirectional)
```

### Compare packages

```csharp
var oldMeta = new PackageMeta
{
    Name = "pack",
    Version = new VersionIdentifier("0.5.8"),
    Epoch = 0,
    Release = 1
};

var newMeta = new PackageMeta
{
    Name = "pack",
    Version = new VersionIdentifier("0.1.0"),
    Epoch = 1,  // epoch bump forces this to be newer
    Release = 1
};

bool isNewer = newMeta.IsNewerThan(oldMeta);  // true (epoch wins)
```

### Parse a package archive

```csharp
using var zip = ZipFile.OpenRead("sodium-mc26.1-fabric.pacminepack.zip");

PackageMeta? meta = PackageParser.GetMeta(zip);
DateTime? packagedAt = PackageParser.GetPackagedTime(zip);

if (meta is not null)
{
    Console.WriteLine($"{meta.Name} v{meta.Version}");
}
```

### Custom JSON serialization

`VersionIdentifier` and `VersionRange` serialize as plain JSON strings without extra configuration:

```csharp
using System.Text.Json;

var meta = new PackageMeta
{
    Name = "fabric-api",
    Version = new VersionIdentifier("0.102.0"),
    Depends = new Dictionary<string, VersionRange>
    {
        ["minecraft"] = new VersionRange(">=1.20"),
        ["fabricloader"] = new VersionRange("^0.16")
    },
    Provides = new Dictionary<string, VersionIdentifier>
    {
        ["api"] = new VersionIdentifier("1.0")
    }
};

// Serialize — VersionIdentifier and VersionRange become plain strings
string json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });

// Deserialize — strings are automatically converted back
PackageMeta? restored = JsonSerializer.Deserialize<PackageMeta>(json);
```

<details>
<summary>Example JSON output</summary>

```json
{
  "Name": "fabric-api",
  "Version": "0.102.0",
  "Description": "No description",
  "UpstreamUrl": "N/A",
  "Category": "",
  "License": "N/A",
  "Release": 1,
  "Epoch": 0,
  "Groups": [],
  "Provides": { "api": "1.0" },
  "Depends": { "minecraft": ">=1.20", "fabricloader": "^0.16" },
  "Conflicts": {},
  "Replaces": {},
  "Recommends": {}
}
```
</details>

## Dependencies

- [semver](https://github.com/WalkerCodeRanger/semver) (NuGet v3.0.0) — SemVer 2.0 parsing and range operations
- `System.Text.Json` (built into .NET 10)

## Class Reference

For detailed documentation for all members in `Pacmine.Core` namespace, check the [API reference page](/api/Pacmine.Core.html).
