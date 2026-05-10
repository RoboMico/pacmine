using System.CommandLine;

namespace Pacmine.Console.Commands;

internal static class EnvCommand
{
    public static Command Create()
    {
        var envCmd = new Command("env", "Manage environment packages");

        var setCmd = new Command("set", "Add an env package (if a new one) or change its version")
        {
            new Argument<string>("name", "The name of the environment package"),
            new Argument<string>("version", "The version of the environment package")
        };
        setCmd.SetHandler(ExecuteSetAsync,
            setCmd.Arguments[0] as Argument<string> ?? throw new InvalidOperationException(),
            setCmd.Arguments[1] as Argument<string> ?? throw new InvalidOperationException());

        var unsetCmd = new Command("unset", "Remove an env package")
        {
            new Argument<string>("name", "The name of the environment package to remove")
        };
        unsetCmd.SetHandler(ExecuteUnsetAsync,
            unsetCmd.Arguments[0] as Argument<string> ?? throw new InvalidOperationException());

        envCmd.AddCommand(setCmd);
        envCmd.AddCommand(unsetCmd);

        return envCmd;
    }

    private static async Task ExecuteSetAsync(string name, string version)
    {
        throw new NotImplementedException();
    }

    private static async Task ExecuteUnsetAsync(string name)
    {
        throw new NotImplementedException();
    }
}
