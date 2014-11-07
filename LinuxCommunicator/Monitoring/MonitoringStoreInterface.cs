 //-------------------------------------------------------------------------------------------------
// <copyright file="MonitoringStoreInterface.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// 
// <summary>
//    Definition of the IHpcMonitoringStore interface and associated types used
//    to manage performance counters for the cluster.
// </summary>
//-------------------------------------------------------------------------------------------------

/// <summary>
/// The monitoring namespace.
/// </summary>
namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    using System;
    using System.Data;
    using System.Collections.Generic;

    /// <summary>
    /// Structure of a unique metric id
    /// </summary>
    [Serializable]
    public struct UMID
    {
        /// <summary>
        /// The metric id.
        /// </summary>
        public UInt16 MetricId;

        /// <summary>
        /// The instance of the metric.
        /// </summary>
        public UInt16 InstanceId;

        /// <summary>
        /// Constructor
        /// </summary>
        public UMID(int metricId, int instanceId)
        {
            this.MetricId = (UInt16)metricId;
            this.InstanceId = (UInt16)instanceId;
        }
    }

  
}
