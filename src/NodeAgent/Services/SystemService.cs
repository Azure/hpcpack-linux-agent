using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using System.Text;

namespace NodeAgent.Services;

public interface ISystemService
{
    string HostName { get; }

    /*
     * Execute a command line in "/bin/sh".
     * Return a tuple of exit code, stdout and stderr of the command.
     * Throw an exception if anyting wrong (the exit code of the command is not considered for raising exception).
     */
    Task<Tuple<int, string, string>> ExecuteInShellAsync(string cmd, CancellationToken cancellationToken = default);

    /*
     * Return true when a new user is created, false when the user already exists.
     * Throw an exception if anything wrong.
     */
    Task<bool> CreateUserAsync(string username, string? password, bool isAdmin, CancellationToken cancellationToken = default);

    /*
     * Return the content of generated key.
     * Throw an exception if anything wrong.
     */
    Task<string> GenerateSshPublicKeyAsync(string privateKeyFilePath, CancellationToken cancellationToken = default);

    /*
     * Return an absolute path of the key file.
     * Throw an exception if anything wrong.
     */
    Task<string> AddSshKeyAsync(string username, string key, bool isPrivateKey, CancellationToken cancellationToken = default);

    /*
     * Return an absolute path of the key file.
     * Throw an exception if anything wrong.
     */
    Task AddAuthorizedKeyAsync(string username, string key, CancellationToken cancellationToken = default);

    Task RemoveSshKeyAsync(string username, bool isPrivateKey, CancellationToken cancellationToken = default);

    Task RemoveAuthorizedKeyAsync(string username, string key, CancellationToken cancellationToken = default);
}

public class SystemService : ISystemService
{
    public string HostName => Dns.GetHostName();

    [SupportedOSPlatform("linux")]
    public async Task<Tuple<int, string, string>> ExecuteInShellAsync(string cmd, CancellationToken cancellationToken = default)
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

        using var process = new Process()
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

        return new(exitCode, stdout, stderr);
    }

    public Task<bool> CreateUserAsync(string username, string? password, bool isAdmin, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GenerateSshPublicKeyAsync(string privateKeyFilePath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> AddSshKeyAsync(string userName, string key, bool isPrivateKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task AddAuthorizedKeyAsync(string username, string key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task RemoveSshKeyAsync(string username, bool isPrivateKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task RemoveAuthorizedKeyAsync(string username, string key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
