using Lua;
using Pacmine.Core;

namespace Pacmine.PackageCraft;

/// <summary>
/// Represents a PackageCraft build recipe containing metadata, source definitions,
/// and Lua function hooks for the build pipeline.
/// </summary>
public class PackageCraftRecipe
{
    /// <summary>
    /// Gets or sets the protocol version of the recipe.
    /// </summary>
    public required string Protocol { get; set; }

    /// <summary>
    /// Gets or sets the package metadata for the recipe.
    /// </summary>
    public required PackageMeta Meta { get; set; }

    /// <summary>
    /// Gets or sets the list of source URLs or file paths.
    /// </summary>
    public List<string> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of expected checksums for each source, in the format <c>algorithm:hash</c>.
    /// Use <c>SKIP</c> to skip verification for a source.
    /// </summary>
    public List<string> SourceChecksums { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of variable (config) files that are preserved during uninstall without <c>--purge</c>.
    /// </summary>
    public List<string> VariableFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the Lua function to invoke during the prepare phase. Can be <c>null</c>.
    /// </summary>
    public LuaFunction? Prepare { get; set; }

    /// <summary>
    /// Gets or sets the Lua function to invoke during the get-version phase. Can be <c>null</c>.
    /// </summary>
    public LuaFunction? GetVersion { get; set; }

    /// <summary>
    /// Gets or sets the Lua function to invoke during the build phase. Can be <c>null</c>.
    /// </summary>
    public LuaFunction? Build { get; set; }

    /// <summary>
    /// Gets or sets the Lua function to invoke during the check phase. Can be <c>null</c>.
    /// </summary>
    public LuaFunction? Check { get; set; }

    /// <summary>
    /// Gets or sets the Lua function to invoke during the package phase. Can be <c>null</c>.
    /// </summary>
    public LuaFunction? Package { get; set; }
}
