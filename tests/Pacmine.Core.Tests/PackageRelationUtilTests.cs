using Xunit;
using Pacmine.Core;

namespace Pacmine.Core.Tests;

public class PackageRelationUtilTests
{
    // ── CheckSet ──────────────────────────────────────────────────────────

    [Fact]
    public void CheckSet_DuplicateNames_ReturnsDuplicateReason()
    {
        var packages = new[]
        {
            CreateMeta("pkg-a", "1.0.0"),
            CreateMeta("pkg-a", "2.0.0")
        };

        var reasons = PackageRelationUtil.CheckSet(packages);

        Assert.Single(reasons);
        Assert.IsType<DuplicateNameInvalidReason>(reasons[0]);
        Assert.Equal("pkg-a", reasons[0].TargetPackageName);
    }

    [Fact]
    public void CheckSet_DirectConflict_ReturnsConflictReason()
    {
        var packages = new[]
        {
            CreateMeta("pkg-a", "1.0.0", conflicts: new() { { "pkg-b", new VersionRange("^1.0.0") } }),
            CreateMeta("pkg-b", "1.5.0")
        };

        var reasons = PackageRelationUtil.CheckSet(packages);

        Assert.Single(reasons);
        Assert.IsType<ConflictInvalidReason>(reasons[0]);
        var cr = (ConflictInvalidReason)reasons[0];
        Assert.Contains(cr.TargetPackageName, new[] { "pkg-a", "pkg-b" });
        Assert.Contains(cr.ConflictingPackageName, new[] { "pkg-a", "pkg-b" });
    }

    [Fact]
    public void CheckSet_VirtualPackageConflict_ReturnsConflictReason()
    {
        var packages = new[]
        {
            CreateMeta("provider", "1.0.0",
                provides: new() { { "virtual-lib", new VersionIdentifier("2.0.0") } }),
            CreateMeta("consumer", "1.0.0",
                conflicts: new() { { "virtual-lib", new VersionRange("^2.0.0") } })
        };

        var reasons = PackageRelationUtil.CheckSet(packages);

        Assert.Single(reasons);
        Assert.IsType<ConflictInvalidReason>(reasons[0]);
    }

    [Fact]
    public void CheckSet_MissingDependency_ReturnsMissingDepReason()
    {
        var packages = new[]
        {
            CreateMeta("consumer", "1.0.0",
                depends: new() { { "missing-dep", new VersionRange("^1.0.0") } })
        };

        var reasons = PackageRelationUtil.CheckSet(packages);

        Assert.Single(reasons);
        Assert.IsType<MissingDependsInvalidReason>(reasons[0]);
        var mr = (MissingDependsInvalidReason)reasons[0];
        Assert.Equal("consumer", mr.TargetPackageName);
        Assert.Equal("missing-dep", mr.MissingDependName);
    }

