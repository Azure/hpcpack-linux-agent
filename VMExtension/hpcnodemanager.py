#!/usr/bin/env python
#
# HPCNodeManager extension
#
# Copyright 2015 Microsoft Corporation
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
import os
import sys
import json
import subprocess
import re
import time
import traceback
import socket
import shutil
import platform
import struct
import array
import fcntl
import hashlib

from Utils.WAAgentUtil import waagent
import Utils.HandlerUtil as Util
from azurelinuxagent.common.osutil import get_osutil
from azurelinuxagent.common.version import get_distro

#Define global variables
ExtensionShortName = 'HPCNodeManager'
DaemonPidFilePath = '/var/run/hpcnmdaemon.pid'
InstallRoot = '/opt/hpcnodemanager'
DistroName = None
DistroVersion = None
RestartIntervalInSeconds = 60
osutil = None

def main():
    waagent.LoggerInit('/var/log/waagent.log','/dev/stdout')
    waagent.Log('Microsoft.HpcPack Linux NodeAgent started to handle.')
    global DistroName, DistroVersion, osutil
    distro = get_dist_info()
    DistroName = distro[0].lower()
    DistroVersion = distro[1]
    osutil = get_osutil()
    for a in sys.argv[1:]:        
        if re.match("^([-/]*)(disable)", a):
            disable()
        elif re.match("^([-/]*)(uninstall)", a):
            uninstall()
        elif re.match("^([-/]*)(install)", a):
            install()
        elif re.match("^([-/]*)(enable)", a):
            enable()
        elif re.match("^([-/]*)(daemon)", a):
            daemon()            
        elif re.match("^([-/]*)(update)", a):
            update()

def _is_nodemanager_daemon(pid):
    retcode, output = waagent.RunGetOutput("ps -p {0} -o cmd=".format(pid))
    if retcode == 0:
        waagent.Log("The cmd for process {0} is {1}".format(pid, output))
        pattern = r'(.*[/\s])?{0}\s+[-/]*daemon$'.format(os.path.basename(__file__))
        if re.match(pattern, output):
            return True
    waagent.Log("The process {0} is not HPC Linux node manager daemon".format(pid))
    return False

def install_package(package_name):
    if DistroName in ["centos", "redhat", "alma", "rocky"]:
        cmd = "yum -y install " + package_name
    elif DistroName == "ubuntu":
        waagent.Log("Updating apt package lists with command: apt-get -y update")
        exitcode = waagent.Run("apt-get -y update", chk_err=False)
        if exitcode != 0:
            waagent.Log("Update apt package lists failed with exitcode: {0}".format(exitcode))
        cmd = "apt-get -y install " + package_name
    elif DistroName == "suse":
        if not os.listdir('/etc/zypp/repos.d'):
            waagent.Run("zypper ar http://download.opensuse.org/distribution/13.2/repo/oss/suse/ opensuse")
            cmd = "zypper -n --gpg-auto-import-keys install --force-resolution -l " + package_name
        else:
            cmd = "zypper -n install --force-resolution -l " + package_name
    else:
        raise Exception("Unsupported Linux Distro.")
    waagent.Log("The command to install {0}: {1}".format(package_name, cmd))
    attempt = 1
    while(True):
        waagent.Log("Installing package {0} (Attempt {1})".format(package_name, attempt))
        retcode, retoutput = waagent.RunGetOutput(cmd)
        if retcode == 0:
            waagent.Log("package {0} installation succeeded".format(package_name))
            break
        else:
            waagent.Log("package {0} installation failed {1}:\n {2}".format(package_name, retcode, retoutput))
            if attempt < 10:
                time.sleep(min(30, pow(2, attempt)))
                attempt += 1
                if DistroName == 'suse' and retcode == 104:
                    waagent.Run("zypper ar http://download.opensuse.org/distribution/13.2/repo/oss/suse/ opensuse")
                    cmd = "zypper -n --gpg-auto-import-keys install --force-resolution -l " + package_name
                elif DistroName == "ubuntu":
                    waagent.Run("apt-get -y update", chk_err=False)
                continue
            else:
                raise Exception("failed to install package {0}:{1}".format(package_name, retcode))

