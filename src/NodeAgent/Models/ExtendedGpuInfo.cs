namespace NodeAgent.Models
{
    public class ExtendedGpuInfo : GpuInfo
    {
        public float FanSpeed { get; set; }

        public float UsedMemoryMB { get; set; }

        public float PowerWatt { get; set; }

        public float CurrentSMClock { get; set; }

        public float Temperature { get; set; }

        public float GpuUtilization { get; set; }
        public float UsedMemoryPercentage { get => 100 * (UsedMemoryMB / TotalMemory); }

        public string ExtendedPciBusDevice
        { 
            get
            {
                var tokens = PciBusId?.Split('.');
                var ids = tokens?[0].Split(':');
                return $"{ids?[1]}:{ids?[2]}";
            }
        }
    }
}
