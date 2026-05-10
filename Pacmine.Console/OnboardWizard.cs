namespace Pacmine.Console;

public class OnboardWizard
{
    public required string InstancePath { get; set; }

    public required string MinecraftVersion { get; set; }

    public string? FabricLoaderVersion { get; set; }

    public string? NeoForgeLoaderVersion { get; set; }

    public required string JavaVersion { get; set; }

    public async Task RunAsync()
    {
        throw new NotImplementedException();
    }
}
