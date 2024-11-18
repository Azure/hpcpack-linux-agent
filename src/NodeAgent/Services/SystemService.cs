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
     * Return the absolute path of the key file.
     * Throw an exception if anything wrong.
     */
    Task<string> AddSshKeyAsync(string username, string key, bool isPrivateKey, CancellationToken cancellationToken = default);

    /*
     * Return the absolute path of the removed key file, or null if the file doesn't exist.
     * Throw an exception if anything wrong.
     */
    Task<string?> RemoveSshKeyAsync(string username, bool isPrivateKey, CancellationToken cancellationToken = default);

    /*
     * Return an absolute path of the authorized key file.
     * Throw an exception if anything wrong.
     */
    Task<string> AddAuthorizedKeyAsync(string username, string key, CancellationToken cancellationToken = default);

    /*
     * Return the absolute path of the authorized key file, or null if the file doesn't exist.
     * Throw an exception if anything wrong.
     */
    Task<string?> RemoveAuthorizedKeyAsync(string username, string key, CancellationToken cancellationToken = default);

    Task<Tuple<ulong, ulong>> GetCpuUsageAsync(CancellationToken cancellationToken = default);

    Task<Tuple<ulong, ulong>> GetMemoryUsageAsync(CancellationToken cancellationToken = default);

    Task<Tuple<float, float>> GetVirtualMemoryStatAsync(CancellationToken cancellationToken = default);

    float GetFreeSpacePercentage();
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
            FileName = "/bin/bash",
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
            var msg = $"Error when creating user '{username}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}";
            throw new SystemException(msg);
        }
        return code == 0;
    }

    [SupportedOSPlatform("linux")]
    public async Task<string> GenerateSshPublicKeyAsync(string privateKeyFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(privateKeyFilePath))
        {
            throw new ArgumentException($"File '{privateKeyFilePath}' doesn't exist.", nameof(privateKeyFilePath));
        }

        var script = @"
set -ex

keyfile=$1
ssh-keygen -y -f ""$keyfile""
";
        var (code, stdout, stderr) = await ExecuteInShellAsync(
            script, [nameof(GenerateSshPublicKeyAsync), privateKeyFilePath], null, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("GenerateSshPublicKeyAsync result:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);

        if (code != 0 && code != 100)
        {
            var msg = $"Error when generating SSH public key from file '{privateKeyFilePath}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}";
            throw new SystemException(msg);
        }
        //NOTE: The ending '\n' is kept.
        return stdout;
    }

    [SupportedOSPlatform("linux")]
    public async Task<string> AddSshKeyAsync(string username, string key, bool isPrivateKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username '{username}'!");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"Invalid key!");
        }

        var script = @"
set -ex

user=$1
private=$2

home_dir=$(eval printf ""~$user"")
ssh_dir=$home_dir/.ssh
if [[ ! -d ""$ssh_dir"" ]]; then
    mkdir ""$ssh_dir""
    chown ""$user"" ""$ssh_dir""
    chmod 700 ""$ssh_dir""
fi

if ((private == 1)); then
    key_file=id_rsa
    key_file_mode=600
else
    key_file=id_rsa.pub
    key_file_mode=644
fi

key_path=$ssh_dir/$key_file
if [[ -a ""$key_path"" ]]; then
    printf ""$key_path""
    exit 100
fi

cp /dev/stdin ""$key_path""
chown ""$user"" ""$key_path""
chmod $key_file_mode ""$key_path""

printf ""$key_path""
";
        var (code, stdout, stderr) = await ExecuteInShellAsync(
            script, [nameof(AddSshKeyAsync), username, isPrivateKey ? "1" : "0"], key, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("AddSshKeyAsync result:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);

        if (code != 0 && code != 100)
        {
            var msg = $"Error when adding {(isPrivateKey ? "private" : "public")} SSH key for user '{username}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}";
            throw new SystemException(msg);
        }
        return stdout.TrimEnd();
    }

    [SupportedOSPlatform("linux")]
    public async Task<string?> RemoveSshKeyAsync(string username, bool isPrivateKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username '{username}'!");
        }

        var script = @"
set -ex

user=$1
private=$2

# Test exsitance before we go
id ""$user"" >/dev/null 2>&1

home_dir=$(eval printf ""~$user"")
ssh_dir=$home_dir/.ssh

if ((private == 1)); then
    key_file=id_rsa
else
    key_file=id_rsa.pub
fi

key_path=$ssh_dir/$key_file
if [[ ! -a ""$key_path"" ]]; then
    exit 0
fi

rm -rf ""$key_path""
printf ""$key_path""
";
        var (code, stdout, stderr) = await ExecuteInShellAsync(
            script, [nameof(RemoveSshKeyAsync), username, isPrivateKey ? "1" : "0"], null, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("RemoveSshKeyAsync result:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);

        if (code != 0)
        {
            var msg = $"Error when removing {(isPrivateKey ? "private" : "public")} SSH key for user '{username}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}";
            throw new SystemException(msg);
        }

        var path = stdout.TrimEnd();
        return string.IsNullOrEmpty(path) ? null : path;
    }

    [SupportedOSPlatform("linux")]
    public async Task<string> AddAuthorizedKeyAsync(string username, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username '{username}'!");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"Invalid key!");
        }

        var script = @"
set -ex

user=$1

home_dir=$(eval printf ""~$user"")
ssh_dir=$home_dir/.ssh
if [[ ! -d ""$ssh_dir"" ]]; then
    mkdir ""$ssh_dir""
    chown ""$user"" ""$ssh_dir""
    chmod 700 ""$ssh_dir""
fi

key_file=$ssh_dir/authorized_keys

# Read key from subshell. In this way, the trailing line endings are removed.
key=$(cat /dev/stdin)
echo ""$key"" >> ""$key_file""
chown ""$user"" ""$key_file""
chmod 600 ""$key_file""
printf ""$key_file""
";
        var (code, stdout, stderr) = await ExecuteInShellAsync(
            script, [nameof(AddAuthorizedKeyAsync), username], key, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("AddAuthorizedKeyAsync result:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);

        if (code != 0)
        {
            var msg = $"Error when adding authorized key for user '{username}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}";
            throw new SystemException(msg);
        }
        return stdout.TrimEnd();
    }

    [SupportedOSPlatform("linux")]
    public async Task<string?> RemoveAuthorizedKeyAsync(string username, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username '{username}'!");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"Invalid key!");
        }

        var script = @"
set -ex

user=$1

# Test exsitance before we go
id ""$user"" >/dev/null 2>&1

home_dir=$(eval printf ""~$user"")
ssh_dir=$home_dir/.ssh
key_file=$ssh_dir/authorized_keys

if [[ ! -a ""$key_file"" ]]; then
    exit 0
fi

key=$(cat /dev/stdin)
sed -i /^""$key""$/d ""$key_file""
printf ""$key_file""
";
        var (code, stdout, stderr) = await ExecuteInShellAsync(
    script, [nameof(RemoveAuthorizedKeyAsync), username], key, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("RemoveAuthorizedKeyAsync result:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);

        if (code != 0)
        {
            var msg = $"Error when removing authorized key for user '{username}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}";
            throw new SystemException(msg);
        }

        var path = stdout.TrimEnd();
        return string.IsNullOrEmpty(path) ? null : path;
    }

    public Task<Tuple<ulong, ulong>> GetCpuUsageAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Tuple<ulong, ulong>> GetMemoryUsageAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Tuple<float, float>> GetVirtualMemoryStatAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public float GetFreeSpacePercentage()
    {
        throw new NotImplementedException();
    }
}
