namespace Pacmine.Console;

/// <summary>
/// Guides the user through initial environment setup when initializing a Pacmine root.
/// Detects Minecraft version, mod loader versions, and Java version,
/// then creates corresponding environment packages.
/// </summary>
public class OnboardWizard
{
    /// <summary>
    /// Gets or sets the path to the game instance directory to initialize.
    /// </summary>
    public required string InstancePath { get; set; }

    /// <summary>
    /// Gets or sets the detected Minecraft version (e.g. "26.1.2").
    /// </summary>
    public required string MinecraftVersion { get; set; }

    /// <summary>
    /// Gets or sets the detected Fabric Loader version, if applicable.
    /// </summary>
    public string? FabricLoaderVersion { get; set; }

    /// <summary>
    /// Gets or sets the detected NeoForge loader version, if applicable.
    /// </summary>
    public string? NeoForgeLoaderVersion { get; set; }

    /// <summary>
    /// Gets or sets the detected Java version (e.g. "25").
    /// </summary>
    public required string JavaVersion { get; set; }

    /// <summary>
    /// Runs the onboard wizard interactively.
    /// </summary>
    public async Task RunAsync()
    {
        throw new NotImplementedException();
    }
}
