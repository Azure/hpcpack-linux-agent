using NodeAgent.Services;

namespace NodeAgent.Test.Servcies;

public class ConfigManagerTest
{
    [Fact]
    public void TestNonexistedConfigFile()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            var configManager = new ConfigManager(null, "nonexisted-file");
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
                var configManager = new ConfigManager(null, filepath);
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
                var configManager = new ConfigManager(null, filepath);
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
    "HeartbeatUri": "abc",
    "RegisterUri": "a",
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
            var configManager = new ConfigManager(null, filename);
            Assert.Equal("abc", configManager.Config.HeartbeatUri);
            Assert.NotEmpty(configManager.Config.RegisterUri);
            Assert.NotEmpty(configManager.Config.NamingServiceUri);
            Assert.NotEmpty(configManager.Config.DefaultServiceName);
            Assert.NotEmpty(configManager.Config.UdpMetricServiceName);

            //Change and save
            configManager.Config.HeartbeatUri = "xyz";
            configManager.SaveConfig();

            //NOTE: An absolute path is passed in here.
            configManager = new ConfigManager(null, filepath);
            Assert.Equal("xyz", configManager.Config.HeartbeatUri);
        }
        finally
        {
            File.Delete(filepath);
        }
    }
}
