using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

using SystemProcess = System.Diagnostics.Process;

namespace NodeAgent.Utils;

public static class System
{
    [SupportedOSPlatform("linux")]
    public static Tuple<int, string, string> ExecuteInShell(string cmd)
    {
        return ExecuteInShellAsync(cmd).Result;
    }

    [SupportedOSPlatform("linux")]
    public static async Task<Tuple<int, string, string>> ExecuteInShellAsync(string cmd, CancellationToken cancellationToken = default)
    {
        int exitCode = 0;
        var stdout = string.Empty;
        var stderr = string.Empty;

        var startInfo = new ProcessStartInfo()
        {
            UseShellExecute = false, //This means the Windows GUI shell, not the Linux shell
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            FileName = "/bin/sh",
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(cmd);

        using var process = new SystemProcess()
        {
            StartInfo = startInfo,

            //NOTE: This is to avoid the parent process being zombie in some situation. See 
            //https://github.com/dotnet/runtime/issues/21661
            EnableRaisingEvents = true,
        };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        /*
         * NOTE
         *
         * The args.Data doesn't include the EOL if any. So you cannot tell if there's an EOL for
         * a line of output. Here an EOL is always appended by "AppendLine" to our stdout/stderr
         * variable. That means if the original output doesn't end with an EOL, our stdout/stderr
         * still ends with it. This is by design.
         */
        process.OutputDataReceived += (sender, args) => {
            if (args.Data != null)
            {
                stdoutBuilder.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (sender, args) => {
            if (args.Data != null)
            {
                stderrBuilder.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        exitCode = process.ExitCode;
        stdout = stdoutBuilder.ToString();
        stderr = stderrBuilder.ToString();

        return new (exitCode, stdout, stderr);
    }
}