    [Fact]
    public void CheckSet_DependencyViaVirtualPackage_ReturnsEmpty()
    {
        var packages = new[]
        {
            CreateMeta("provider", "2.0.0",
                provides: new() { { "virtual-lib", new VersionIdentifier("1.0.0") } }),
            CreateMeta("consumer", "1.0.0",
                depends: new() { { "virtual-lib", new VersionRange("^1.0.0") } })
        };

        var reasons = PackageRelationUtil.CheckSet(packages);

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckSet_Replacement_ReturnsReplacedReason()
    {
        var packages = new[]
        {
            CreateMeta("replacer", "2.0.0",
                replaces: new() { { "replaced-pkg", new VersionRange("*") } }),
            CreateMeta("replaced-pkg", "1.0.0")
        };

        var reasons = PackageRelationUtil.CheckSet(packages);

        Assert.Single(reasons);
        Assert.IsType<PackageReplacedInvalidReason>(reasons[0]);
        var rr = (PackageReplacedInvalidReason)reasons[0];
        Assert.Equal("replacer", rr.TargetPackageName);
        Assert.Equal("replaced-pkg", rr.ReplacedPackageName);
    }

    [Fact]
    public void CheckSet_ValidSet_ReturnsEmpty()
    {
        var packages = new[]
        {
            CreateMeta("pkg-a", "1.0.0"),
            CreateMeta("pkg-b", "2.0.0"),
            CreateMeta("pkg-c", "3.0.0",
                depends: new() { { "pkg-a", new VersionRange("^1.0.0") } })
        };

        var reasons = PackageRelationUtil.CheckSet(packages);

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckSet_MultipleIssues_ReturnsAllReasons()
    {
        var packages = new[]
        {
            CreateMeta("pkg-a", "1.0.0"),
            CreateMeta("pkg-a", "2.0.0"), // duplicate
            CreateMeta("pkg-b", "1.0.0",
                conflicts: new() { { "pkg-c", new VersionRange("^1.0.0") } }),
            CreateMeta("pkg-c", "1.5.0") // conflicts with pkg-b
        };

        var reasons = PackageRelationUtil.CheckSet(packages);

        Assert.Equal(2, reasons.Length);
        Assert.Contains(reasons, r => r is DuplicateNameInvalidReason);
        Assert.Contains(reasons, r => r is ConflictInvalidReason);
    }

    // ── CheckAdd ──────────────────────────────────────────────────────────

    [Fact]
    public void CheckAdd_DuplicateWithBase_ReturnsDuplicateReason()
    {
        var @base = new[] { CreateMeta("existing", "1.0.0") };
        var add = new[] { CreateMeta("existing", "2.0.0") };

        var reasons = PackageRelationUtil.CheckAdd(@base, add);

        Assert.Single(reasons);
        Assert.IsType<DuplicateNameInvalidReason>(reasons[0]);
    }

    [Fact]
    public void CheckAdd_ConflictWithBase_ReturnsConflictReason()
    {
        var @base = new[] { CreateMeta("base-pkg", "1.0.0") };
        var add = new[] { CreateMeta("new-pkg", "1.0.0",
            conflicts: new() { { "base-pkg", new VersionRange("^1.0.0") } }) };

        var reasons = PackageRelationUtil.CheckAdd(@base, add);

        Assert.Single(reasons);
        Assert.IsType<ConflictInvalidReason>(reasons[0]);
    }

    [Fact]
    public void CheckAdd_MissingDependency_ReturnsMissingDepReason()
    {
        var @base = Array.Empty<PackageMeta>();
        var add = new[] { CreateMeta("new-pkg", "1.0.0",
            depends: new() { { "missing-dep", new VersionRange("^1.0.0") } }) };

        var reasons = PackageRelationUtil.CheckAdd(@base, add);

        Assert.Single(reasons);
        Assert.IsType<MissingDependsInvalidReason>(reasons[0]);
    }

    [Fact]
    public void CheckAdd_SatisfiedDependencyFromBase_ReturnsEmpty()
    {
        var @base = new[] { CreateMeta("provider", "2.0.0",
            provides: new() { { "virtual-lib", new VersionIdentifier("1.5.0") } }) };
        var add = new[] { CreateMeta("consumer", "1.0.0",
            depends: new() { { "virtual-lib", new VersionRange("^1.0.0") } }) };

        var reasons = PackageRelationUtil.CheckAdd(@base, add);

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckAdd_ValidAddition_ReturnsEmpty()
    {
        var @base = new[] { CreateMeta("base-pkg", "1.0.0") };
        var add = new[] { CreateMeta("new-pkg", "2.0.0") };

        var reasons = PackageRelationUtil.CheckAdd(@base, add);

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckAdd_ReplaceFromBase_ReturnsReplacedReason()
    {
        var @base = new[] { CreateMeta("replacer", "2.0.0") };
        var add = new[] { CreateMeta("replaced-pkg", "1.0.0") };

        // The base package 'replacer' replaces 'replaced-pkg' — but 'replaced-pkg'
        // doesn't declare any replaces. The replacer is already in base.
        // Let's test: base has replacer with replaces, add has the target.
        var @base2 = new[] { CreateMeta("replacer", "2.0.0",
            replaces: new() { { "replaced-pkg", new VersionRange("*") } }) };
        var add2 = new[] { CreateMeta("replaced-pkg", "1.0.0") };

        var reasons = PackageRelationUtil.CheckAdd(@base2, add2);

        Assert.Single(reasons);
        Assert.IsType<PackageReplacedInvalidReason>(reasons[0]);
    }

    // ── CheckRemove ───────────────────────────────────────────────────────

    [Fact]
    public void CheckRemove_BreaksDependency_ReturnsMissingDepReason()
    {
        var @base = new[]
        {
            CreateMeta("dependency", "1.0.0"),
            CreateMeta("dependent", "1.0.0",
                depends: new() { { "dependency", new VersionRange("^1.0.0") } })
        };

        var reasons = PackageRelationUtil.CheckRemove(@base, ["dependency"]);

        Assert.Single(reasons);
        Assert.IsType<MissingDependsInvalidReason>(reasons[0]);
        var mr = (MissingDependsInvalidReason)reasons[0];
        Assert.Equal("dependent", mr.TargetPackageName);
        Assert.Equal("dependency", mr.MissingDependName);
    }

    [Fact]
    public void CheckRemove_NonExistentPackage_ThrowsArgumentException()
    {
        var @base = new[] { CreateMeta("existing", "1.0.0") };

        Assert.Throws<ArgumentException>(() =>
            PackageRelationUtil.CheckRemove(@base, ["nonexistent"]));
    }

    [Fact]
    public void CheckRemove_LeafPackage_ReturnsEmpty()
    {
        var @base = new[]
        {
            CreateMeta("dependency", "1.0.0"),
            CreateMeta("leaf", "2.0.0") // nothing depends on it
        };

        var reasons = PackageRelationUtil.CheckRemove(@base, ["leaf"]);

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckRemove_DependencyViaVirtualPackage_Breaks()
    {
        var @base = new[]
        {
            CreateMeta("provider", "2.0.0",
                provides: new() { { "virtual-lib", new VersionIdentifier("1.0.0") } }),
            CreateMeta("consumer", "1.0.0",
                depends: new() { { "virtual-lib", new VersionRange("^1.0.0") } })
        };

        var reasons = PackageRelationUtil.CheckRemove(@base, ["provider"]);

        Assert.Single(reasons);
        Assert.IsType<MissingDependsInvalidReason>(reasons[0]);
    }

    [Fact]
    public void CheckRemove_OnlyUnusedPackage_ReturnsEmpty()
    {
        var @base = new[]
        {
            CreateMeta("unused-dep", "1.0.0"),
            CreateMeta("dep-b", "2.0.0"),
            CreateMeta("dependent", "1.0.0",
                depends: new()
                {
                    { "dep-b", new VersionRange("^2.0.0") }
                })
        };

        // Remove unused-dep — nothing depends on it
        var reasons = PackageRelationUtil.CheckRemove(@base, ["unused-dep"]);

        Assert.Empty(reasons);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static PackageMeta CreateMeta(
        string name,
        string version,
        Dictionary<string, VersionRange>? depends = null,
        Dictionary<string, VersionRange>? conflicts = null,
        Dictionary<string, VersionIdentifier>? provides = null,
        Dictionary<string, VersionRange>? replaces = null)
    {
        return new PackageMeta
        {
            Name = name,
            Version = new VersionIdentifier(version),
            Depends = depends ?? [],
            Conflicts = conflicts ?? [],
            Provides = provides ?? [],
            Replaces = replaces ?? []
        };
    }
}
