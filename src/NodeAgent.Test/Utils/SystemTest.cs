using System.Runtime.Versioning;
using static NodeAgent.Utils.System;

namespace NodeAgent.Test.Utils;

[SupportedOSPlatform("linux")]
public class SystemTest
{
    [Fact]
    public void TestExecuteInShellAsync()
    {
        //Output a multi-line string with a EOL
        var (code, stdout, stderr) = ExecuteInShell(@"printf 'a\nb\n\nc\n'");
        Assert.Equal(0, code);
        Assert.Equal("a\nb\n\nc\n", stdout);
        Assert.Equal("", stderr);

        //Output a multi-line string without a EOL
        (code, stdout, stderr) = ExecuteInShell(@"printf 'a\nb\n\nc'");
        Assert.Equal(0, code);
        Assert.Equal("a\nb\n\nc\n", stdout);
        Assert.Equal("", stderr);
    }
}
