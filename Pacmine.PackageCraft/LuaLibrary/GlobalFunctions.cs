using Lua;

namespace Pacmine.PackageCraft.LuaLibrary;

/// <summary>
/// Provides global Lua functions exposed to the Lua state in <see cref="PackageBuilder"/>.
/// </summary>
public class GlobalFunctions
{
    private PackageBuilder builderContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalFunctions"/> class.
    /// </summary>
    /// <param name="builderContext">The <see cref="PackageBuilder"/> instance providing environment context.</param>
    public GlobalFunctions(PackageBuilder builderContext)
    {
        this.builderContext = builderContext;
    }

    /// <summary>
    /// Registers the global Lua functions into the specified Lua state.
    /// </summary>
    /// <param name="luaState">The Lua state whose global environment will receive the registered functions.</param>
    public void RegisterFunctions(LuaState luaState)
    {
        luaState.Environment["print"] = new LuaFunction((context, ct) =>
        {
            Print(context.GetArgument<string>(0));
            return new(context.Return());
        });

        luaState.Environment["git"] = new LuaFunction((context, ct) =>
        {
            int ret = GitCall(context.GetArgument<string>(0));
            return new(context.Return(ret));
        });
    }

    /// <summary>
    /// Prints a message to the output.
    /// </summary>
    /// <param name="message">The message string to print.</param>
    public void Print(string message)
    {
        // TODO: Implement
    }

    /// <summary>
    /// Executes a Git command with the specified arguments.
    /// </summary>
    /// <param name="args">The command-line arguments to pass to Git.</param>
    /// <returns>The exit code returned by the Git process.</returns>
    /// <exception cref="Exception">Thrown when Git functionality is disabled(<see cref="PackageBuilder.GitCommand"/> is <c>null</c>).</exception>
    public int GitCall(string args)
    {
        if (builderContext.GitCommand == null)
        {
            throw new Exception("Git is disabled");
        }
        // TODO: Implement

        return -1;
    }
}