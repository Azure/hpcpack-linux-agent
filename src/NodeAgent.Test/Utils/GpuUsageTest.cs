using NodeAgent.Models;
using NodeAgent.Utils;

namespace NodeAgent.Test.Utils;

public class GpuUsageTest
{
    [Fact]
    public void TestUpdateAndProperty()
    {
        var gpuUsage = new GpuUsage();
        var updatedGpuInfo = new List<ExtendedGpuInfo>()
        {
            new()
            {
                Name = "GPU1",
                GpuUtilization = 10,
                FanSpeed = 20,
                UsedMemoryMB = 30,
                TotalMemory = 40,
                PowerWatt = 50,
                CurrentSMClock = 60,
                Temperature = 70,
            },
            new()
            {
                Name = "GPU2",
                GpuUtilization = 20,
                FanSpeed = 30,
                UsedMemoryMB = 40,
                TotalMemory = 50,
                PowerWatt = 60,
                CurrentSMClock = 70,
                Temperature = 80,
            },
        };

        gpuUsage.Update(updatedGpuInfo);

        Assert.Equal(2, gpuUsage.GpuInstanceNames.Count);
        Assert.Equal("GPU1(1)", gpuUsage.GpuInstanceNames[0]);
        Assert.Equal("GPU2(2)", gpuUsage.GpuInstanceNames[1]);
        Assert.Equal(15, gpuUsage.Utilization);
        Assert.Equal(20, gpuUsage.FanSpeed);
        Assert.Equal(70, gpuUsage.UsedMemoryMB);
        Assert.Equal(90, gpuUsage.TotalMemoryMB);
        Assert.Equal(77.77777777777777, gpuUsage.UsedMemoryPercentage, 1e-5);
        Assert.Equal(110, gpuUsage.PowerWatt);
        Assert.Equal(65, gpuUsage.CurrentSMClock);
        Assert.Equal(75, gpuUsage.Temperature);
    }
}