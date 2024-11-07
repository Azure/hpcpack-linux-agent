using Microsoft.Extensions.Logging;
using NodeAgent.Services;
using System.Runtime.Versioning;

namespace NodeAgent.Test.Servcies;

[SupportedOSPlatform("linux")]
public class SystemServiceTest : IDisposable
{
    private ILoggerFactory _loggerFactory;
    private SystemService _system;

    public SystemServiceTest()
    {
        _loggerFactory = LoggerFactory.Create(_ => { });
        var logger = _loggerFactory.CreateLogger<SystemService>();
        _system = new SystemService(logger);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [Fact]
    public async Task TestHostName()
    {
        var hostname = _system.HostName;
        var (code, stdout, _) = await _system.ExecuteInShellAsync("hostname");
        Assert.Equal(0, code);
        Assert.Equal(stdout.TrimEnd('\n'), hostname);
    }

    [Fact]
    public async Task TestExecuteInShellAsync()
    {
        //Output a multi-line string with a EOL
        var (code, stdout, stderr) = await _system.ExecuteInShellAsync(@"printf 'a\nb\n\nc\n'");
        Assert.Equal(0, code);
        Assert.Equal("a\nb\n\nc\n", stdout);
        Assert.Equal("", stderr);

        //Output a multi-line string without a EOL
        //Note the trailing "\n" in stdout: this is by design.
        (code, stdout, stderr) = await _system.ExecuteInShellAsync(@"printf 'a\nb\n\nc'");
        Assert.Equal(0, code);
        Assert.Equal("a\nb\n\nc\n", stdout);
        Assert.Equal("", stderr);

        //Execute a multi-line command
        var cmd = @"
echo abc
echo xyz >&2
exit 1
";
        (code, stdout, stderr) = await _system.ExecuteInShellAsync(cmd);
        Assert.Equal(1, code);
        Assert.Equal("abc\n", stdout);
        Assert.Equal("xyz\n", stderr);

        //Execute a command with arguments
        cmd = @"
echo $0
echo $1
";
        (code, stdout, stderr) = await _system.ExecuteInShellAsync(cmd, ["abc", "xyz"]);
        Assert.Equal(0, code);
        Assert.Equal("abc\nxyz\n", stdout);
        Assert.Equal("", stderr);

        //Execute command with input as stdin
        //Note the trailing "\n" in stdout: this is by design.
        var input = "hello";
        (code, stdout, _) = await _system.ExecuteInShellAsync(@"cat", null, input);
        Assert.Equal(0, code);
        Assert.Equal($"{input}\n", stdout);
    }

    /*
     * NOTE
     *
     * Linux OS permissons on user operations are required to do the test.
     */
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

            var (code, stdout, stderr) = await _system.ExecuteInShellAsync(test, ["test", username, isAdmin ? "1" : "0"]);
            Assert.Equal(0, code);

            isNew = await _system.CreateUserAsync(username, password, isAdmin);
            Assert.False(isNew);
        }
        finally
        {
            await _system.DeleteUserAsync(username);
        }
    }

}