def _uninstall_nodemanager_files():
    if os.path.isdir(InstallRoot):
        for tmpname in os.listdir(InstallRoot):
            if tmpname == 'logs':
                continue
            if tmpname == 'certs':
                continue
            if tmpname == 'filters':
                continue
            tmppath = os.path.join(InstallRoot, tmpname)
            if os.path.isdir(tmppath):
                shutil.rmtree(tmppath)
            elif os.path.isfile(tmppath):
                os.remove(tmppath)

def _install_cgroup_tool():
    if waagent.Run("command -v cgexec", chk_err=False) == 0:
        waagent.Log("cgroup tools was already installed")
    else:
        waagent.Log("Start to install cgroup tools")
        if DistroName == "ubuntu":
            if re.match("^1", DistroVersion):
                cg_pkgname = 'cgroup-bin'
            else:
                cg_pkgname = 'cgroup-tools'
        elif (DistroName == "centos" or DistroName == "redhat") and re.match("^6", DistroVersion):
            cg_pkgname = 'libcgroup'
        else:
            cg_pkgname = 'libcgroup-tools'
        install_package(cg_pkgname)
        waagent.Log("cgroup tool was successfully installed")

def _install_sysstat():
    if waagent.Run("command -v iostat", chk_err=False) == 0:
        waagent.Log("sysstat was already installed")
    else:
        waagent.Log("Start to install sysstat")
        install_package('sysstat')
        waagent.Log("sysstat was successfully installed")

def _install_pstree():
    if waagent.Run("command -v pstree", chk_err=False) == 0:
        waagent.Log("pstree was already installed")
    else:
        waagent.Log("Start to install pstree")
        install_package('psmisc')
        waagent.Log("pstree was successfully installed")

def get_networkinterfaces():
    """
    Return the interface name, and ip addr of the
    all non loopback interfaces.
    """
    expected=16 # how many devices should I expect...
    is_64bits = sys.maxsize > 2**32
    struct_size=40 if is_64bits else 32 # for 64bit the size is 40 bytes, for 32bits it is 32 bytes.
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    buff=array.array('B', b'\0' * (expected*struct_size))
    retsize=(struct.unpack('iL', fcntl.ioctl(s.fileno(), 0x8912, struct.pack('iL',expected*struct_size,buff.buffer_info()[0]))))[0]
    if retsize == (expected*struct_size) :
        waagent.Log('SIOCGIFCONF returned more than ' + str(expected) + ' up network interfaces.')
    nics = []
    s=buff.tostring()
    for i in range(0,retsize,struct_size):
        iface=s[i:i+16].split(b'\0', 1)[0]
        if iface == b'lo':
            continue
        else:
            nics.append((iface.decode('latin-1'), socket.inet_ntoa(s[i+20:i+24])))
    return nics

def cleanup_host_entries():
    hostsfile = '/etc/hosts'
    if not os.path.isfile(hostsfile):
        return
    try:
        hpcentryexists = False
        newcontent=''
        with open(hostsfile, 'r') as F:
            for line in F.readlines():
                if re.match(r"^[0-9\.]+\s+[^\s#]+\s+#HPCD?\s*$", line):
                    hpcentryexists = True
                else:
                    newcontent += line
        if hpcentryexists:
            waagent.Log("Clean all HPC related host entries from hosts file")
            waagent.ReplaceFileContentsAtomic(hostsfile,newcontent)
            os.chmod(hostsfile, 0o644)
    except :
        raise

def init_suse_hostsfile(host_name, ipaddrs):
    hostsfile = '/etc/hosts'
    if not os.path.isfile(hostsfile):
        return
    try:
        newhpcd_entries = ''
        for ipaddr in ipaddrs:
            newhpcd_entries += '{0:24}{1:30}#HPCD\n'.format(ipaddr, host_name)

        curhpcd_entries = ''
        newcontent = ''
        hpcentryexists = False
        with open(hostsfile, 'r') as F:
            for line in F.readlines():
                if re.match(r"^[0-9\.]+\s+[^\s#]+\s+#HPCD\s*$", line):
                    curhpcd_entries += line
                    hpcentryexists = True
                elif re.match(r"^[0-9\.]+\s+[^\s#]+\s+#HPC\s*$", line):
                    hpcentryexists = True
                else:
                    newcontent += line

        if newhpcd_entries != curhpcd_entries:
            if hpcentryexists:
                waagent.Log("Clean the HPC related host entries from hosts file")
            waagent.Log("Add the following HPCD host entries:\n{0}".format(newhpcd_entries))
            if newcontent and newcontent[-1] != '\n':
                newcontent += '\n'
            newcontent += newhpcd_entries
            waagent.ReplaceFileContentsAtomic(hostsfile,newcontent)
            os.chmod(hostsfile, 0o644)
    except :
        raise

