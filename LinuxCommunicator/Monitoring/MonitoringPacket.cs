//-------------------------------------------------------------------------------------------------
// <copyright file="UDPServer.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//
// <summary>
//    Class the implements the common data structure for Monitoring UDP communication channel
// </summary>
//-------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    /// <summary>
    /// On wire representation of the packet.
    /// </summary>
    public struct CounterWirePacket
    {
        public int version;
        public Guid uuid;
        public int count;
        public int tickCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
        public UMID[] umids;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
        public float[] values;
    }
}
