namespace Pacmine.Core;

/// <summary>
/// Provides static utility methods for validating relationships within a set of packages,
/// such as detecting duplicates, conflicts, missing dependencies, and replacement violations.
/// Includes optimized variants for incremental add and remove operations.
/// </summary>
public static class PackageRelationUtil
{
    /// <summary>
    /// Performs a full validity check on a set of packages, detecting duplicate names,
    /// mutual conflicts, missing dependencies, and replacement violations.
    /// </summary>
    /// <param name="packages">The set of packages to validate.</param>
    /// <returns>An array of <see cref="InvalidReason"/> describing any issues found.</returns>
    public static InvalidReason[] CheckSet(PackageMeta[] packages)
    {
        List<InvalidReason> reasons = [];

        // duplicate
        HashSet<string> names = [];
        for (int i = 0; i < packages.Length; i++)
        {
            if (names.Contains(packages[i].Name))
            {
                reasons.Add(new DuplicateNameInvalidReason(packages[i].Name));
            }
            else
            {
                names.Add(packages[i].Name);
            }
        }

        // conflicts
        for (int i = 0; i < packages.Length; i++)
        {
            for (int j = i + 1; j < packages.Length; j++)
            {
                if (packages[i].IsConflictingWith(packages[j]))
                {
                    reasons.Add(new ConflictInvalidReason(packages[i].Name, packages[j].Name));
                }
            }
        }

        // dependencies
        Dictionary<string, List<VersionIdentifier>> dict = [];
        foreach (var p in packages)
        {
            HashSet<(string Name, VersionIdentifier Version)> fullSet
                = p.Provides.Select(x => (x.Key, x.Value)).ToHashSet();
            fullSet.Add((p.Name, p.Version));
            foreach (var (name, version) in fullSet)
            {
                if (dict.TryGetValue(name, out var list))
                {
                    list.Add(version);
                }
                else
                {
                    dict.Add(name, [version]);
                }
            }
        }
        foreach (var p in packages)
        {
            foreach (var dep in p.Depends)
            {
                if (!dict.TryGetValue(dep.Key, out var list) || !list.Any(x => dep.Value.Contains(x)))
                {
                    reasons.Add(new MissingDependsInvalidReason(p.Name, dep.Key, dep.Value));
                }
            }
        }

        // replaces
        foreach (var p in packages)
        {
            foreach (var rep in p.Replaces)
            {
                if (dict.TryGetValue(rep.Key, out var list) && list.Any(x => rep.Value.Contains(x)))
                {
                    reasons.Add(new PackageReplacedInvalidReason(p.Name, rep.Key));
                }
            }
        }

        return reasons.ToArray();
    }