def gethostname_from_configfile(configfile):
    config_hostname = None
    if os.path.isfile(configfile):
        with open(configfile, 'r') as F:
            configjson = json.load(F)
        if 'RegisterUri' in configjson:
            reguri = configjson['RegisterUri']
            reguri = reguri[0:reguri.rindex('/')]
            config_hostname = reguri[reguri.rindex('/')+1:]
    return config_hostname

def _add_dns_search(domain_fqdn):
    need_update = False
    new_content = ''
    for line in (open('/etc/resolv.conf','r')).readlines():
        if re.match('^search.* {0}'.format(domain_fqdn), line):
            waagent.Log('{0} was already added in /etc/resolv.conf'.format(domain_fqdn))
            return
        if re.match('^search', line):
            need_update = True
            new_content += line.replace('search', 'search {0}'.format(domain_fqdn))
        else:
            new_content += line
    if need_update:
        waagent.Log('Adding {0} to /etc/resolv.conf'.format(domain_fqdn))
        waagent.SetFileContents('/etc/resolv.conf', new_content)

def _update_dns_record(domain_fqdn):
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    while True:
        try:
            s.connect((domain_fqdn, 53))
            break
        except Exception as e:
            waagent.Log('Failed to connect to {0}:53: {1}'.format(domain_fqdn, e))
    ipaddr = s.getsockname()[0]
    host_fqdn = "{0}.{1}".format(socket.gethostname().split('.')[0], domain_fqdn)
    dns_cmd = 'echo -e "server {0}\nzone {0}\nupdate delete {1}\nupdate add {1} 864000 A {2}\nsend\n" | nsupdate -v'.format(domain_fqdn, host_fqdn, ipaddr)
    waagent.Log('The command to update ip to dns server is: {0}'.format(dns_cmd))
    retry = 0
    while retry < 60:
        dns_ret, dns_msg = waagent.RunGetOutput(dns_cmd)
        if dns_ret == 0:
            waagent.Log("Succeeded to update ip to dns server.")
            return
        else:
            retry = retry + 1
            waagent.Log("Failed to update ip to dns server: {0}, {1}".format(dns_ret, dns_msg))
            time.sleep(10)

def _mount_cgroup():
    if not os.path.isdir('/cgroup'):
        os.mkdir('/cgroup')
    if not os.listdir('/cgroup'):
        retcode, mount_msg = waagent.RunGetOutput('mount -t cgroup cgroup /cgroup')
        waagent.Log("mount /cgroup directory {0}:{1}".format(retcode, mount_msg))
        if retcode == 0:
            waagent.Log("/cgroup directory is successfully mounted.")
        else:
            raise Exception("failed to mount /cgroup directory")
    else:
        waagent.Log("/cgroup directory was already mounted.")

def config_firewall_rules():
    if DistroName == 'redhat':
        waagent.Log('Configuring the firewall rules')
        major_version = int(DistroVersion.split('.')[0])
        if major_version < 7:
            waagent.Run('lokkit --port=40000:tcp --update', chk_err=False)
            waagent.Run('lokkit --port=40002:tcp --update', chk_err=False)
        elif waagent.Run("firewall-cmd --state", chk_err=False) == 0:
            waagent.Run("firewall-cmd --permanent --zone=public --add-port=40000/tcp")
            waagent.Run("firewall-cmd --permanent --zone=public --add-port=40002/tcp")
            waagent.Run("firewall-cmd --reload")

def parse_context(operation):
    hutil = Util.HandlerUtility(waagent.Log, waagent.Error, ExtensionShortName)
    hutil.do_parse_context(operation)
    return hutil


