using NodeAgent.Services;
using Xunit.Abstractions;

namespace NodeAgent.Test;

public class TestUser : IDisposable
{
    private ISystemService _system;
    private ITestOutputHelper? _output;

    public string Name { get; private set; }

    public bool? IsNew {  get; private set; }

    public TestUser(string name, ISystemService system, ITestOutputHelper? output = null)
    {
        _system = system;
        _output = output;
        Name = name;
        IsNew = _system.CreateUserAsync(Name, "password", false).Result;
    }

    public void Dispose()
    {
        _system.DeleteUserAsync(Name, _output).Wait();
    }
}
