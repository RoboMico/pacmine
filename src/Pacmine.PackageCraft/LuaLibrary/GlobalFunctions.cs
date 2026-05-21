using System.Diagnostics;
using Lua;

namespace Pacmine.PackageCraft.LuaLibrary;

/// <summary>
/// Provides global Lua functions exposed to the Lua state.
/// Disabled functions are not registered into the Lua state at all — no runtime permission checks.
/// </summary>
public class GlobalFunctions
{
    private readonly PackageBuilder builderContext;
    private readonly string? gitCommand;
    private readonly bool allowShellExecution;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalFunctions"/> class.
    /// </summary>
    /// <param name="builderContext">The <see cref="PackageBuilder"/> instance providing stdout/stderr and working directory context.</param>
    /// <param name="gitCommand">The Git command path, or <c>null</c> to disable the <c>git()</c> function.</param>
    /// <param name="allowShellExecution">Whether the <c>shell()</c> function is available.</param>
    public GlobalFunctions(PackageBuilder builderContext, string? gitCommand, bool allowShellExecution)
    {
        this.builderContext = builderContext;
        this.gitCommand = gitCommand;
        this.allowShellExecution = allowShellExecution;
    }

    /// <summary>
    /// Registers the global Lua functions into the specified Lua state.
    /// Only enabled functions are registered — disabled functions are never reachable from Lua.
    /// </summary>
    /// <param name="luaState">The Lua state whose global environment will receive the registered functions.</param>
    public void RegisterFunctions(LuaState luaState)
    {
        // print and printerr are always safe — register unconditionally
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

        // git: register only if a Git command is configured
        if (gitCommand != null)
        {
            luaState.Environment["git"] = new LuaFunction((context, ct) =>
            {
                string args = context.GetArgument<string>(0);
                int timeoutSec = 0;
                if (context.ArgumentCount > 1)
                {
                    timeoutSec = context.GetArgument<int>(1);
                }
                int ret = GitCall(args, timeoutSec);
                return new(context.Return(ret));
            });
        }

        // shell: register only if explicitly allowed
        if (allowShellExecution)
        {
            luaState.Environment["shell"] = new LuaFunction((context, ct) =>
            {
                string command = context.GetArgument<string>(0);
                int timeoutSec = 0;
                if (context.ArgumentCount > 1)
                {
                    timeoutSec = context.GetArgument<int>(1);
                }
                int ret = ShellExecute(command, timeoutSec);
                return new(context.Return(ret));
            });
        }
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
    /// This method is only called when <c>git</c> was registered (i.e., <see cref="gitCommand"/> is not null).
    /// </summary>
    /// <param name="args">The command-line arguments to pass to Git.</param>
    /// <returns>The exit code returned by the Git process.</returns>
    public int GitCall(string args, int timeoutSec = 0)
    {
        return RunProcess(gitCommand!, args, builderContext.SourceDirectory!.FullName, timeoutSec);
    }

    /// <summary>
    /// Executes a shell command.
    /// This method is only called when <c>shell</c> was registered (i.e., <see cref="allowShellExecution"/> is <c>true</c>).
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>The exit code returned by the shell process.</returns>
    public int ShellExecute(string command, int timeoutSec = 0)
    {
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
        return RunProcess(shell, shellArgs, builderContext.SourceDirectory!.FullName, timeoutSec);
    }

    private int RunProcess(string command, string args, string workingDirectory, int timeoutSec)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            WorkingDirectory = workingDirectory,
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

        if (timeoutSec > 0)
        {
            if (!process.WaitForExit(timeoutSec * 1000))
            {
                // Timed out — kill the process tree and return a failure exit code.
                process.Kill(entireProcessTree: true);
                builderContext.WriteStderr($"\nProcess timed out after {timeoutSec} second(s) and was killed.\n");
                return -65536;
            }
        }
        else
        {
            process.WaitForExit();
        }

        return process.ExitCode;
    }
}