def cmpFileHash(file1, file2):
    if not (os.path.isfile(file1) and os.path.isfile(file2)):
        return False
    digests = []
    for filename in [file1, file2]:
        md5hash = hashlib.md5()
        with open(filename, 'rb') as f:
            buf = f.read()
            md5hash.update(buf)
            digest = md5hash.hexdigest()
            digests.append(digest)
    return digests[0] == digests[1]


def install():
    hutil = parse_context('Install')
    try:
        cleanup_host_entries()
        _uninstall_nodemanager_files()
        if DistroName in ["centos", "redhat", "alma", "rocky"]:
            waagent.Run("yum-config-manager --setopt=\\*.skip_if_unavailable=1 --save", chk_err=False)
        _install_cgroup_tool()
        _install_sysstat()
        _install_pstree()
        
        logDir = os.path.join(InstallRoot, "logs")
        if not os.path.isdir(logDir):
            os.makedirs(logDir)
        srcDir = os.path.join(os.getcwd(), "bin")
        waagent.RunGetOutput("chmod +x {0}/*".format(srcDir))
        waagent.RunGetOutput("chmod +x {0}/lib/*".format(srcDir))
        for filename in os.listdir(srcDir):
            srcname = os.path.join(srcDir, filename)
            destname = os.path.join(InstallRoot, filename)
            if os.path.isfile(srcname):
                shutil.copy2(srcname, destname)
            elif os.path.isdir(srcname):
                shutil.copytree(srcname, destname)
        libdir = os.path.join(InstallRoot, 'lib')
        for tmpname in os.listdir(libdir):
            tmppath = os.path.join(libdir, tmpname)
            if tmpname.endswith(".tar.gz") and os.path.isfile(tmppath):
                waagent.Run("tar xzvf {0} -C {1}".format(tmppath, libdir))
                os.remove(tmppath)
        waagent.Run("chmod -R 755 {0}".format(libdir))

        host_name = None
        public_settings = hutil._context._config['runtimeSettings'][0]['handlerSettings'].get('publicSettings')
        if public_settings:
            host_name = public_settings.get('HostName')
        backup_configfile = os.path.join(os.getcwd(), 'nodemanager.json')
        if not host_name:
            # if there is backup nodemanager.json, means it is an update install, if 'HostName' not defined in the extension
            # settings, we shall get from the backup nodemanager.json
            if os.path.isfile(backup_configfile):
                waagent.Log("Backup nodemanager configuration file found")
                host_name = gethostname_from_configfile(backup_configfile)

        curhostname = socket.gethostname().split('.')[0]
        if host_name:
            if host_name.lower() != curhostname.lower():
                waagent.Log("HostName was set: hostname from {0} to {1}".format(curhostname, host_name))
                osutil.set_hostname(host_name)
                osutil.publish_hostname(host_name)
        else:
            host_name = curhostname
        public_settings = hutil._context._config['runtimeSettings'][0]['handlerSettings'].get('publicSettings')
        cluster_connstring = public_settings.get('ClusterConnectionString')
        if not cluster_connstring:
            waagent.Log("ClusterConnectionString is not specified")
            protect_settings = hutil._context._config['runtimeSettings'][0]['handlerSettings'].get('protectedSettings')
            cluster_connstring = protect_settings.get('ClusterName')
            if not cluster_connstring:
                error_msg = "neither ClusterConnectionString nor ClusterName is specified."
                hutil.error(error_msg)
                raise ValueError(error_msg)
        ssl_thumbprint = public_settings.get('SSLThumbprint')
        certsdir = os.path.join(InstallRoot, "certs")
        if not ssl_thumbprint:
            api_prefix = "http://{0}:80/HpcLinux/api/"
            listen_uri = "http://0.0.0.0:40000"
        else:
            api_prefix = "https://{0}:443/HpcLinux/api/"
            listen_uri = "https://0.0.0.0:40002"
            # import the ssl certificate for hpc nodemanager
            if not os.path.isdir(certsdir):
                os.makedirs(certsdir, 0o750)
            else:
                os.chmod(certsdir, 0o750)
            ssl_thumbprint = ssl_thumbprint.upper()
            prvfile = os.path.join("/var/lib/waagent", ssl_thumbprint + ".prv")
            srccrtfile = os.path.join("/var/lib/waagent", ssl_thumbprint + ".crt")
            rsakeyfile = os.path.join(certsdir, "nodemanager_rsa.key")
            dstcrtfile = os.path.join(certsdir, "nodemanager.crt")
            if os.path.isfile(prvfile) and not cmpFileHash(prvfile, rsakeyfile):
                waagent.Run("rm -rf {0}/nodemanager.crt {0}/nodemanager.key {0}/nodemanager.pem {0}/nodemanager_rsa.key".format(certsdir), chk_err=False)
                shutil.copy2(prvfile, rsakeyfile)
                shutil.copy2(srccrtfile, dstcrtfile)
                shutil.copy2(dstcrtfile, os.path.join(certsdir, "nodemanager.pem"))
                waagent.Run("openssl rsa -in {0}/nodemanager_rsa.key -out {0}/nodemanager.key".format(certsdir))
                waagent.Run("chmod 640 {0}/nodemanager.crt {0}/nodemanager.key {0}/nodemanager.pem {0}/nodemanager_rsa.key".format(certsdir))

        node_uri = api_prefix + host_name + "/computenodereported"
        reg_uri = api_prefix + host_name + "/registerrequested"
        hostsfile_uri = api_prefix + "hostsfile"
        metric_ids_uri = api_prefix + host_name + "/getinstanceids"
        namingSvcUris = ['https://{0}:443/HpcNaming/api/fabric/resolve/singleton/'.format(h.split('.')[0].strip()) for h in cluster_connstring.split(',')]
        if os.path.isfile(backup_configfile):
            with open(backup_configfile, 'r') as F:
                configjson = json.load(F)
            configjson["NamingServiceUri"] = namingSvcUris
            configjson["HeartbeatUri"] = node_uri
            configjson["RegisterUri"] = reg_uri
            configjson["HostsFileUri"] = hostsfile_uri
            configjson["MetricInstanceIdsUri"] = metric_ids_uri
            configjson["MetricUri"] = ""
            configjson["ListeningUri"] = listen_uri
        else:
            configjson = {
              "ConfigVersion": "1.0",
              "NamingServiceUri": namingSvcUris,
              "HeartbeatUri": node_uri,
              "RegisterUri": reg_uri,
              "MetricUri": "",
              "MetricInstanceIdsUri": metric_ids_uri,
              "HostsFileUri": hostsfile_uri,
              "HostsFetchInterval": 120,
              "ListeningUri": listen_uri,
              "DefaultServiceName": "SchedulerStatefulService",
              "UdpMetricServiceName": "MonitoringStatefulService"
            }
        if ssl_thumbprint:
            configjson["TrustedCAFile"] = os.path.join(certsdir, "nodemanager.pem")
            configjson["CertificateChainFile"] = os.path.join(certsdir, "nodemanager.crt")
            configjson["PrivateKeyFile"] = os.path.join(certsdir, "nodemanager.key")
        configfile = os.path.join(InstallRoot, 'nodemanager.json')
        waagent.SetFileContents(configfile, json.dumps(configjson))
        shutil.copy2(configfile, backup_configfile)
        config_firewall_rules()
        hutil.do_exit(0, 'Install', 'success', '0', 'Install Succeeded.')
    except Exception as e:
        hutil.do_exit(1, 'Install','error','1', '{0}'.format(e))

