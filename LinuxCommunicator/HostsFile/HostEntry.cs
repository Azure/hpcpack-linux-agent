using System;
using System.Net;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.HostsFile
{
    public class HostEntry
    {
        /// <summary>
        /// The host name
        /// </summary>
        public string Name;

        /// <summary>
        /// The host IP address
        /// </summary>
        public string Address;

        /// <summary>
        /// Creates a new hosts file entry for the specified host.
        /// </summary>
        /// <param name="name">The host name</param>
        /// <param name="ipAddress">The IP address</param>
        private HostEntry(string name, string ipAddress)
        {
 
            this.Address = ipAddress;
            this.Name = name;
        }

        public static bool TryCreate(string name, string ipAddress, out HostEntry entry)
        {
            IPAddress testAddress;
            if (!IPAddress.TryParse(ipAddress, out testAddress) || string.IsNullOrEmpty(name))
            {
                entry = null;
                return false;
            }

            entry = new HostEntry(name, ipAddress);
            return true;
        }

        public override bool Equals(object obj)
        {
            HostEntry entry = obj as HostEntry;
            if (entry == null)
            {
                return false;
            }

            return string.Equals(this.Name, entry.Name) && string.Equals(this.Address, entry.Address);
        }

        public override int GetHashCode()
        {
            return (this.Address ?? string.Empty).GetHashCode() ^ (this.Name ?? string.Empty).GetHashCode();
        }
    }
}
