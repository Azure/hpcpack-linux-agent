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
        /// The interval to reload hosts file
        /// </summary>
        private const int ReloadTInterval = 1000 * 60;

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
        /// The ip address entries in the host file.
        /// </summary>
        private List<HostEntry> managedEntries = new List<HostEntry>();

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
        /// <param name="filepath"></param>
        public HostsFileManager(string filepath)
        {
            this.filepath = filepath;
            this.reloadTimer = new Timer(new TimerCallback(ReloadTimerEvent), null, 0, Timeout.Infinite);
        }

        /// <summary>
        /// Gets the list of HPC managed entries in the host file.
        /// </summary>
        public List<HostEntry> ManagedEntries
        {
            get { return this.managedEntries; }
        }

        /// <summary>
        /// Gets or sets a flag indicating whether the file is HPC managed
        /// </summary>
        public Boolean ManageFile
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
                if(!fileInfo.Exists)
                {
                    LinuxCommunicator.Instance.Tracer.TraceInfo("[HostsFileManager] The hosts file doesn't exists: {0}", this.filepath);
                    return;
                }

                if(fileInfo.LastWriteTimeUtc <= this.lastModified)
                {
                    LinuxCommunicator.Instance.Tracer.TraceInfo("[HostsFileManager] The hosts file isn't changed since last load");
                    return;
                }

                bool manageFile = false;
                List<HostEntry> newEntries = new List<HostEntry>();
                using (StreamReader file = File.OpenText(this.filepath))
                {
                    string line = file.ReadLine();
                    while (line != null)
                    {
                        Match commentMatch = HostsFileManager.Comment.Match(line);
                        if (commentMatch.Success)
                        {
                            // throw away comments for the moment.
                        }

                        Match manageFileMatch = HostsFileManager.CommentParameter.Match(line);
                        if (manageFileMatch.Success)
                        {
                            if (string.Compare(manageFileMatch.Groups["parameter"].ToString(), "ManageFile", StringComparison.OrdinalIgnoreCase) == 0
                                && string.Compare(manageFileMatch.Groups["value"].ToString(), "true", StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                manageFile = true;
                            }
                        }

                        Match ipEntryMatch = HostsFileManager.IpEntry.Match(line);
                        if (ipEntryMatch.Success && manageFile)
                        {
                            string ip = ipEntryMatch.Groups["ip"].ToString();
                            string name = ipEntryMatch.Groups["dnsName"].ToString();
                            string comment = ipEntryMatch.Groups["comment"].ToString();

                            if (comment.Equals(HostsFileManager.ManagedEntryKey, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    newEntries.Add(new HostEntry(name, ip));
                                }
                                catch (ArgumentException ex)
                                {
                                    LinuxCommunicator.Instance.Tracer.TraceInfo("[HostsFileManager] Skip invalid host entry name={0}, ip={1}: {2}", name, ip, ex.Message);
                                }
                            }
                        }

                        line = file.ReadLine();
                    }
                }

                if (manageFile)
                {
                    if (newEntries.Except(this.managedEntries).Any() || this.managedEntries.Except(newEntries).Any())
                    {
                        this.managedEntries = newEntries;
                        this.UpdateId = Guid.NewGuid();
                        LinuxCommunicator.Instance.Tracer.TraceInfo("[HostsFileManager] The managed host entries updated, current update Id is {0}", this.UpdateId);
                    }
                }
                else
                {
                    LinuxCommunicator.Instance.Tracer.TraceWarning("[HostsFileManager] Hosts file was not managed by HPC");
                    this.managedEntries.Clear();
                }

                this.ManageFile = manageFile;
                this.lastModified = fileInfo.LastWriteTimeUtc;
            }
            catch (Exception e)
            {
                LinuxCommunicator.Instance.Tracer.TraceWarning("[HostsFileManager] Failed to reload host file: {0}", e);
            }
            finally
            {
                this.reloadTimer.Change(ReloadTInterval, Timeout.Infinite);
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
                if (this.reloadTimer != null)
                {
                    this.reloadTimer.Dispose();
                }
            }
        }
    }
}