def enable():
    #Check whether monitor process is running.
    #If it does, return. Otherwise clear pid file
    hutil = parse_context('Enable')
    if os.path.isfile(DaemonPidFilePath):
        pid = waagent.GetFileContents(DaemonPidFilePath)
        if os.path.isdir(os.path.join("/proc", pid)) and _is_nodemanager_daemon(pid):
            if hutil.is_seq_smaller():
                hutil.do_exit(0, 'Enable', 'success', '0', 
                              'HPC Linux node manager daemon is already running')
            else:
                waagent.Log("Stop old daemon: {0}".format(pid))
                os.killpg(int(pid), 9)
        os.remove(DaemonPidFilePath)

    args = [get_python_executor(), os.path.join(os.getcwd(), __file__), "daemon"]
    devnull = open(os.devnull, 'w')
    child = subprocess.Popen(args, stdout=devnull, stderr=devnull, preexec_fn=os.setsid)
    if child.pid is None or child.pid < 1:
        hutil.do_exit(1, 'Enable', 'error', '1',
                      'Failed to launch HPC Linux node manager daemon')
    else:
        hutil.save_seq()
        waagent.SetFileContents(DaemonPidFilePath, str(child.pid))
        #Sleep 3 seconds to check if the process is still running
        time.sleep(3)
        if child.poll() is None:
            waagent.Log("Daemon pid: {0}".format(child.pid))
            hutil.do_exit(0, 'Enable', 'success', '0',
                      'HPC Linux node manager daemon is enabled')
        else:
            hutil.do_exit(1, 'Enable', 'error', '2',
                      'Failed to launch HPC Linux node manager daemon')

