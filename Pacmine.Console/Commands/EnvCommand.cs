using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class EnvCommand
{
    public static Command Create()
    {
        var envCmd = new Command("env", "Manage environment packages");

        var nameArg = new Argument<string>("name")
        {
            Description = "The name of the environment package"
        };
        var versionArg = new Argument<string>("version")
        {
            Description = "The version of the environment package"
        };
        var setCmd = new Command("set", "Add an env package (if a new one) or change its version")
        {
            nameArg,
            versionArg
        };
        setCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetValue(nameArg);
            var version = parseResult.GetValue(versionArg);
            await ExecuteSetAsync(name, version);
        });

        var unsetNameArg = new Argument<string>("name")
        {
            Description = "The name of the environment package to remove"
        };
        var unsetCmd = new Command("unset", "Remove an env package")
        {
            unsetNameArg
        };
        unsetCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetValue(unsetNameArg);
            await ExecuteUnsetAsync(name);
        });

        envCmd.Add(setCmd);
        envCmd.Add(unsetCmd);

        return envCmd;
    }

    private static async Task ExecuteSetAsync(string? name, string? version)
    {
        throw new NotImplementedException();
    }

    private static async Task ExecuteUnsetAsync(string? name)
    {
        throw new NotImplementedException();
    }
}
