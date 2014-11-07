//-------------------------------------------------------------------------------------------------
// <copyright file="UDPClient.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//
// <summary>
//    Class that manages the sending of UDP data to the Monitoring Server.
// </summary>
//-------------------------------------------------------------------------------------------------

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Runtime.Remoting;
    using System.Runtime.Remoting.Channels;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Diagnostics;

    /// <summary>
    /// Sends perf data to the headnode.
    /// </summary>
    public class CounterDataSender
    {
        /// <summary>
        /// The udp connection we send data through.
        /// </summary>
        private System.Net.Sockets.UdpClient udpClient;

        private object lockObj = new object();

        /// <summary>
        /// The packet version we support
        /// </summary>
        private const int SupportedVersion = 1;

        /// <summary>
        /// The maximum number of counters in a packet
        /// </summary>
        public const int MaxCountersInPacket = 80;

        /// <summary>
        /// The maximum size of a packet.
        /// </summary>
        public const int MaxCounterPacketSize = 1024;

        /// <summary>
        /// The port that the counter data handler listens to.
        /// </summary>
        private int monitoringPort;

        /// <summary>
        /// The head node we are sending data to
        /// </summary>
        private string headNode;

        /// <summary>
        /// Buffer to use to format the counters.
        /// </summary>
        IntPtr formatBuffer = IntPtr.Zero;

        /// <summary>
        /// Buffer to use when sending the counters
        /// </summary>
        byte[] wireBuffer = new byte[CounterDataSender.MaxCounterPacketSize];

        /// <summary>
        /// Counter wire packet
        /// </summary>
        CounterWirePacket packet = new CounterWirePacket();

        /// <summary>
        /// Constructor
        /// </summary>
        public CounterDataSender()
        {
        }

        /// <summary>
        /// Opens the connection.
        /// </summary>
        /// <param name="headNode">The node we are connecting to.</param>
        /// <param name="port">The port to send data to.</param>
        public void OpenConnection(string headNode, int monitoringPort)
        {
            lock (this.lockObj)
            {
                if (this.formatBuffer == IntPtr.Zero)
                {
                    formatBuffer = Marshal.AllocHGlobal(CounterDataSender.MaxCounterPacketSize);
                }

                this.monitoringPort = monitoringPort;
                this.headNode = headNode;

                //
                //  Init packet
                //
                this.packet.version = CounterDataSender.SupportedVersion;
                this.packet.umids = new UMID[CounterDataSender.MaxCountersInPacket];
                this.packet.values = new float[CounterDataSender.MaxCountersInPacket];

                //
                //  Init UDP client
                //
                LinuxCommunicator.Instance.Tracer.TraceInfo("Connecting UDPClient to {0}:{1}", this.headNode, this.monitoringPort);


                bool bUseIPv4 = false;
                if (Socket.OSSupportsIPv4)
                {
                    LinuxCommunicator.Instance.Tracer.TraceInfo("Creating UDP Client for IPv4: ");
                    try
                    {
                        this.udpClient = new UdpClient(AddressFamily.InterNetwork);
                        bUseIPv4 = true;
                    }
                    catch (Exception ex)
                    {
                        LinuxCommunicator.Instance.Tracer.TraceError("[ERROR] Error connecting UDP Client using IPv4: {0}", ex);
                    }
                }

                if (Socket.OSSupportsIPv6 && bUseIPv4 == false)
                {
                    LinuxCommunicator.Instance.Tracer.TraceInfo("Creating UDP Client for IPv6: ");
                    try
                    {
                        this.udpClient = new UdpClient(AddressFamily.InterNetworkV6);
                    }
                    catch (Exception ex)
                    {
                        LinuxCommunicator.Instance.Tracer.TraceError("[ERROR] Error connecting UDP Client using IPv6: {0}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        public void CloseConnection()
        {
            lock (this.lockObj)
            {
                if (this.udpClient != null)
                {
                    try
                    {
                        this.udpClient.Close();
                    }
                    catch (SocketException se)
                    {
                        LinuxCommunicator.Instance.Tracer.TraceError("[ERROR] Exception during CloseConnection, error code: {0}. {1}", se.ErrorCode, se);
                    }

                    this.udpClient = null;
                }

                if (this.formatBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(this.formatBuffer);
                    this.formatBuffer = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Sends a block of counter data to the head node.
        /// </summary>
        public void SendData(Guid nodeId, UMID[] umids, float[] values, int tickCount)
        {
            LinuxCommunicator.Instance.Tracer.TraceDetail("[VERBOSE] Sending data to {0}:{1}", this.headNode, this.monitoringPort);

            for (int i = 0; i < umids.Length; i++)
            {
                LinuxCommunicator.Instance.Tracer.TraceDetail("[VERBOSE] {0}|{1} : {2}", umids[i].MetricId, umids[i].InstanceId, values[i]);
            }

            try
            {
                int countersSent = 0;
                packet.uuid = nodeId;
                packet.tickCount = tickCount;

                while (countersSent < umids.Length)
                {
                    int countersToSend = Math.Min(umids.Length - countersSent, CounterDataSender.MaxCountersInPacket);
                    packet.count = countersToSend;
                    Array.Copy(umids, countersSent, packet.umids, 0, countersToSend);
                    Array.Copy(values, countersSent, packet.values, 0, countersToSend);
                    Marshal.StructureToPtr(packet, this.formatBuffer, false);
                    Marshal.Copy(this.formatBuffer, this.wireBuffer, 0, (int)Marshal.SizeOf(packet));
                    if (Marshal.SizeOf(packet) >= CounterDataSender.MaxCounterPacketSize)
                    {
                        LinuxCommunicator.Instance.Tracer.TraceWarning("[WARNING] Too many counters in the packet, max packet size: {0}, actual packet size {1}", CounterDataSender.MaxCounterPacketSize, Marshal.SizeOf(packet));
                    }

                    // print send data
                    int datasize = Marshal.SizeOf(packet);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < datasize; i++)
                    {
                        sb.Append(this.wireBuffer[i].ToString() + " ");
                    }
                    LinuxCommunicator.Instance.Tracer.TraceDetail("[VERBOSE] Sending data (size={0}) : {1}", datasize, sb.ToString());

                    udpClient.Send(this.wireBuffer, CounterDataSender.MaxCounterPacketSize, this.headNode, this.monitoringPort);
                    countersSent += countersToSend;
                }
            }
            catch (SocketException e)
            {
                LinuxCommunicator.Instance.Tracer.TraceError("[ERROR] Failed to send counter data due to socket exception, error code: {0}, exception {1}", e.ErrorCode, e);

                // re-connect since this exception can happen if Connect is not called
                this.Reconnect();
            }
            catch (InvalidOperationException e1)
            {
                LinuxCommunicator.Instance.Tracer.TraceError("[ERROR] Failed to send counter data due to invalid operation exception {0}", e1);

                // re-connect since we hit following exception in bug 19454:
                // The operation is not allowed on non-connected sockets.
                this.Reconnect();
            }
            catch (Exception e2)
            {
                // use this catch to track any exception that is not caught in above block, then we can
                // analyze the exception and take proper action here (such as reconnect etc).
                LinuxCommunicator.Instance.Tracer.TraceError("[ERROR] Failed to send counter data due to exception {0}", e2);
            }
        }

        private void Reconnect()
        {
            lock (this.lockObj)
            {
                this.CloseConnection();
                this.OpenConnection(this.headNode, this.monitoringPort);
            }
        }
    }
}
