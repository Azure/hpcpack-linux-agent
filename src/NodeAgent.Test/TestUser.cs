using NodeAgent.Services;
using Xunit.Abstractions;

namespace NodeAgent.Test;

public class TestUser : IDisposable
{
    private const string Letters = "abcdefghijklmnopqrstuvwxyz0123456789";
    private ISystemService _system;
    private ITestOutputHelper? _output;

    public static string RandomName => $"user_{new string(Random.Shared.GetItems<char>(Letters, 6))}";

    public string Name { get; private set; }

    public bool? IsNew {  get; private set; }

    public TestUser(ISystemService system, ITestOutputHelper? output = null)
    {
        _system = system;
        _output = output;
        Name = RandomName;
        IsNew = _system.CreateUserAsync(Name, "password", false).Result;
    }

    public void Dispose()
    {
        _system.DeleteUserAsync(Name, _output).Wait();
    }
}
