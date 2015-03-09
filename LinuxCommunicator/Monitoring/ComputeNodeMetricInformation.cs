using System;
using System.Collections.Generic;
using Microsoft.Hpc.Activation;
using System.Runtime.Serialization;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    /// <summary>
    /// Structure of a unique metric id
    /// </summary>
    [Serializable]
    [DataContract]
    public class ComputeNodeMetricInformationUMID
    {
        /// <summary>
        /// The metric id.
        /// </summary>
        [DataMember]
        public UInt16 MetricId { get; set; }

        /// <summary>
        /// The instance id.
        /// </summary>
        [DataMember]
        public UInt16 InstanceId { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ComputeNodeMetricInformationUMID(ushort metricId, ushort instanceId)
        {
            this.MetricId = metricId;
            this.InstanceId = instanceId;
        }

        public ComputeNodeMetricInformationUMID()
        {
            this.MetricId = 0;
            this.InstanceId = 0;
        }

        public UMID ToUMID()
        {
            return new UMID(this.MetricId, this.InstanceId);
        }
    }

    [Serializable]
    [DataContract]
    public class ComputeNodeMetricInformation
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Time { get; set; }

        [DataMember]
        public List<ComputeNodeMetricInformationUMID> Umids { get; set; }

        [DataMember]
        public List<float> Values { get; set; }

        [DataMember]
        public int TickCount { get; set; }

        public ComputeNodeMetricInformation()
        {
        }

        public UMID[] GetUmids()
        {
            List<UMID> list = new List<UMID>(Umids.Count);
            foreach(var v in Umids){
                list.Add(v.ToUMID());
            }
            return list.ToArray();
        }

        public float[] GetValues()
        {
            return Values.ToArray();
        }
        
        public string IpAddress { get; set; }

        public int CoreCount { get; set; }

        public int SocketCount { get; set; }

        public int MemoryMegabytes { get; set; }
    }
}
