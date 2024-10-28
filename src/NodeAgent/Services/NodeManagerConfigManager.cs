using NodeAgent.Models;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface INodeManagerConfigManager
{
    NodeManagerConfig Config { get; }

    Task SaveConfigAsync();
}

public class NodeManagerConfigManager : INodeManagerConfigManager
{
    public const string DefaultConfigFile = "nodemanager.json";

    private ILogger _logger;

    private string _configFile;

    //TODO: We may need configFilePath instead of configFile ...
    public NodeManagerConfigManager(ILogger<NodeManagerConfigManager> logger, string? configFile = null)
    {
        _logger = logger;
        _configFile = configFile ?? DefaultConfigFile;

        //TODO: Load config from configFile ...
    }

    public NodeManagerConfig Config => throw new NotImplementedException();

    public Task SaveConfigAsync()
    {
        throw new NotImplementedException();
    }
}
