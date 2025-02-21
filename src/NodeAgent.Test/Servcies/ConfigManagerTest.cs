using Microsoft.Extensions.Logging;
using NodeAgent.Services;
using Xunit.Abstractions;

namespace NodeAgent.Test.Servcies;

public class ConfigManagerTest : TestBase
{
    private ILogger<ConfigManager> _logger;

    public ConfigManagerTest(ITestOutputHelper output) : base(output)
    {
        _logger = LoggerFactory.CreateLogger<ConfigManager>();
    }

    [Fact]
    public void TestNonexistedConfigFile()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            var configManager = new ConfigManager(_logger, "nonexisted-file");
        });
    }

    [Fact]
    public void TestEmptyConfigFile()
    {
        var filename = "empty-file";
        var filepath = Path.GetFullPath(filename, Directory.GetCurrentDirectory());
        File.WriteAllText(filepath, "   ");

        try
        {
            Assert.Throws<InvalidDataException>(() =>
            {
                var configManager = new ConfigManager(_logger, filepath);
            });
        }
        finally
        {
            File.Delete(filepath);
        }
    }

    [Fact]
    public void TestInvalidConfigFile()
    {
        var json = """
{
    "HeartbeatUri": "abc",
    "RegisterUri": "a"
}
""";
        var filename = "invalid-file";
        var filepath = Path.GetFullPath(filename, Directory.GetCurrentDirectory());
        File.WriteAllText(filepath, json);

        try
        {
            Assert.Throws<InvalidDataException>(() =>
            {
                var configManager = new ConfigManager(_logger, filepath);
            });
        }
        finally
        {
            File.Delete(filepath);
        }
    }

    [Fact]
    public void TestValidConfigFile()
    {
        var json = """
{
    "CertificateChainFile": "a",
    "ListeningUri": "a",
    "HeartbeatUri": "a",
    "RegisterUri": "a",
    "HostsFileUri": "a",
    "NamingServiceUri": ["a"],
    "DefaultServiceName": "a",
    "UdpMetricServiceName": "a"
}
""";
        var filename = "valid-file";
        var filepath = Path.GetFullPath(filename, Directory.GetCurrentDirectory());
        File.WriteAllText(filepath, json);

        try
        {
            //NOTE: Only filename is passed in here.
            var configManager = new ConfigManager(_logger, filename);
            Assert.Equal("a", configManager.Config.HeartbeatUri);
            Assert.NotEmpty(configManager.Config.RegisterUri);
            Assert.NotEmpty(configManager.Config.NamingServiceUri);
            Assert.NotEmpty(configManager.Config.DefaultServiceName);
            Assert.NotEmpty(configManager.Config.UdpMetricServiceName);

            //Change and save
            configManager.Config.HeartbeatUri = "xyz";
            configManager.SaveConfig();

            //NOTE: An absolute path is passed in here.
            configManager = new ConfigManager(_logger, filepath);
            Assert.Equal("xyz", configManager.Config.HeartbeatUri);
        }
        finally
        {
            File.Delete(filepath);
        }
    }
}
