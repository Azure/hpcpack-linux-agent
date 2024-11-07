using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using System.Text;

namespace NodeAgent.Services;

public class SystemException : ApplicationException
{
    public SystemException() : base() { }

    public SystemException(string message) : base(message) { }
}

public interface ISystemService
{
    string HostName { get; }

    /*
     * Execute a command line in "/bin/sh".
     * Return a tuple of exit code, stdout and stderr of the command.
     * Throw an exception if anyting wrong (the exit code of the command is not considered for raising exception).
     */
    Task<Tuple<int, string, string>> ExecuteInShellAsync(string cmd, IEnumerable<string>? args = null, string? stdin = null,
        CancellationToken cancellationToken = default);

    /*
     * Return true when a new user is created, false when the user already exists.
     * Throw an exception if anything wrong.
     */
    Task<bool> CreateUserAsync(string username, string password, bool isAdmin, CancellationToken cancellationToken = default);

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
    private ILogger _logger;

    public SystemService(ILogger<SystemService> logger)
    {
        _logger = logger;
    }


    public string HostName => Dns.GetHostName();

    [SupportedOSPlatform("linux")]
    public async Task<Tuple<int, string, string>> ExecuteInShellAsync(string cmd, IEnumerable<string>? args = null, string? stdin = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cmd))
        {
            throw new ArgumentException("Invalid commnad.", nameof(cmd));
        }
        //NOTE: This is critical for a multi-line command!
        cmd = cmd.Replace("\r\n", "\n");

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
        if (stdin != null)
        {
            startInfo.RedirectStandardInput = true;
        }
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(cmd);
        if (args != null)
        {
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

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

        if (stdin != null)
        {
            var stdinWriter = process.StandardInput;
            stdinWriter.Write(stdin);
            stdinWriter.Close();
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        exitCode = process.ExitCode;
        stdout = stdoutBuilder.ToString();
        stderr = stderrBuilder.ToString();

        return new(exitCode, stdout, stderr);
    }

    [SupportedOSPlatform("linux")]
    public async Task<bool> CreateUserAsync(string username, string password, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username '{username}'!");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Contains('\n'))
        {
            throw new ArgumentException($"Invalid password!", nameof(password));
        }

        var script = @"
set -ex

user=$1
admin=$2

if id -u ""$user"" ; then
    exit 100
fi

useradd -m -s /bin/bash ""$user""
passwd ""$user""

if ((admin == 1)) ; then
    usermod -aG sudo ""$user"" || usermod -aG wheel ""$user""
fi
";
        var stdin = $"{password}\n{password}\n";
        var (code, stdout, stderr) = await ExecuteInShellAsync(
            script, [nameof(CreateUserAsync), username, isAdmin ? "1" : "0"], stdin, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("CreateUserAsync result:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);

        if (code != 0 && code != 100)
        {
            throw new SystemException($"Error when creating user '{username}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}");
        }

        return code == 0;
    }

    /*
     * NOTE
     *
     * The method is only supposed to be used in test, not for production, for it doesn't stop the user's processes,
     * if any, before deleting the user.
     */
    [SupportedOSPlatform("linux")]
    public async Task DeleteUserAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username '{username}'!");
        }

        var cmd = @"userdel -r ""$1""";
        var (code, stdout, stderr) = await ExecuteInShellAsync(cmd, [nameof(DeleteUserAsync), username], null, cancellationToken);

        if (code != 0)
        {
            throw new SystemException($"Error when deleting user '{username}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}");
        }
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
