
using Microsoft.Extensions.Logging;
using NodeAgent.Services;

namespace NodeAgent.Test.Servcies;

public class ConfigManagerTest
{
    [Fact]
    public void TestNonexistedConfigFile()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var logger = loggerFactory.CreateLogger<ConfigManager>();

        Assert.Throws<FileNotFoundException>(() =>
        {
            var configManager = new ConfigManager(logger, "nonexisted-file");
        });
    }

    [Fact]
    public void TestEmptyConfigFile()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var logger = loggerFactory.CreateLogger<ConfigManager>();

        var filename = "empty-file";
        var filepath = Path.GetFullPath(filename, Directory.GetCurrentDirectory());
        File.WriteAllText(filepath, "   ");

        try
        {
            Assert.Throws<InvalidDataException>(() =>
            {
                var configManager = new ConfigManager(logger, filepath);
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
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var logger = loggerFactory.CreateLogger<ConfigManager>();

        var json = @"
{
""HeartbeatUri"": ""abc""
}
";
        var filename = "valid-file";
        var filepath = Path.GetFullPath(filename, Directory.GetCurrentDirectory());
        File.WriteAllText(filepath, json);

        try
        {
            //NOTE: Only filename is passed in here.
            var configManager = new ConfigManager(logger, filename);
            Assert.Equal("abc", configManager.Config.HeartbeatUri);

            //Change and save
            configManager.Config.HeartbeatUri = "xyz";
            configManager.SaveConfig();

            //NOTE: An absolute path is passed in here.
            configManager = new ConfigManager(logger, filepath);
            Assert.Equal("xyz", configManager.Config.HeartbeatUri);
        }
        finally
        {
            File.Delete(filepath);
        }
    }
}
