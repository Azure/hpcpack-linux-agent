using System.Runtime.Versioning;

namespace NodeAgent.Services;

public interface ISystemService
{
    string HostName { get; }

    /*
     * Execute a command line in "/bin/sh".
     * Return a tuple of exit code, stdout and stderr of the command.
     * Throw an exception if anyting wrong (the exit code of the command is not considered for raising exception).
     */
    Task<Tuple<int, string, string>> ExecuteInShellAsync(string cmd, CancellationToken cancellationToken = default);

    /*
     * Return true when a new user is created, false when the user already exists.
     * Throw an exception if anything wrong.
     */
    Task<bool> CreateUserAsync(string username, string? password, bool isAdmin, CancellationToken cancellationToken = default);

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
    public string HostName => Utils.System.HostName;

    [SupportedOSPlatform("linux")]
    public Task<Tuple<int, string, string>> ExecuteInShellAsync(string cmd, CancellationToken cancellationToken = default)
    {
        return Utils.System.ExecuteInShellAsync(cmd, cancellationToken);
    }

    public Task<bool> CreateUserAsync(string username, string? password, bool isAdmin, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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