    /// <summary>
    /// Checks the validity of adding a set of packages to an existing valid base set.
    /// This is semantically equivalent to <see cref="CheckSet(PackageMeta[])"/> on the union
    /// of <paramref name="base"/> and <paramref name="add"/>, but is more efficient because
    /// it only checks interactions involving the newly added packages.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>This method assumes that both <paramref name="base"/> and <paramref name="add"/>
    /// are valid package sets. For performance reasons, this assumption is considered truth
    /// and won't be checked. The result is unmeaning if the assumed conditions are not met.</item>
    /// <item>If you are trying to check the compatibility when installing a new batch of packages,
    /// you should update the base set first if there are any upgrading/downgrading packages,
    /// then call <see cref="CheckSet"/> to check the issues caused by altering versions;
    /// then use this method where <paramref name="add"/> only contains new packages to check
    /// whether they are acceptable.</item>
    /// </list>
    /// </remarks>
    /// <param name="base">An existing valid set of packages.</param>
    /// <param name="add">The packages to add to the base set.</param>
    /// <returns>An array of <see cref="InvalidReason"/> describing any issues found.</returns>
    public static InvalidReason[] CheckAdd(PackageMeta[] @base, PackageMeta[] add)
    {
        List<InvalidReason> reasons = [];

        // duplicate: only check between add and base (both sets are assumed internally valid)
        HashSet<string> baseNames = @base.Select(p => p.Name).ToHashSet();
        for (int i = 0; i < add.Length; i++)
        {
            if (baseNames.Contains(add[i].Name))
            {
                reasons.Add(new DuplicateNameInvalidReason(add[i].Name));
            }
        }

        // conflicts: check within add, and add vs base
        for (int i = 0; i < add.Length; i++)
        {
            for (int j = i + 1; j < add.Length; j++)
            {
                if (add[i].IsConflictingWith(add[j]))
                {
                    reasons.Add(new ConflictInvalidReason(add[i].Name, add[j].Name));
                }
            }
            foreach (var bp in @base)
            {
                if (add[i].IsConflictingWith(bp))
                {
                    reasons.Add(new ConflictInvalidReason(add[i].Name, bp.Name));
                }
            }
        }

        // dependencies: build combined dict from base and add, then check add packages' depends
        Dictionary<string, List<VersionIdentifier>> dict = [];
        foreach (var p in @base)
        {
            HashSet<(string Name, VersionIdentifier Version)> fullSet
                = p.Provides.Select(x => (x.Key, x.Value)).ToHashSet();
            fullSet.Add((p.Name, p.Version));
            foreach (var (name, version) in fullSet)
            {
                if (dict.TryGetValue(name, out var list))
                {
                    list.Add(version);
                }
                else
                {
                    dict.Add(name, [version]);
                }
            }
        }
        foreach (var p in add)
        {
            HashSet<(string Name, VersionIdentifier Version)> fullSet
                = p.Provides.Select(x => (x.Key, x.Value)).ToHashSet();
            fullSet.Add((p.Name, p.Version));
            foreach (var (name, version) in fullSet)
            {
                if (dict.TryGetValue(name, out var list))
                {
                    list.Add(version);
                }
                else
                {
                    dict.Add(name, [version]);
                }
            }
        }
        foreach (var p in add)
        {
            foreach (var dep in p.Depends)
            {
                if (!dict.TryGetValue(dep.Key, out var list) || !list.Any(x => dep.Value.Contains(x)))
                {
                    reasons.Add(new MissingDependsInvalidReason(p.Name, dep.Key, dep.Value));
                }
            }
        }

        // replaces: check all packages' replaces against the combined dict.
        // Since @base is assumed valid, base-against-base produces no results,
        // so only add-involved interactions will surface.
        foreach (var p in @base.Concat(add))
        {
            foreach (var rep in p.Replaces)
            {
                if (dict.TryGetValue(rep.Key, out var list) && list.Any(x => rep.Value.Contains(x)))
                {
                    reasons.Add(new PackageReplacedInvalidReason(p.Name, rep.Key));
                }
            }
        }

        return reasons.ToArray();
    }

    /// <summary>
    /// Checks the validity of removing a set of packages from an existing valid base set.
    /// This is semantically equivalent to <see cref="CheckSet(PackageMeta[])"/> on
    /// <paramref name="base"/> minus the packages named in <paramref name="remove"/>,
    /// but is more efficient because it only re-checks dependencies of the remaining packages.
    /// </summary>
    /// <param name="base">An existing valid set of packages.</param>
    /// <param name="remove">The names of the packages to remove.</param>
    /// <returns>An array of <see cref="InvalidReason"/> describing any issues found.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when any name in <paramref name="remove"/> is not present in <paramref name="base"/>.
    /// </exception>
    public static InvalidReason[] CheckRemove(PackageMeta[] @base, string[] remove)
    {
        // Validate that remove is a subset of @base
        HashSet<string> baseNameSet = @base.Select(p => p.Name).ToHashSet();
        foreach (var name in remove)
        {
            if (!baseNameSet.Contains(name))
            {
                throw new ArgumentException(
                    $"Cannot remove package '{name}' because it is not present in the base set.",
                    nameof(remove));
            }
        }

        List<InvalidReason> reasons = [];
        HashSet<string> removeSet = remove.ToHashSet();

        // Build dict of remaining packages (excluding those being removed)
        Dictionary<string, List<VersionIdentifier>> dict = [];
        foreach (var p in @base)
        {
            if (removeSet.Contains(p.Name))
                continue;

            HashSet<(string Name, VersionIdentifier Version)> fullSet
                = p.Provides.Select(x => (x.Key, x.Value)).ToHashSet();
            fullSet.Add((p.Name, p.Version));
            foreach (var (name, version) in fullSet)
            {
                if (dict.TryGetValue(name, out var list))
                {
                    list.Add(version);
                }
                else
                {
                    dict.Add(name, [version]);
                }
            }
        }

        // Re-check dependencies of remaining packages.
        // Removing packages cannot create new duplicate, conflict, or replaces issues,
        // but it can break dependencies.
        foreach (var p in @base)
        {
            if (removeSet.Contains(p.Name))
                continue;

            foreach (var dep in p.Depends)
            {
                if (!dict.TryGetValue(dep.Key, out var list) || !list.Any(x => dep.Value.Contains(x)))
                {
                    reasons.Add(new MissingDependsInvalidReason(p.Name, dep.Key, dep.Value));
                }
            }
        }

        return reasons.ToArray();
    }
}