using NodeAgent.Services;
using Xunit.Abstractions;

namespace NodeAgent.Test;

public static class SystemServiceExtensions
{
    public static async Task DeleteUserAsync(this ISystemService system, string username, ITestOutputHelper? output = null)
    {
        var cmd = """userdel -rf "$1" """;
        var result = await system.ExecuteInShellAsync(cmd, [nameof(DeleteUserAsync), username]).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var msg = $"Error when deleting user '{username}': {result}";
            output?.WriteLine(msg);
        }
    }
}
