using NodeAgent.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface IConfigManager
{
    NodeManagerConfig Config { get; }

    void SaveConfig();
}

public class ConfigManager : IConfigManager
{
    public const string DefaultConfigFile = "nodemanager.json";

    private ILogger? _logger;

    private string _configFilePath;

    private NodeManagerConfig? _config;

    private object _saveLock = new object();

    public ConfigManager(ILogger<ConfigManager>? logger = null, string? configFile = null)
    {
        _logger = logger;
        _configFilePath = configFile ?? DefaultConfigFile;
        if (!Path.IsPathFullyQualified(_configFilePath))
        {
            _configFilePath = Path.GetFullPath(_configFilePath, Directory.GetCurrentDirectory());
        }
        _logger?.LogInformation("Node Manager Configuration file: {file}", _configFilePath);

        ReadConfig();
    }

    private void ReadConfig()
    {
        var content = File.ReadAllText(_configFilePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidDataException($"Config file ${_configFilePath} is empty!");
        }
        _config = JsonSerializer.Deserialize<NodeManagerConfig>(content!);
        if (_config == null)
        {
            throw new InvalidDataException($"Config file ${_configFilePath} is invalid!");
        }
        var validationResults = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(_config, new ValidationContext(_config), validationResults, true);
        if (!isValid)
        {
            foreach (var validationResult in validationResults)
            {
                _logger?.LogError("Node Manager configuration validation error: {error}", validationResult.ErrorMessage);
            }
            throw new InvalidDataException($"Config file ${_configFilePath} contains invalid data!");
        }
    }

    public NodeManagerConfig Config => _config!;

    public void SaveConfig()
    {
        lock (_saveLock)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            var jsonString = JsonSerializer.Serialize(Config, options);
            File.WriteAllText(_configFilePath, jsonString);
        }
    }
}
