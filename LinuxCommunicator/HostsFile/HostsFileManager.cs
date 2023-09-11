using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.HostsFile
{
    /// <summary>
    /// This class abstracts a hosts file so that is can be managed by HPC.
    /// </summary>
    public class HostsFileManager : IDisposable
    {
        /// <summary>
        /// The string placed in the comment field of an entry managed
        /// by the cluster.
        /// </summary>
        public const string ManagedEntryKey = "HPC";

        /// <summary>
        /// The parameter name used to indicate whether the hosts file is HPC managed.
        /// </summary>
        public const string ManageFileParameter = "ManageFile";

        /// <summary>
        /// The interval to reload hosts file
        /// </summary>
        private const int ReloadInterval = 1000 * 60;

        /// <summary>
        /// Regular expression for a comment line in the text file
        /// </summary>
        private static readonly Regex Comment = new Regex(@"^#(?<1>.*)$");

        /// <summary>
        /// Regular expression for a parameter value in the comments section.
        /// </summary>
        private static readonly Regex CommentParameter = new Regex(@"^#\s*(?<parameter>[\w\p{Lm}\p{Nl}\p{Cf}\p{Mn}\p{Mc}\.]+)\s*=\s*(?<value>[\w\p{Lm}\p{Nl}\p{Cf}\p{Mn}\p{Mc}\.]+)");

        /// <summary>
        /// Regular expression for a ip entry in the text file.
        /// </summary>
        private static readonly Regex IpEntry = new Regex(@"^(?<ip>[0-9\.]+)\s+(?<dnsName>[^\s#]+)(\s+#(?<comment>.*))?");

        /// <summary>
        /// The path to the hosts file
        /// </summary>
        private string filepath;

        /// <summary>
        /// The timer to reload hosts file
        /// </summary>
        private Timer reloadTimer;

        /// <summary>
        /// The last modified date of the current loaded hosts file
        /// </summary>
        private DateTime lastModified = DateTime.MinValue;

        /// <summary>
        /// The unique update ID
        /// </summary>
        public Guid UpdateId
        {
            get;
            private set;
        }

        /// <summary>
        /// This constructor initializes the parser on a specific hosts file
        /// </summary>
        public HostsFileManager()
        {
            this.filepath = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
            this.ManagedEntries = new List<HostEntry>();
            this.reloadTimer = new Timer(ReloadTimerEvent, null, 0, Timeout.Infinite);
        }

        /// <summary>
        /// Gets the list of HPC managed entries in the host file.
        /// </summary>
        public List<HostEntry> ManagedEntries
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets a flag indicating whether the file is HPC managed
        /// </summary>
        public bool ManagedByHPC
        {
            get;
            private set;
        }

        /// <summary>
        /// Loads the contents of an existing host file. Will clear
        /// the current configuration of this instance.
        /// </summary>
        /// <param name="path"></param>
        public void ReloadTimerEvent(object param)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(this.filepath);
                if (!fileInfo.Exists)
                {
                    LinuxCommunicator.Instance?.Tracer?.TraceInfo("[HostsFileManager] The hosts file doesn't exists: {0}", this.filepath);
                    return;
                }

                if (fileInfo.LastWriteTimeUtc <= this.lastModified)
                {
                    LinuxCommunicator.Instance?.Tracer?.TraceInfo("[HostsFileManager] The hosts file isn't changed since last load");
                    return;
                }

                bool manageByHPC = false;
                Dictionary<string, HostEntry> newEntries = new Dictionary<string, HostEntry>();
                foreach (var line in File.ReadAllLines(this.filepath))
                {
                    Match commentMatch = HostsFileManager.Comment.Match(line);
                    if (commentMatch.Success)
                    {
                        Match commentParameterMatch = HostsFileManager.CommentParameter.Match(line);
                        if (commentParameterMatch.Success && string.Equals(commentParameterMatch.Groups["parameter"].ToString(), ManageFileParameter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.Equals(commentParameterMatch.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase))
                            {
                                manageByHPC = true;
                            }
                        }

                        continue;
                    }

                    Match ipEntryMatch = HostsFileManager.IpEntry.Match(line);
                    HostEntry entry;

                    if (ipEntryMatch.Success &&
                        manageByHPC &&
                        !ipEntryMatch.Groups["ip"].Value.StartsWith(@"169.254") &&
                        ipEntryMatch.Groups["comment"].Value.Equals(HostsFileManager.ManagedEntryKey, StringComparison.OrdinalIgnoreCase) &&
                        HostEntry.TryCreate(ipEntryMatch.Groups["dnsName"].Value, ipEntryMatch.Groups["ip"].Value, out entry))
                    {
                        newEntries[entry.Name] = entry;
                    }
                }

                if (manageByHPC)
                {
                    if (newEntries.Count != this.ManagedEntries.Count || !(new HashSet<HostEntry>(this.ManagedEntries)).SetEquals(new HashSet<HostEntry>(newEntries.Values)))
                    {
                        // Put the <NetworkType>.<NodeName> entries at the end of the list
                        var newHostList = newEntries.Values.Where(entry => !entry.Name.Contains('.')).ToList();
                        newHostList.AddRange(newEntries.Values.Where(entry => entry.Name.Contains('.')));
                        this.ManagedEntries = newHostList;
                        this.UpdateId = Guid.NewGuid();
                        LinuxCommunicator.Instance?.Tracer?.TraceInfo("[HostsFileManager] The managed host entries updated, current update Id is {0}", this.UpdateId);
                    }
                    else
                    {
                        LinuxCommunicator.Instance?.Tracer?.TraceInfo("[HostsFileManager] No update to HPC managed host entries, current update Id is {0}", this.UpdateId);
                    }
                }
                else
                {
                    LinuxCommunicator.Instance?.Tracer?.TraceWarning("[HostsFileManager] Hosts file was not managed by HPC");
                    this.ManagedEntries.Clear();
                }

                this.ManagedByHPC = manageByHPC;
                this.lastModified = fileInfo.LastWriteTimeUtc;
            }
            catch (Exception e)
            {
                LinuxCommunicator.Instance?.Tracer?.TraceWarning("[HostsFileManager] Failed to reload host file: {0}", e);
            }
            finally
            {
                try
                {
                    this.reloadTimer.Change(ReloadInterval, Timeout.Infinite);
                }
                catch (Exception te)
                {
                    LinuxCommunicator.Instance?.Tracer?.TraceWarning("[HostsFileManager] Failed to restart reload timer: {0}", te);
                }
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.reloadTimer?.Dispose();
            }
        }
    }
}
