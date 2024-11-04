using NodeAgent.Services;
using System.Runtime.Versioning;

namespace NodeAgent.Test.Servcies;

[SupportedOSPlatform("linux")]
public class SystemServiceTest
{
    private SystemService _system = new SystemService();

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
        (code, stdout, stderr) = await _system.ExecuteInShellAsync(@"printf 'a\nb\n\nc'");
        Assert.Equal(0, code);
        Assert.Equal("a\nb\n\nc\n", stdout);
        Assert.Equal("", stderr);
    }
}
