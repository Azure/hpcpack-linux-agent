using NodeAgent.Models;

namespace NodeAgent.Utils
{
    public class GpuUsage
    {
        class GpuEqName : EqualityComparer<ExtendedGpuInfo>
        {
            public override bool Equals(ExtendedGpuInfo? info1, ExtendedGpuInfo? info2)
            {
                if (info1 == null || info2 == null)
                {
                    return false;
                }
                else
                {
                    return string.Equals(info1.Name, info2.Name, StringComparison.InvariantCultureIgnoreCase);
                }
            }

            public override int GetHashCode(ExtendedGpuInfo info) => info.Name!.GetHashCode();
        }

        public List<ExtendedGpuInfo> GpuInfo { get; private set; } = [];

        public List<string> GpuInstanceNames { get; private set; } = [];

        public void Update(List<ExtendedGpuInfo> updatedGpuInfo)
        {
            if (!GpuInfo.SequenceEqual(updatedGpuInfo, new GpuEqName()))
            {
                GpuInstanceNames.Clear();
                int idx = 0;
                foreach (var gpuInfo in updatedGpuInfo)
                {
                    GpuInstanceNames.Add($"{gpuInfo.Name}({++idx})");
                }
            }
            GpuInfo = updatedGpuInfo;
        }

        public float Utilization { get => GpuInfo.Average(x => x.GpuUtilization); }

        public float FanSpeed { get => GpuInfo.Count > 0 ? GpuInfo[0].FanSpeed : 0.0f; }

        public float UsedMemoryMB { get => GpuInfo.Sum(x => x.UsedMemoryMB); }

        public float TotalMemoryMB { get => GpuInfo.Sum(x => x.TotalMemory); }

        public float UsedMemoryPercentage { get => 100 * (UsedMemoryMB / TotalMemoryMB); }

        public float PowerWatt { get => GpuInfo.Sum(x => x.PowerWatt); }

        public float CurrentSMClock { get => GpuInfo.Average(x => x.CurrentSMClock); }

        public float Temperature { get => GpuInfo.Average(x => x.Temperature); }
    }
}
