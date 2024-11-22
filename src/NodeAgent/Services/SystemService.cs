using NodeAgent.Models;
using NodeAgent.Utils;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

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
     * Execute a command in "/bin/sh". The command is a string of single or multiple lines.
     * The first arg (args[0]), if provided, is passed as "$0" for the command.
     * Return a tuple of exit code, stdout and stderr of the command.
     * Throw an exception if anyting wrong (the exit code of the command is not considered for raising exception).
     */
    Task<Tuple<int, string, string>> ExecuteInShellAsync(string cmd, IEnumerable<string>? args = null, string? stdin = null,
        CancellationToken cancellationToken = default);

    /*
     * Execute a command in "/bin/sh". The command is a file path.
     * The first arg (args[0]), if provided, is passed as "$1" for the file.
     * Return a tuple of exit code, stdout and stderr of the command.
     * Throw an exception if anyting wrong (the exit code of the command is not considered for raising exception).
     */
    Task<Tuple<int, string, string>> ExecuteFileInShellAsync(string filePath, IEnumerable<string>? args = null, string? stdin = null,
        string? workingDir = null, CancellationToken cancellationToken = default);

    Task<int> ExecuteFileInShellExAsync(string filePath, IEnumerable<string>? args = null, string? stdin = null, string? workingDir = null,
        IDictionary<string, string?>? env = null, Action<string>? onStdOut = null, Action<string>? onStdErr = null, Action<Process>? onStart = null,
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

    /*
     * Create a temporary directory from the template and make it available only to the user by the username.
     * The template must contain at least 3 consecutive 'X's in last component. For example, "/tmp/task_XXX".
     */
    Task<string> MakeTempDirectoryAsync(string username, string template, CancellationToken cancellationToken = default);

    Task<Tuple<ulong, ulong>> GetCpuUsageAsync(CancellationToken cancellationToken = default);

    /*
     * Return a tuple of total memory and available memory in kB.
     * Throw an exception if anything wrong.
     */
    Task<Tuple<ulong, ulong>> GetMemoryUsageAsync(CancellationToken cancellationToken = default);

    Task<Tuple<float, float>> GetVirtualMemoryStatAsync(CancellationToken cancellationToken = default);

    float GetFreeSpacePercentage();

    /*
     * Return a tuple of cpu cores and sockets.
     * Throw an exception if anything wrong.
     */
    Task<Tuple<int, int>> GetCpuCoresInfoAsync(CancellationToken cancellationToken = default);

    Task<string> GetDistroInfoAsync(CancellationToken cancellationToken = default);

    IList<NetworkInfo> GetNetworkInfo();

    IDictionary<string, ulong> GetNetworkUsageInBytes();

    Task<IDictionary<string, ulong>> GetIbNetworkUsageAsync(CancellationToken cancellationToken = default);

    Task<IList<string>> GetIbDevicesAsync(CancellationToken cancellationToken = default);

    Task<IList<ExtendedGpuInfo>> GetGpuInfoAsync(CancellationToken cancellationToken = default);

    Task<bool> InitializeGpuDriver(CancellationToken cancellationToken = default);
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
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        var onStdOut = (string line) =>
        {
            stdoutBuilder.AppendLine(line);
        };
        var onStdErr = (string line) =>
        {
            stderrBuilder.AppendLine(line);
        };
        var code = await ExecuteInShellExAsync(cmd, args, stdin, false, null, null, onStdOut, onStdErr, null, cancellationToken);
        return new Tuple<int, string, string>(code, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }

    [SupportedOSPlatform("linux")]
    public async Task<Tuple<int, string, string>> ExecuteFileInShellAsync(string filePath, IEnumerable<string>? args = null, string? stdin = null,
        string? workingDir = null, CancellationToken cancellationToken = default)
    {
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        var onStdOut = (string line) =>
        {
            stdoutBuilder.AppendLine(line);
        };
        var onStdErr = (string line) =>
        {
            stderrBuilder.AppendLine(line);
        };
        var code = await ExecuteInShellExAsync(filePath, args, stdin, true, workingDir, null, onStdOut, onStdErr, null, cancellationToken);
        return new Tuple<int, string, string>(code, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }

    [SupportedOSPlatform("linux")]
    public Task<int> ExecuteFileInShellExAsync(string filePath, IEnumerable<string>? args = null, string? stdin = null,
        string? workingDir = null, IDictionary<string, string?>? env = null, Action<string>? onStdOut = null, Action<string>? onStdErr = null,
        Action<Process>? onStart = null, CancellationToken cancellationToken = default)
    {
        return ExecuteInShellExAsync(filePath, args, stdin, true, workingDir, env, onStdOut, onStdErr, null, cancellationToken);
    }

    [SupportedOSPlatform("linux")]
    private async Task<int> ExecuteInShellExAsync(string cmd, IEnumerable<string>? args = null, string? stdin = null, bool isFileCmd = false,
        string ? workingDir = null, IDictionary<string, string?>? env = null, Action<string>? onStdOut = null, Action<string>? onStdErr = null,
        Action<Process>? onStart = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cmd))
        {
            throw new ArgumentException("Invalid commnad.", nameof(cmd));
        }

        if (!isFileCmd)
        {
            //NOTE: This is critical for a multi-line command on Linux.
            //TODO: Maybe we should not do it inside this method, but in the caller.
            cmd = cmd.Replace("\r\n", "\n");
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo()
        {
            UseShellExecute = false, //This means the Windows GUI shell, not the Linux shell
            FileName = "/bin/bash",
        };

        if (workingDir != null)
        {
            startInfo.WorkingDirectory = workingDir;
        }
        if (env != null)
        {
            foreach (var (key, val) in env)
            {
                startInfo.Environment.Add(key, val);
            }
        }
        if (onStdOut != null)
        {
            startInfo.RedirectStandardOutput = true;
        }
        if (onStdErr != null)
        {
            startInfo.RedirectStandardError = true;
        }
        if (stdin != null)
        {
            startInfo.RedirectStandardInput = true;
        }
        if (!isFileCmd)
        {
            startInfo.ArgumentList.Add("-c");
        }
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

        if (onStdOut != null)
        {
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
                    onStdOut(args.Data);
                }
            };
        }

        if (onStdErr != null)
        {
            process.ErrorDataReceived += (sender, args) => {
                if (args.Data != null)
                {
                    onStdErr(args.Data);
                }
            };
        }

        process.Start();
        onStart?.Invoke(process);

        if (stdin != null)
        {
            var stdinWriter = process.StandardInput;
            stdinWriter.Write(stdin);
            stdinWriter.Close();
        }

        if (onStdOut != null)
        {
            process.BeginOutputReadLine();
        }

        if (onStdErr != null)
        {
            process.BeginErrorReadLine();
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
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

    [SupportedOSPlatform("linux")]
    public async Task<string> MakeTempDirectoryAsync(string username, string template, CancellationToken cancellationToken = default)
    {
        var script = @"
set -ex
user=$1
template=$2

path=$(mktemp -d ""$template"")
chown ""$user"" ""$path""
chmod 700 ""$path""
echo ""$path""
";

        var (code, stdout, stderr) = await ExecuteInShellAsync(script, [nameof(MakeTempDirectoryAsync), username, template], null, cancellationToken)
            .ConfigureAwait(false);
        if (code != 0)
        {
            var msg = $"Error when making temp direcctory of template '{template}' for user '{username}'. Exit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}";
            throw new SystemException(msg);
        }
        return stdout.TrimEnd();
    }

    [SupportedOSPlatform("linux")]
    public Task<Tuple<ulong, ulong>> GetCpuUsageAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    [SupportedOSPlatform("linux")]
    public async Task<Tuple<ulong, ulong>> GetMemoryUsageAsync(CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync("/proc/meminfo", cancellationToken);
        return ParseProcMemInfoContent(lines);
    }

    public Tuple<ulong, ulong> ParseProcMemInfoContent(string[] lines)
    {
        if (lines.Length < 3)
        {
            throw new FormatException("Can't parse /proc/meminfo: length < 3");
        }

        var totalLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var availableLine = lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var total = ulong.Parse(totalLine[1]);
        var available = ulong.Parse(availableLine[1]);

        return new(total, available);
    }

    [SupportedOSPlatform("linux")]
    public Task<Tuple<float, float>> GetVirtualMemoryStatAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    [SupportedOSPlatform("linux")]
    public float GetFreeSpacePercentage()
    {
        throw new NotImplementedException();
    }

    [SupportedOSPlatform("linux")]
    public async Task<Tuple<int, int>> GetCpuCoresInfoAsync(CancellationToken cancellationToken = default)
    {
        // cpu core number can be retrieved from Environment.ProcessorCount
        // but socket number can only be retrieved from /proc/cpuinfo
        var lines = await File.ReadAllLinesAsync("/proc/cpuinfo", cancellationToken);
        return ParseProcCpuInfoContent(lines);
    }

    public Tuple<int, int> ParseProcCpuInfoContent(string[] lines)
    {
        var physicalIds = new HashSet<string>();
        var coreIds = new HashSet<string>();

        foreach (var line in lines)
        {
            var cols = line.Split(':');

            // it's expected that it contains empty lines which should be skipped
            if (cols.Length >= 2)
            {
                var key = cols[0].Trim();
                var value = cols[1].Trim();
                if (key.Equals("physical id", StringComparison.OrdinalIgnoreCase))
                {
                    physicalIds.Add(value);
                }
                else if (key.Equals("processor", StringComparison.OrdinalIgnoreCase))
                {
                    coreIds.Add(value);
                }
            }
        }

        var cores = coreIds.Count;
        var sockets = physicalIds.Count;
        sockets = (sockets > 0) ? sockets : 1;

        return new(cores, sockets);
    }

    [SupportedOSPlatform("linux")]
    public async Task<string> GetDistroInfoAsync(CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync("/proc/version", cancellationToken);

        if (lines.Length == 0)
        {
            throw new FormatException("Can't parse /proc/version: length < 1");
        }

        return lines[0];
    }

    [SupportedOSPlatform("linux")]
    public IList<NetworkInfo> GetNetworkInfo()
    {
        var info = new List<NetworkInfo>();

        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var networkInterface in networkInterfaces)
        {
            var networkName = networkInterface.Name;
            // macAddress is like "00155dfc2aac"
            var macAddress = networkInterface.GetPhysicalAddress().ToString();
            // matches every two characters in the input string and appends a colon (:) after each pair, and removes the last colon
            // convertedMacAddress is like "00:15:5d:fc:2a:ac"
            var convertedMacAddress = Regex.Replace(macAddress, ".{2}", "$0:").TrimEnd(':');
            var isIB = networkName.Equals("ib0", StringComparison.OrdinalIgnoreCase);
            var addressInformationList = networkInterface.GetIPProperties().UnicastAddresses;
            var networkInfo = new NetworkInfo()
            {
                Name = networkName,
                MacAddress = convertedMacAddress,
                IsIB = isIB,
            };

            foreach (var addressInformation in addressInformationList)
            {
                var ipAddr = addressInformation.Address.ToString();
                var prefixLength = addressInformation.PrefixLength;

                // only consume the last ip address if there are multiple ip addresses
                if (addressInformation.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    networkInfo.IpV4 = $"{ipAddr}/{prefixLength}";
                }
                if (addressInformation.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    networkInfo.IpV6 = $"{ipAddr}/{prefixLength}";
                }
            }

            info.Add(networkInfo);
        }

        return info;
    }

    [SupportedOSPlatform("linux")]
    public IDictionary<string, ulong> GetNetworkUsageInBytes()
    {
        throw new NotImplementedException();
    }

    [SupportedOSPlatform("linux")]
    public Task<IDictionary<string, ulong>> GetIbNetworkUsageAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    [SupportedOSPlatform("linux")]
    public Task<IList<string>> GetIbDevicesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    [SupportedOSPlatform("linux")]
    public async Task<IList<ExtendedGpuInfo>> GetGpuInfoAsync(CancellationToken cancellationToken = default)
    {
        var command = "nvidia-smi --format=csv,noheader --query-gpu=name,uuid,pci.bus_id,pci.device_id,memory.total,clocks.max.sm,fan.speed,memory.used,power.draw,clocks.current.sm,temperature.gpu,utilization.gpu";
        var (exitCode, stdout, stderr) = await ExecuteInShellAsync(command, cancellationToken: cancellationToken);

        if (exitCode != 0)
        {
            throw new SystemException($"Failed to execute nvidia-smi, exitCode: {exitCode}, stdout: {stdout}, stderr: {stderr}");
        }
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return ParseGpuInfoContent(lines);
    }

    public IList<ExtendedGpuInfo> ParseGpuInfoContent(string[] lines)
    {
        var gpuInfos = new List<ExtendedGpuInfo>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = line.Split(",", StringSplitOptions.RemoveEmptyEntries);

            if (values.Length < 12)
            {
                throw new FormatException("Can't parse output of nvidia-smi: length < 12");
            }

            var info = new ExtendedGpuInfo()
            {
                Name = values[0].Trim(),
                Uuid = values[1].Trim(),
                PciBusId = values[2].Trim(),
                PciBusDevice = values[3].Trim(),
                TotalMemory = (long)(values[4].Trim()).RemoveMeasurement(),
                MaxSMClock = (long)(values[5].Trim()).RemoveMeasurement(),
                FanSpeed = (values[6].Trim()).RemoveMeasurement(),
                UsedMemoryMB = (values[7].Trim()).RemoveMeasurement(),
                PowerWatt = (values[8].Trim()).RemoveMeasurement(),
                CurrentSMClock = (values[9].Trim()).RemoveMeasurement(),
                Temperature = (values[10].Trim()).RemoveMeasurement(),
                GpuUtilization = (values[11].Trim()).RemoveMeasurement(),
            };

            gpuInfos.Add(info);
        }

        return gpuInfos;
    }

    [SupportedOSPlatform("linux")]
    public Task<bool> InitializeGpuDriver(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