def daemon():
    hutil = parse_context('Enable')
    try:
        public_settings = hutil._context._config['runtimeSettings'][0]['handlerSettings'].get('publicSettings')
        domain_fqdn = public_settings.get('DomainName')
        if not domain_fqdn:
            cluster_connstring = public_settings.get('ClusterConnectionString')
            if not cluster_connstring:
                waagent.Log("ClusterConnectionString is not specified, use ClusterName instead")
                protect_settings = hutil._context._config['runtimeSettings'][0]['handlerSettings'].get('protectedSettings')
                cluster_connstring = protect_settings.get('ClusterName')
            headnode_name = cluster_connstring.split(',')[0].strip()
            if headnode_name.find('.') > 0:
                # The head node name is FQDN, extract the domain FQDN
                domain_fqdn = headnode_name.split(".", 1)[1]

        if domain_fqdn:
            waagent.Log("The domain FQDN is " + domain_fqdn)
            _add_dns_search(domain_fqdn)
            #thread.start_new_thread(_update_dns_record, (domain_fqdn,))

        # A fix only for SUSE Linux that sometimes the hostname got changed because out-of-date host/IP entry in /etc/hosts
        # It may happen when the node was assigned a different IP after deallocation
        # We shall clean the current HPC related host/IP entries and add the actual IPs before fetching the hosts file from head node.
        if DistroName == 'suse':
            configfile = os.path.join(InstallRoot, 'nodemanager.json')
            confighostname = gethostname_from_configfile(configfile)
            curhostname = socket.gethostname().split('.')[0]
            if confighostname.lower() != curhostname.lower():
                cleanup_host_entries()
                waagent.Log("Correct the hostname from {0} to {1}".format(curhostname, confighostname))
                osutil.set_hostname(confighostname)
                osutil.publish_hostname(confighostname)
            retry = 0
            while True:
                nics = get_networkinterfaces()
                if len(nics) > 0:
                    init_suse_hostsfile(confighostname, [nic[1] for nic in nics])
                    break
                elif retry < 30:
                    waagent.Log("Failed to get network interfaces information, retry later ...")
                    time.sleep(2)
                    retry = retry + 1
                else:
                    waagent.Log("Failed to get network interfaces information, just clean")
                    break
        # Mount the directory /cgroup for centos 6.*
        major_version = int(DistroVersion.split('.')[0])
        if (DistroName == 'centos' or DistroName == 'redhat') and major_version < 7:
            _mount_cgroup()
        while True:
            exe_path = os.path.join(InstallRoot, "nodemanager")
            devnull = open(os.devnull, 'w')
            child_process = subprocess.Popen(exe_path, stdout=devnull, stderr=devnull, cwd=InstallRoot)
            if child_process.pid is None or child_process.pid < 1:
                exit_msg = 'Failed to start HPC node manager process'
                hutil.do_status_report('Enable', 'error', 1, exit_msg)
            else:
                #Sleep 1 second to check if the process is still running
                time.sleep(1)
                if child_process.poll() is None:
                    hutil.do_status_report('Enable', 'success', 0, "")
                    waagent.Log('HPC node manager process started')
                    exit_code = child_process.wait()
                    exit_msg = "HPC node manager process exits: {0}".format(exit_code)
                    hutil.do_status_report('Enable', 'warning', exit_code, exit_msg)
                else:
                    exit_msg = "HPC node manager process crashes: {0}".format(child_process.returncode)
                    hutil.do_status_report('Enable', 'error', child_process.returncode, exit_msg)
            waagent.Log(exit_msg)
            waagent.Log("Restart HPC node manager process after {0} seconds".format(RestartIntervalInSeconds))
            time.sleep(RestartIntervalInSeconds)

    except Exception as e:
        hutil.error("Failed to enable the extension with error: %s, stack trace: %s" %(str(e), traceback.format_exc()))
        hutil.do_exit(1, 'Enable','error','1', 'Enable failed.')

