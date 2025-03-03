using NodeAgent.Models;
using NodeAgent.Services;

namespace NodeAgent.Test.Servcies;

public class HostsManagerServiceTest
{
    [Fact]
    public void TestUpdateHostEntries()
    {
        var hostEntries = new HostEntry[]
        {
            new() { HostName = "h1", IPAddress = "1.0.0.1" },
            new() { HostName = "n1.h1", IPAddress = "1.1.0.1" },
            new() { HostName = "h2", IPAddress = "1.0.0.2" },
            new() { HostName = "n1.h2", IPAddress = "1.1.0.2" },
        };
        var content = "";
        var expected = """

1.0.0.1 h1 #HPC
1.0.0.2 h2 #HPC
1.1.0.1 n1.h1 #HPC
1.1.0.2 n1.h2 #HPC

""".Replace("\r\n", "\n");

        var result = HostsManagerService.UpdateHostEntries(content, hostEntries);
        Assert.Equal(expected, result);

        content = """

# Some comment
  1.2.3.4  host

2.3.4.5  # Some comment

1.0.0.0     #HPC
1.0.0.1 h    #HPC
""".Replace("\r\n", "\n");

        expected = """

# Some comment
  1.2.3.4  host

2.3.4.5  # Some comment

1.0.0.1 h1 #HPC
1.0.0.2 h2 #HPC
1.1.0.1 n1.h1 #HPC
1.1.0.2 n1.h2 #HPC

""".Replace("\r\n", "\n");

        result = HostsManagerService.UpdateHostEntries(content, hostEntries);
        Assert.Equal(expected, result);
    }
}
