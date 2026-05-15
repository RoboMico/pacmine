using System.Diagnostics;
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
        luaState.Environment["printerr"] = new LuaFunction((context, ct) =>
        {
            PrintError(context.GetArgument<string>(0));
            return new(context.Return());
        });
        luaState.Environment["git"] = new LuaFunction((context, ct) =>
        {
            int ret = GitCall(context.GetArgument<string>(0));
            return new(context.Return(ret));
        });
        luaState.Environment["shell"] = new LuaFunction((context, ct) =>
        {
            int ret = ShellExecute(context.GetArgument<string>(0));
            return new(context.Return(ret));
        });
    }

    /// <summary>
    /// Prints a message to the output.
    /// </summary>
    /// <param name="message">The message string to print.</param>
    public void Print(string message)
    {
        builderContext.WriteStdout(message);
    }

    /// <summary>
    /// Prints a message to the error stream.
    /// </summary>
    /// <param name="message">The message string to print.</param>
    public void PrintError(string message)
    {
        builderContext.WriteStderr(message);
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

        var psi = new ProcessStartInfo
        {
            FileName = builderContext.GitCommand,
            Arguments = args,
            WorkingDirectory = builderContext.SourceDirectory!.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                builderContext.WriteStdout(e.Data + "\n");
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                builderContext.WriteStderr(e.Data + "\n");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode;
    }

    /// <summary>
    /// Executes a shell command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>The exit code returned by the shell process.</returns>
    /// <exception cref="Exception">Thrown when shell execution is disabled(<see cref="PackageBuilder.AllowShellExceution"/> is <c>false</c>).</exception>
    public int ShellExecute(string command)
    {
        if (builderContext.AllowShellExceution == false)
        {
            throw new Exception("Shell execution is disabled");
        }

        string shell;
        string shellArgs;

        if (OperatingSystem.IsWindows())
        {
            shell = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            shellArgs = "/c " + command;
        }
        else
        {
            shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh";
            shellArgs = "-c " + command;
        }

        var psi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = shellArgs,
            WorkingDirectory = builderContext.SourceDirectory!.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                builderContext.WriteStdout(e.Data + "\n");
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                builderContext.WriteStderr(e.Data + "\n");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode;
    }
}