using System.Runtime.Versioning;
using static NodeAgent.Utils.System;

namespace NodeAgent.Test.Utils;

[SupportedOSPlatform("linux")]
public class SystemTest
{
    [Fact]
    public void TestExecuteInShellAsync()
    {
        var (code, stdout, stderr) = ExecuteInShell("echo -n back");
        Assert.Equal(0, code);
        Assert.Equal("back", stdout);
        Assert.Equal("", stderr);
    }
}
