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

        /*
         * NOTE
         *
         * I don't know why there's a trailing "\n", though "-n" is specified.
         * It seems a character by the .NET API.
         */
        Assert.Equal("back\n", stdout);

        Assert.Equal("", stderr);
    }
}
