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
        public HostEntry(string name, string ipAddress)
        {
            IPAddress testAddress;
            if (!IPAddress.TryParse(ipAddress, out testAddress))
            {
                throw new ArgumentException("ipAddress must be a valid IP address");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name cannot be null");
            }

            this.Address = ipAddress;
            this.Name = name;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            HostEntry entry = obj as HostEntry;
            if (entry == null)
            {
                return false;
            }

            return string.Equals(this.Name, entry.Name) && string.Equals(this.Address, entry.Address);
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            if ((this.Address != null))
            {
                hashCode = hashCode ^ this.Address.GetHashCode();
            }
            if ((this.Name != null))
            {
                hashCode = hashCode ^ this.Name.GetHashCode();
            }
            return hashCode;
        }
    }
}
