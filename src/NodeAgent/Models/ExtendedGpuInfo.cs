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

        // TODO: add ExtendedPciBusDevice and process the case when PciBusId is null
    }
}
