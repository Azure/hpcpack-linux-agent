using Microsoft.Extensions.Logging;
using NodeAgent.Services;
using System.Reflection;
using System.Runtime.Versioning;
using Xunit.Abstractions;

namespace NodeAgent.Test.Servcies;

/*
 * NOTE
 *
 * Linux OS permissons are required for some test methods, so that you may need
 * "sudo dotnet test ..." for SystemServiceTest.
 */
[SupportedOSPlatform("linux")]
public class SystemServiceTest : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ILoggerFactory _loggerFactory;
    private SystemService _system;

    private async Task<bool> IsGpuSupported()
    {
        var result = await _system.ExecuteInShellAsync("type nvidia-smi");
        return result.ExitCode == 0;
    }

    public SystemServiceTest(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(_ => { });
        var logger = _loggerFactory.CreateLogger<SystemService>();
        _system = new SystemService(logger);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    private async Task DeleteUserAsync(string username)
    {
        var cmd = @"userdel -rf ""$1""";
        var result = await _system.ExecuteInShellAsync(cmd, [nameof(DeleteUserAsync), username]).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var msg = $"Error when deleting user '{username}': {result}";
            _output.WriteLine(msg);
        }
    }

    [Fact]
    public async Task TestHostName()
    {
        var hostname = _system.HostName;
        var result = await _system.ExecuteInShellAsync("hostname");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(result.StdOut!.TrimEnd('\n'), hostname);
    }

    [Fact]
    public async Task TestExecuteInShellAsync()
    {
        //Output a multi-line string with a EOL
        var result = await _system.ExecuteInShellAsync(@"printf 'a\nb\n\nc\n'");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("a\nb\n\nc\n", result.StdOut);
        Assert.Equal("", result.StdErr);

        //Output a multi-line string without a EOL
        //Note the trailing "\n" in stdout: this is by design.
        result = await _system.ExecuteInShellAsync(@"printf 'a\nb\n\nc'");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("a\nb\n\nc\n", result.StdOut);
        Assert.Equal("", result.StdErr);

        //Execute a multi-line command
        var cmd = @"
echo abc
echo xyz >&2
exit 1
";
        result = await _system.ExecuteInShellAsync(cmd);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("abc\n", result.StdOut);
        Assert.Equal("xyz\n", result.StdErr);

        //Execute a command with arguments
        cmd = @"
echo $0
echo $1
";
        result = await _system.ExecuteInShellAsync(cmd, ["abc", "xyz"]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("abc\nxyz\n", result.StdOut);
        Assert.Equal("", result.StdErr);

        //Execute command with input as stdin
        //Note the trailing "\n" in stdout: this is by design.
        var input = "hello";
        result = await _system.ExecuteInShellAsync(@"cat", null, input);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{input}\n", result.StdOut);
        Assert.Equal("", result.StdErr);
    }

    [Fact]
    public async Task TestExecuteFileInShellAsync()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var file = Path.Join(dir, "Assets", "test.sh");
        var arg = "abc";
        var input = "input";
        var result = await _system.ExecuteFileInShellAsync(file, [arg], input);
        Assert.Equal(100, result.ExitCode);
        Assert.Equal($"{arg}\n", result.StdOut);
        Assert.Equal($"{input}\n", result.StdErr);
    }

    [Theory]
    [InlineData("testuser1", "testpw", true)]
    [InlineData("testuser2", "testpw", false)]
    public async Task TestCreateUserAsync(string username, string password, bool isAdmin)
    {
        var test = @"
set -ex

user=$1
admin=$2

id ""$user""

if ((admin == 1)) ; then
    groups=$(id -nG ""$user"")
    { echo $groups | grep -qw sudo ; } || { echo $groups | grep -qw wheel ; } || exit 1
fi
";
        try
        {
            var isNew = await _system.CreateUserAsync(username, password, isAdmin);
            Assert.True(isNew);

            var result = await _system.ExecuteInShellAsync(test, ["test", username, isAdmin ? "1" : "0"]);
            Assert.Equal(0, result.ExitCode);

            isNew = await _system.CreateUserAsync(username, password, isAdmin);
            Assert.False(isNew);
        }
        finally
        {
            await DeleteUserAsync(username);
        }
    }

    [Theory]
    [InlineData("test user1", "testpw", true)]
    [InlineData("test user2", "testpw", false)]
    public async Task TestCreateUserAsyncError(string username, string password, bool isAdmin)
    {
        try
        {
            await Assert.ThrowsAsync<Services.SystemException>(async () =>
            {
                await _system.CreateUserAsync(username, password, isAdmin);
            });
        }
        finally
        {
            await DeleteUserAsync(username);
        }
    }

    [Theory]
    [InlineData("testuser1", true)]
    [InlineData("testuser1", false)]
    public async Task TestAddSshKeyAsync(string username, bool isPrivateKey)
    {
        var key = @"
1
2
3
";
        try
        {
            var isNew = await _system.CreateUserAsync(username, "password", false);
            Assert.True(isNew);

            var fileName = isPrivateKey ? "id_rsa" : "id_rsa.pub";
            var keyFilePath = await _system.AddSshKeyAsync(username, key, isPrivateKey);
            Assert.EndsWith(fileName, keyFilePath);
            Assert.Contains(username, keyFilePath);

            var content = File.ReadAllText(keyFilePath);
            Assert.Equal(key, content);

            //Should be OK if the same key is added again.
            var keyFilePath2 = await _system.AddSshKeyAsync(username, key, isPrivateKey);
            Assert.Equal(keyFilePath, keyFilePath2);

            var content2 = File.ReadAllText(keyFilePath2);
            Assert.Equal(key, content2);

            //TODO: Test key file ownership and permission ...
        }
        finally
        {
            //The key file should be deleted since it's inside the user's home.
            await DeleteUserAsync(username);
        }
    }
    [Fact]
    public async Task TestAddSshKeyAsync2()
    {
        var key = @"
1
2
3
";
        var username = "testuser1";
        try
        {
            var isNew = await _system.CreateUserAsync(username, "password", false);
            Assert.True(isNew);

            //Add a private key
            var keyFilePath = await _system.AddSshKeyAsync(username, key, true);
            Assert.EndsWith("id_rsa", keyFilePath);

            var content = File.ReadAllText(keyFilePath);
            Assert.Equal(key, content);

            //Followed by a public key
            keyFilePath = await _system.AddSshKeyAsync(username, key, false);
            Assert.EndsWith("id_rsa.pub", keyFilePath);

            content = File.ReadAllText(keyFilePath);
            Assert.Equal(key, content);
        }
        finally
        {
            //The key files should be deleted since they're inside the user's home.
            await DeleteUserAsync(username);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestAddSshKeyAsyncError(bool isPrivateKey)
    {
        var key = "123";
        var username = "testuser1";
        await Assert.ThrowsAsync<Services.SystemException>(async () =>
        {
            await _system.AddSshKeyAsync(username, key, isPrivateKey);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestRemoveSshKeyAsync(bool isPrivateKey)
    {
        var key = @"
1
2
3
";
        var username = "testuser1";
        try
        {
            var isNew = await _system.CreateUserAsync(username, "password", false);
            Assert.True(isNew);

            var keyFilePath = await _system.AddSshKeyAsync(username, key, isPrivateKey);
            Assert.True(File.Exists(keyFilePath));

            var path = await _system.RemoveSshKeyAsync(username, isPrivateKey);
            Assert.Equal(keyFilePath, path);
            Assert.False(File.Exists(keyFilePath));

            //Remove it again
            path = await _system.RemoveSshKeyAsync(username, isPrivateKey);
            Assert.Null(path);
            Assert.False(File.Exists(keyFilePath));
        }
        finally
        {
            await DeleteUserAsync(username);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestRemoveSshKeyAsyncError(bool isPrivateKey)
    {
        await Assert.ThrowsAsync<Services.SystemException>(async () =>
        {
            var username = "testuser1";
            var path = await _system.RemoveSshKeyAsync(username, isPrivateKey);
            Assert.Null(path);
        });
    }

    [Theory]
    [InlineData("key")]
    [InlineData("key2\n")]
    [InlineData("key3\n\n")]
    public async Task TestAddAuthorizedKeyAsync(string key)
    {
        var username = "testuser1";
        try
        {
            var isNew = await _system.CreateUserAsync(username, "password", false);
            Assert.True(isNew);

            var keyFile = await _system.AddAuthorizedKeyAsync(username, key);
            var lines = await File.ReadAllLinesAsync(keyFile);
            Assert.NotNull(lines);
            Assert.Single(lines);
            Assert.Equal(key.TrimEnd(), lines[0]);
        }
        finally
        {
            await DeleteUserAsync(username);
        }
    }

    [Fact]
    public async Task TestAddAuthorizedKeyAsync2()
    {
        var username = "testuser1";
        try
        {
            var isNew = await _system.CreateUserAsync(username, "password", false);
            Assert.True(isNew);

            string? keyFile = null;
            var keys = new string[] { "key1", "key2\n", "key3\n\n" };
            foreach (var key in keys)
            {
                keyFile = await _system.AddAuthorizedKeyAsync(username, key);
            }

            var lines = await File.ReadAllLinesAsync(keyFile!);
            Assert.NotNull(lines);
            Assert.Equal(keys.Length, lines.Length);

            for (var i = 0; i < keys.Length; i++)
            {
                Assert.Equal(keys[i].TrimEnd(), lines[i]);
            }
        }
        finally
        {
            await DeleteUserAsync(username);
        }
    }

    [Fact]
    public async Task TestRemoveAuthorizedKeyAsync()
    {
        var username = "testuser1";
        try
        {
            var isNew = await _system.CreateUserAsync(username, "password", false);
            Assert.True(isNew);

            //Remove a key while the authorized key file doesn't exist yet.
            var keyFile0 = await _system.RemoveAuthorizedKeyAsync(username, "NonExistedKey");
            Assert.Null(keyFile0);

            string? keyFile = null;
            var keys = new string[] { "key1", "key2", "key3" };
            foreach (var key in keys)
            {
                keyFile = await _system.AddAuthorizedKeyAsync(username, key);
            }
            var keyFileContent = await File.ReadAllTextAsync(keyFile!);

            //Remove a non-existed key
            var keyFile2 = await _system.RemoveAuthorizedKeyAsync(username, "NonExistedKey");
            Assert.Equal(keyFile, keyFile2);

            var keyFileContent2 = await File.ReadAllTextAsync(keyFile2!);
            Assert.Equal(keyFileContent, keyFileContent2);

            //Remove existed keys
            var indexes = new int[] { 1, 0, 2 };
            var count = indexes.Length;
            foreach (var i in indexes)
            {
                var key = keys[i];
                var file = await _system.RemoveAuthorizedKeyAsync(username, key);
                count--;
                Assert.Equal(keyFile, file);

                var fileLines = await File.ReadAllLinesAsync(file!);
                Assert.NotNull(fileLines);
                Assert.Equal(count, fileLines.Length);
                Assert.DoesNotContain(key, fileLines);
            }
        }
        finally
        {
            await DeleteUserAsync(username);
        }
    }

    [Fact]
    public async Task TestRemoveAuthorizedKeyAsyncError()
    {
        await Assert.ThrowsAsync<Services.SystemException>(async () =>
        {
            var username = "testuser1";
            var key = "key";
            await _system.RemoveAuthorizedKeyAsync(username, key);
        });
    }

    [Fact]
    public async Task TestGenerateSshPublicKeyAsync()
    {
        try
        {
            var test = @"
ssh-keygen -f /tmp/id_rsa -N ''
";
            var result = await _system.ExecuteInShellAsync(test, null, "\n\n");
            Assert.Equal(0, result.ExitCode);

            var publicKeyExpected = await File.ReadAllTextAsync("/tmp/id_rsa.pub");
            var publicKey = await _system.GenerateSshPublicKeyAsync("/tmp/id_rsa");
            Assert.Equal(publicKeyExpected, publicKey);
        }
        finally
        {
            File.Delete("/tmp/id_rsa");
            File.Delete("/tmp/id_rsa.pub");
        }
    }

    [Fact]
    public async Task TestGenerateSshPublicKeyAsyncError()
    {
        try
        {
            var test = @"
echo abc > /tmp/xyz
";
            var result = await _system.ExecuteInShellAsync(test);
            Assert.Equal(0, result.ExitCode);

            await Assert.ThrowsAsync<Services.SystemException>(async () =>
            {
                await _system.GenerateSshPublicKeyAsync("/tmp/xyz");
            });
        }
        finally
        {
            File.Delete("/tmp/xyz");
        }

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _system.GenerateSshPublicKeyAsync("/invalid/path");
        });
    }

    [Fact]
    public async Task TestMakeTempDirectoryAsync()
    {
        var username = "testuser1";
        string? path = null;
        try
        {
            var isNew = await _system.CreateUserAsync(username, "password", false);
            Assert.True(isNew);

            var prefix = "/tmp/xyz_";
            var template = prefix + "XXX";
            path = await _system.MakeTempDirectoryAsync(username, template);
            Assert.StartsWith(prefix, path);
            Assert.NotEqual(template, path);
            Assert.Equal(template.Length, path.Length);

            var test = @"
set -ex
user=$1
path=$2
owner=$(stat -Lc ""%U"" ""$path"")
[[ $owner == $user ]] || exit 1
perms=$(stat -Lc ""%a"" ""$path"")
(( $perms == 700 )) || exit 2
";
            var result = await _system.ExecuteInShellAsync(test, ["test", username, path]);
            if (result.ExitCode != 0)
            {
                _output.WriteLine(result.ToString());
            }
            Assert.Equal(0, result.ExitCode);
        }
        finally
        {
            await DeleteUserAsync(username);
            if (path != null)
            {
                Directory.Delete(path, true);
            }
        }
    }

    [Fact]
    public async Task TestMakeTempDirectoryAsyncError()
    {
        var username = "testuser1";
        string? path = null;
        try
        {
            var isNew = await _system.CreateUserAsync(username, "password", false);
            Assert.True(isNew);

            var prefix = "/tmp/xzy_";
            var template = prefix + "XX";

            await Assert.ThrowsAsync<Services.SystemException>(async () =>
            {
                path = await _system.MakeTempDirectoryAsync(username, template);
            });
        }
        finally
        {
            await DeleteUserAsync(username);
            if (path != null)
            {
                Directory.Delete(path, true);
            }
        }
    }

    [Fact]
    public void TestParseProcMemInfoContent()
    {
        string[] lines =
        [
            "MemTotal:        10 kB",
            "MemFree:         1 kB",
            "MemAvailable:    2 kB",
            "Buffers:         3 kB",
        ];

        var result = _system.ParseProcMemInfoContent(lines);
        Assert.Equal((ulong)10, result.Total);
        Assert.Equal((ulong)2, result.Available);
    }

    [Fact]
    public async Task TestGetMemoryUsageAsync()
    {
        var result = await _system.GetMemoryUsageAsync();
        Assert.True(result.Total >= 0);
        Assert.True(result.Available >= 0);
    }

    [Fact]
    public void TestParseProcCpuInfoContent()
    {
        var lines = new string[]
        {
            "processor	     : 0",
            "physical id     : 0",
            "processor       : 1",
            "physical id     : 0",
            "...             : .",
            "processor       : 2",
            "physical id     : 0",
            "processor       : 3",
            "...             : .",
        };

        var result = _system.ParseProcCpuInfoContent(lines);
        Assert.Equal(4, result.Cores);
        Assert.Equal(1, result.Sockets);
    }

    [Fact]
    public async Task TestGetCpuInfoAsync()
    {
        var result = await _system.GetCpuInfoAsync();
        Assert.True(result.Cores > 0);
        Assert.True(result.Sockets > 0);
    }

    [Fact]
    public async Task TestGetDistroInfoAsync()
    {
        var result = await _system.GetDistroInfoAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public void TestGetNetworkInfo()
    {
        var result = _system.GetNetworkInfo();
        Assert.NotNull(result);

        foreach (var info in result)
        {
            Assert.NotNull(info.Name);
        }
    }

    [SkippableFact]
    public async Task TestGetGpuInfoAsync()
    {
        var isSupportGpu = await IsGpuSupported();
        Skip.IfNot(isSupportGpu);

        var result = await _system.GetGpuInfoAsync();
        Assert.NotNull(result);
        foreach (var info in result)
        {
            Assert.NotNull(info.Name);
            Assert.NotNull(info.Uuid);
            Assert.NotNull(info.PciBusId);
            Assert.NotNull(info.PciBusDevice);
            Assert.True(info.TotalMemory >= 0);
            Assert.True(info.MaxSMClock >= 0);
            Assert.True(info.FanSpeed >= 0);
            Assert.True(info.UsedMemoryMB >= 0);
            Assert.True(info.PowerWatt >= 0);
            Assert.True(info.CurrentSMClock > 0);
            Assert.True(info.Temperature >= 0);
            Assert.True(info.GpuUtilization >= 0);
        }
    }

    [Fact]
    public void TestParseGpuInfoContent()
    {
        var lines = new string[]
        {
            "Tesla V100-PCIE-16GB, GPU-0b937386-446c-7655-29da-f7e79b729e13, 00000001:00:00.0, 0x1DB410DE, 16384 MiB, 1380 MHz, [N/A], 0 MiB, 22.57 W, 135 MHz, 27, 0 %",
        };

        var result = _system.ParseGpuInfoContent(lines).ToList();
        Assert.Single(result);

        Assert.Equal("Tesla V100-PCIE-16GB", result[0].Name);
        Assert.Equal("GPU-0b937386-446c-7655-29da-f7e79b729e13", result[0].Uuid);
        Assert.Equal("00000001:00:00.0", result[0].PciBusId);
        Assert.Equal("0x1DB410DE", result[0].PciBusDevice);
        Assert.Equal(16384, result[0].TotalMemory);
        Assert.Equal(1380, result[0].MaxSMClock);
        Assert.Equal(0, result[0].FanSpeed);
        Assert.Equal(0, result[0].UsedMemoryMB);
        Assert.Equal(22.57, result[0].PowerWatt, 1e-2);
        Assert.Equal(135, result[0].CurrentSMClock);
        Assert.Equal(27, result[0].Temperature);
        Assert.Equal(0, result[0].GpuUtilization);
    }

    [SkippableFact]
    public async Task TestInitializeGpuDriverAsync()
    {
        var isSupportGpu = await IsGpuSupported();
        Skip.IfNot(isSupportGpu);

        await _system.InitializeGpuDriverAsync();
    }
}