def uninstall():
    hutil = parse_context('Uninstall')
    _uninstall_nodemanager_files()
    cleanup_host_entries()
    hutil.do_exit(0,'Uninstall','success','0', 'Uninstall succeeded')

def disable():
    hutil = parse_context('Disable')
    #Check whether monitor process is running.
    #If it does, kill it. Otherwise clear pid file
    if os.path.isfile(DaemonPidFilePath):
        pid = waagent.GetFileContents(DaemonPidFilePath)
        if os.path.isdir(os.path.join("/proc", pid)) and _is_nodemanager_daemon(pid):
            waagent.Log(("Stop HPC node manager daemon: {0}").format(pid))
            os.killpg(int(pid), 9)
            os.remove(DaemonPidFilePath)
            cleanup_host_entries()
            hutil.do_exit(0, 'Disable', 'success', '0',
                          'HPC node manager daemon is disabled')
        os.remove(DaemonPidFilePath)

    hutil.do_exit(0, 'Disable', 'success', '0',
                  'HPC node manager daemon is not running')

def update():
    hutil = parse_context('Update')
    cleanup_host_entries()
    configfile = os.path.join(InstallRoot, 'nodemanager.json')
    if os.path.isfile(configfile):
        waagent.Log("Update extension: backup the nodemanager configuration file.")
        shutil.copy2(configfile, os.getcwd())
        # A fix only for SUSE Linux that sometimes the hostname got changed because out-of-date host/IP entry in /etc/hosts
        # It may happen when the node was assigned a different IP after deallocation
        if DistroName == 'suse':
            confighostname = gethostname_from_configfile(configfile)
            if confighostname:
                curhostname = socket.gethostname().split('.')[0]
                if confighostname.lower() != curhostname.lower():
                    waagent.Log("Update: Set the hostname from {0} to {1}".format(curhostname, confighostname))
                    osutil.set_hostname(confighostname)
                    osutil.publish_hostname(confighostname)
    hutil.do_exit(0,'Update','success','0', 'Update Succeeded')

def get_python_executor():
    cmd = ''
    if sys.version_info.major == 2:
        cmd = 'python2'
    elif sys.version_info.major == 3:
        cmd = 'python3'
    if waagent.Run("command -v {0}".format(cmd), chk_err=False) != 0:
        # If a user-installed python isn't available, check for a platform-python. This is typically only used in RHEL 8.0.
        if waagent.Run("command -v /usr/libexec/platform-python", chk_err=False) == 0:
            cmd = '/usr/libexec/platform-python'
    return cmd

def get_dist_info():
    try:
        return get_distro()
    except:
        pass
    errCode, info = waagent.RunGetOutput("cat /etc/*-release")
    if errCode != 0:
        raise Exception('Failed to get Linux Distro info by running command "cat /etc/*release", error code: {}'.format(errCode))
    distroName = ''
    distroVersion = ''
    for line in info.splitlines():
        if line.startswith('PRETTY_NAME='):
            line = line.lower()
            if 'ubuntu' in line:
                distroName = 'ubuntu'
            elif 'centos' in line:
                distroName = 'centos'
            elif 'red hat' in line:
                distroName = 'redhat'
            elif 'suse' in line:
                distroName = 'suse'
            elif 'alma' in line:
                distroName = 'alma'
            elif 'rocky' in line:
                distroName = 'rocky'
            elif 'fedora' in line:
                distroName = 'fedora'
            elif 'freebsd' in line:
                distroName = 'freebsd'
            else:
                raise Exception('Unknown linux distribution with {}'.format(line))
        if line.startswith('VERSION_ID='):
            line = line.strip(' ')
            quoteIndex = line.index('"')
            if quoteIndex >= 0:
                distroVersion = line[quoteIndex+1:-1]
    return distroName, distroVersion, ""

  
if __name__ == '__main__' :
    main()

