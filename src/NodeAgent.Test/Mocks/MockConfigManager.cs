using NodeAgent.Models;
using NodeAgent.Services;

namespace NodeAgent.Test.Mocks;

public class MockConfigManager : IConfigManager
{
    private NodeManagerConfig _config = new NodeManagerConfig();

    public NodeManagerConfig Config => _config;

    public void SaveConfig()
    {
        throw new NotSupportedException();
    }
}
