#!/usr/bin/env python
#
# Hpc Linux Agent Setup
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
#
# Requires Python2.6+ or Python3.x

import os
import sys
import subprocess
import re
import time
import socket
import platform
import getpass
import shutil
import json
import string
import traceback
import tempfile
import tarfile

# Define global variables
InstallRoot = '/opt/hpcnodemanager'
DistroName = None
DistroVersion = None
SetupLogFile = None
SupportSystemd = False

if not hasattr(subprocess,'check_output'):
    def check_output(*popenargs, **kwargs):
        r"""Backport from subprocess module from python 2.7"""
        if 'stdout' in kwargs:
            raise ValueError('stdout argument not allowed, it will be overridden.')
        process = subprocess.Popen(stdout=subprocess.PIPE, *popenargs, **kwargs)
        output, unused_err = process.communicate()
        retcode = process.poll()
        if retcode:
            cmd = kwargs.get("args")
            if cmd is None:
                cmd = popenargs[0]
            raise subprocess.CalledProcessError(retcode, cmd, output=output)
        return output

    # Exception classes used by this module.
    class CalledProcessError(Exception):
        def __init__(self, returncode, cmd, output=None):
            self.returncode = returncode
            self.cmd = cmd
            self.output = output
        def __str__(self):
            return "Command '%s' returned non-zero exit status %d" % (self.cmd, self.returncode)

    subprocess.check_output=check_output
    subprocess.CalledProcessError=CalledProcessError


def get_argvalue(argstr):
    start_index = argstr.index(':') + 1
    return argstr[start_index:]


def install_package(package_name):
    if DistroName in ["centos", "redhat", "alma", "almalinux", "rocky", "rockylinux"]:
        cmd = "yum -y install " + package_name
    elif DistroName == "ubuntu":
        cmd = "apt-get -y install " + package_name
    elif DistroName == "suse":
        cmd = "zypper -n install " + package_name
    else:
        raise Exception("Unsupported Linux Distro.")
    Log("The command to install {0}: {1}".format(package_name, cmd))
    attempt = 1
    while(True):
        Log("Installing package {0} (Attempt {1})".format(package_name, attempt))
        retcode, retoutput = RunGetOutput(cmd)
        if retcode == 0:
            Log("package {0} installation succeeded".format(package_name))
            break
        else:
            Log("package {0} installation failed {0}:\n {1}".format(package_name, retcode, retoutput))
            if attempt < 3:
                attempt += 1
                time.sleep(5)
                continue
            else:
                raise Exception("failed to install package {0}:{1}".format(package_name, retcode))

def extract_hpcagent_files(src):
    srctar = tarfile.open(src, 'r:gz')
    try:
        Run("rm -rf {0}/nodemanager {0}/hpcagent {0}/*.sh {0}/*.py {0}/lib {0}/Utils".format(InstallRoot))
        srctar.extractall(InstallRoot)
        libdir = os.path.join(InstallRoot, 'lib')
        os.chmod(libdir, 0o644)
        os.chmod(os.path.join(InstallRoot, 'Utils'), 0o644)
        # Copy setup.py itself to the InstallRoot
        shutil.copy2(__file__, InstallRoot)
        for tmpname in os.listdir(libdir):
            tmppath = os.path.join(libdir, tmpname)
            if tmpname.endswith(".tar.gz") and os.path.isfile(tmppath):
                Run("tar xzvf {0} -C {1}".format(tmppath, libdir))
                os.remove(tmppath)
        Run("chmod 755 {0}/nodemanager {0}/hpcagent {0}/*.sh {0}/*.py {0}/Utils/*".format(InstallRoot))
        Run("chmod -R 755 {0}/lib".format(InstallRoot))
    finally:
        srctar.close()

def remove_hpcagent_files(keep_log=True, keep_cert=True):
    if os.path.isdir(InstallRoot):
        for tmpname in os.listdir(InstallRoot):
            if tmpname == 'logs' and keep_log:
                continue
            if tmpname == 'certs'and keep_cert:
                continue
            tmppath = os.path.join(InstallRoot, tmpname)
            if os.path.isdir(tmppath):
                shutil.rmtree(tmppath)
            elif os.path.isfile(tmppath):
                os.remove(tmppath)

def install_cgroup_tools():
    if Run("command -v cgexec", chk_err=False) == 0:
        Log("cgroup tools was already installed")
    else:
        Log("Start to install cgroup tools")
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
        Log("cgroup tool was successfully installed")

def install_sysstat():
    if Run("command -v iostat", chk_err=False) == 0:
        Log("sysstat was already installed")
    else:
        Log("Start to install sysstat")
        install_package('sysstat')
        Log("sysstat was successfully installed")

def install_pstree():
    if Run("command -v pstree", chk_err=False) == 0:
        Log("pstree was already installed")
    else:
        Log("Start to install pstree")
        install_package('psmisc')
        Log("pstree was successfully installed")

def copy_direcotry(src, dest):
    if not os.path.exists(dest):
        shutil.copytree(src, dest)
    if not os.path.samefile(src, dest):
        for filename in os.listdir(src):
            srcname = os.path.join(src, filename)
            destname = os.path.join(dest, filename)
            if os.path.isfile(srcname):
                shutil.copy2(srcname, destname)
            elif os.path.isdir(srcname):
                copy_direcotry(srcname, destname)


def Usage():
    usage = 'Usage: \n' \
            '*Fresh new install the HPC node agent:\n' \
            '    setup.py -install -connectionstring:<connectionstring> -certfile:<certfile> -certpasswd:<certpass> [-managehosts]\n\n' \
            '*Install the HPC node agent with currently existing certificate:\n' \
            '    setup.py -install -connectionstring:<connectionstring> -keepcert [-managehosts]\n\n' \
            '*Uninstall the HPC node agent:\n' \
            '    setup.py -uninstall [-keepcert]\n\n' \
            '*Update the binaries of HPC node agent:\n' \
            '    setup.py -update\n\n' \
            '*Update the certificate used to communicate with head node:\n' \
            '    setup.py -updatecert -certfile:<certfile> -certpasswd:<certpass>\n\n' \
            'Description of the parameters:\n' \
            '    connectionstring: The connection string of the HPC cluster, typically a list of head node hostnames or full qualified domain names.\n' \
            '    certfile: The PFX certificate file used to communicate with head node\n' \
            '    certpasswd: The protection password of the PFX certificate\n' \
            '    keepcert: Keep the currently existing certificates\n' \
            '    managehosts: Specify that you want the /etc/hosts file managed by HPC\n\n' \
            'Note: This command must be run as root user\n\n' \
            'Examples: \n' \
            'setup.py -install -connectionstring:\'hn1,hn2,hn3\' -certfile:\'/root/mycert.pfx\' -certpasswd:\'Pa$$word1\' -managehosts\n\n' \
            'setup.py -install -connectionstring:\'hn1.hpc.local,hn2.hpc.local,hn3.hpc.local\' -keepcert\n\n' \
            'setup.py -uninstall -keepcert\n\n' \
            'setup.py -update\n\n' \
            'setup.py -updatecert -certfile:\'/root/newcert.pfx\' -certpasswd:\'Pa$$word2\'\n'
    print(usage)

def is_hpcagent_installed():
    if os.path.isfile('/etc/init.d/hpcagent'):
        return True
    else:
        return False

def cleanup_host_entries():
    hostsfile = '/etc/hosts'
    if not os.path.isfile(hostsfile):
        return
    try:
        updated = False
        newcontent=''
        with open(hostsfile, 'r') as F:
            for line in F.readlines():
                if re.match(r"^[0-9\.]+\s+[^\s#]+\s+#HPC\s*$", line):
                    updated = True
                else:
                    newcontent += line
        if updated:
            Log("Clean HPC related host entries from hosts file")
            ReplaceFileContentsAtomic(hostsfile,newcontent)
            os.chmod(hostsfile, 0o644)
    except :
        raise

def cleanup_hpc_agent(keepcert):
    if os.path.isfile('/etc/init.d/hpcagent'):
        Log("Stop the hpc node agent")
        if SupportSystemd:
            Run("systemctl stop hpcagent", chk_err=False)
            Run("systemctl disable hpcagent", chk_err=False)
            os.remove('/etc/init.d/hpcagent')
            Run("systemctl reset-failed", chk_err=False)
        else:
            Run("service hpcagent stop", chk_err=False)
            if DistroName == "ubuntu":
                Run("update-rc.d -f hpcagent remove")
            elif DistroName in ["centos", "redhat", "suse", "alma", "almalinux", "rocky", "rockylinux"]:
                Run("chkconfig --del hpcagent")
            else:
                raise Exception("unsupported Linux Distro")
            os.remove('/etc/init.d/hpcagent')
    if Run("firewall-cmd --state", chk_err=False) == 0:
        Log("Remove firewall policies for hpc agent")
        Run("firewall-cmd --permanent --zone=public --query-port=40000/tcp && firewall-cmd --permanent --zone=public --remove-port=40000/tcp", chk_err=False)
        Run("firewall-cmd --permanent --zone=public --query-port=40002/tcp && firewall-cmd --permanent --zone=public --remove-port=40002/tcp", chk_err=False)
        Run("firewall-cmd --reload")
    remove_hpcagent_files(keep_cert=keepcert)
    cleanup_host_entries()

def uninstall():
    keepcert = False
    if len(sys.argv) > 3:
        Usage()
        sys.exit(1)
    elif (len(sys.argv) == 3):
        if re.match("^[-/]keepcert", sys.argv[2]):
            keepcert = True
        else:
            Usage()
            sys.exit(1)
    cleanup_hpc_agent(keepcert)
    Log("hpc agent removed")

def update():
    if len(sys.argv) > 2:
        Usage()
        sys.exit(1)

    setup_dir = os.path.dirname(os.path.abspath(__file__))
    if os.path.samefile(setup_dir, InstallRoot):
        Log("Nothing to update")
        sys.exit(1)

    srcpkg = os.path.join(setup_dir, 'hpcnodeagent.tar.gz')
    if not os.path.isfile(srcpkg):
        Log("Nothing to update: hpcnodeagent.tar.gz not found")
        sys.exit(1)

    if not is_hpcagent_installed():
        Log("No hpc agent installed")
        sys.exit(1)

    pemfile = os.path.join(InstallRoot, "certs/nodemanager.pem")
    rsakeyfile = os.path.join(InstallRoot, "certs/nodemanager_rsa.key")
    if not os.path.isfile(pemfile) or not os.path.isfile(rsakeyfile):
        Log("No certificates configured")
        sys.exit(1)

    configfile = os.path.join(InstallRoot, 'nodemanager.json')
    if not os.path.isfile(configfile):
        Log("nodemanager.json not found")
        sys.exit(1)

    Log("Stop the hpc node agent")
    if SupportSystemd:
        Run("systemctl stop hpcagent", chk_err=False)
    else:
        Run("service hpcagent stop", chk_err=False)

    with open(configfile, 'r') as F:
        configjson = json.load(F)

    Log("Update the binaries")
    extract_hpcagent_files(srcpkg)
    ReplaceFileContentsAtomic(configfile, json.dumps(configjson))
    os.chmod(configfile, 0o644)
    shutil.move(os.path.join(InstallRoot, "hpcagent.sh"), "/etc/init.d/hpcagent")

    Log("restart hpcagent")
    if SupportSystemd:
        Run("systemctl restart hpcagent")
    else:
        Run("service hpcagent restart")
    Log("hpc agent updated")

def generatekeypair(certfile, certpasswd):
    certsdir = os.path.join(InstallRoot, "certs")
    if not os.path.isdir(certsdir):
        os.makedirs(certsdir, 0o750)
    else:
        os.chmod(certsdir, 0o750)
    result = Run("openssl pkcs12 -in {0} -out {1}/nodemanager_rsa.key -nocerts -nodes -password pass:'{2}'".format(certfile, certsdir, certpasswd))
    if result != 0:
        raise Exception("Failed to generate nodemanager_rsa.key, please check whether the certificate protection password is correct.")
    result = Run("openssl pkcs12 -in {0} -out {1}/nodemanager.pem -password pass:'{2}' -nokeys".format(certfile, certsdir, certpasswd))
    if result != 0:
        raise Exception("Failed to generate nodemanager.pem, please check whether the certificate protection password is correct.")
    result = Run("openssl rsa -in {0}/nodemanager_rsa.key -out {0}/nodemanager.key".format(certsdir))
    if result != 0:
        raise Exception("Failed to generate nodemanager.key.")
    shutil.copy2(os.path.join(certsdir,'nodemanager.pem'), os.path.join(certsdir, 'nodemanager.crt'))

def updatecert():
    if not is_hpcagent_installed():
        Log("No hpc agent installed")
        sys.exit(1)

    certfile = None
    certpasswd = None
    for a in sys.argv[2:]:
        if re.match("^[-/]certfile:.+", a):
            certfile = get_argvalue(a)
        elif re.match("^[-/]certpasswd:.+", a):
            certpasswd = get_argvalue(a)
        else:
            print("Invalid argument: %s" % a)
            Usage()
            sys.exit(1)

    if not os.path.isfile(certfile):
        print("certfile not found: %s" % certfile)
        sys.exit(1)
    while not certpasswd:
        certpasswd = getpass.getpass(prompt='Please input the certificate protection password:')

    try:
        generatekeypair(certfile, certpasswd)
        print("The certificate was successfully updated")
        if SupportSystemd:
            Run("systemctl restart hpcagent")
        else:
            Run("service hpcagent restart")
        sys.exit(0)
    except Exception as e:
        print("Failed to update certificate: {0}".format(e))
        sys.exit(1)

def install():
    keepcert = False
    managehosts = False
    connectionstring = None
    certfile = None
    certpasswd = None
    for a in sys.argv[2:]:
        if re.match(r"^[-/](help|usage|\?)", a):
            Usage()
            sys.exit(0)
        if re.match("^[-/](connectionstring|clusname):.+", a):
            connectionstring = get_argvalue(a)
        elif re.match("^[-/]certfile:.+", a):
            certfile = get_argvalue(a)
        elif re.match("^[-/]certpasswd:.+", a):
            certpasswd = get_argvalue(a)
        elif re.match("^[-/]keepcert", a):
            keepcert = True
        elif re.match("^[-/]managehosts", a):
            managehosts = True
        else:
            print("Invalid argument: %s" % a)
            Usage()
            sys.exit(1)

    if not connectionstring or (not keepcert and not certfile):
        print("One or more parameters are not specified.")
        Usage()
        sys.exit(1)

    if keepcert and (certfile or certpasswd):
        print("The parameter keepcert cannot be specified with the parameter certfile or certpass")
        Usage()
        sys.exit(1)

    if keepcert:
        pemfile = os.path.join(InstallRoot, "certs/nodemanager.pem")
        rsakeyfile = os.path.join(InstallRoot, "certs/nodemanager_rsa.key")
        if not os.path.isfile(pemfile) or not os.path.isfile(rsakeyfile):
            Log("nodemanager.pem or nodemanager_rsa.key not found")
            sys.exit(1)
    else:
        if not os.path.isfile(certfile):
            print("certfile not found: %s" % certfile)
            sys.exit(1)
        while not certpasswd:
            certpasswd = getpass.getpass(prompt='Please input the certificate protection password:')

    srcpkgdir = os.path.dirname(__file__)
    srcpkg = os.path.join(srcpkgdir, 'hpcnodeagent.tar.gz')
    if not os.path.isfile(srcpkg):
        Log("hpcnodeagent.tar.gz not found")
        sys.exit(1)

    if is_hpcagent_installed():
        Log("hpc agent was already installed")
        sys.exit(0)

    Log("Start to install HPC Linux node agent")
    try:
        extract_hpcagent_files(srcpkg)
        logdir = os.path.join(InstallRoot, "logs")
        certsdir = os.path.join(InstallRoot, "certs")
        if not os.path.isdir(logdir):
            os.makedirs(logdir)

        host_name = socket.gethostname().split('.')[0]
        api_prefix = "https://{0}:443/HpcLinux/api/"
        node_uri = api_prefix + host_name + "/computenodereported"
        reg_uri = api_prefix + host_name + "/registerrequested"
        metric_inst_uri = api_prefix + host_name + "/getinstanceids"
        configjson = {
          "NamingServiceUri": ['https://{0}:443/HpcNaming/api/fabric/resolve/singleton/'.format(h.strip()) for h in connectionstring.split(',')],
          "HeartbeatUri": node_uri,
          "RegisterUri": reg_uri,
          "MetricInstanceIdsUri": metric_inst_uri,
          "MetricUri": "",
          "TrustedCAFile": os.path.join(certsdir, "nodemanager.pem"),
          "CertificateChainFile": os.path.join(certsdir, "nodemanager.crt"),
          "PrivateKeyFile": os.path.join(certsdir, "nodemanager.key"),
          "ListeningUri": "https://0.0.0.0:40002",
          "DefaultServiceName": "SchedulerStatefulService",
          "UdpMetricServiceName": "MonitoringStatefulService"
        }
        if managehosts:
            configjson['HostsFileUri'] = api_prefix + "hostsfile"
        configfile = os.path.join(InstallRoot, 'nodemanager.json')
        SetFileContents(configfile, json.dumps(configjson))
        os.chmod(configfile, 0o640)
        if not keepcert:
            Log("Generating the key pair from {0}".format(certfile))
            generatekeypair(certfile, certpasswd)

        Log("Install depending tools ...")
        install_cgroup_tools()
        install_sysstat()
        install_pstree()

        if Run("command -v setsebool", chk_err=False) == 0:
            Log("Set SELinux boolean value httpd_can_network_connect and allow_httpd_anon_write to true")
            Run("setsebool -P httpd_can_network_connect 1")
            Run("setsebool -P allow_httpd_anon_write 1")

        if Run("firewall-cmd --state", chk_err=False) == 0:
            Log("Configuring firewalld settings")
            Run("firewall-cmd --permanent --zone=public --add-port=40000/tcp")
            Run("firewall-cmd --permanent --zone=public --add-port=40002/tcp")
            Run("firewall-cmd --reload")
            Log("firewalld settings configured")

        Log("Starting the hpc node agent daemon")
        shutil.move(os.path.join(InstallRoot, "hpcagent.sh"), "/etc/init.d/hpcagent")
        if SupportSystemd:
            Run("systemctl enable hpcagent")
            errCode, msg = RunGetOutput("systemctl start hpcagent")
        else:
            if DistroName == "ubuntu":
                Run("update-rc.d hpcagent defaults")
            elif DistroName in ["centos", "redhat", "suse", "alma", "almalinux", "rocky", "rockylinux"]:
                Run("chkconfig --add hpcagent")
            else:
                raise Exception("unsupported Linux Distro")
            errCode, msg = RunGetOutput("service hpcagent start")
        if errCode == 0:
            Log("The hpc node agent was installed")
        else:
            Log("The hpc node agent failed to start: " + msg)
            sys.exit(1)
    except Exception as e:
        cleanup_hpc_agent(keepcert)
        Log("Failed to install hpc node agent: {0}, stack trace: {1}".format(e, traceback.format_exc()))
        sys.exit(1)

def get_dist_info():
    distroName = ''
    distroVersion = ''    
    if 'linux_distribution' in dir(platform):
        distinfo = platform.linux_distribution(full_distribution_name=0)
        distroName = distinfo[0].strip()
        distroVersion = distinfo[1]
    # if the distroName is empty we get from /etc/*-release
    if not distroName:
        errCode, info = RunGetOutput("cat /etc/*-release")
        if errCode != 0:
            raise Exception('Failed to get Linux Distro info by running command "cat /etc/*release", error code: {}'.format(errCode))
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
                elif 'almalinux' in line:
                    distroName = 'almalinux'
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
    return distroName.lower(), distroVersion

def main():
    t = time.localtime()
    global DistroName, DistroVersion, SetupLogFile, SupportSystemd
    DistroName, DistroVersion = get_dist_info()
    SupportSystemd = Run("command -v systemctl", chk_err=False) == 0

    if len(sys.argv) < 2:
        Usage()
        sys.exit(1)

    if re.match(r"^[-/]*(help|usage|\?)", sys.argv[1]):
        Usage()
        sys.exit(0)

    if os.geteuid() != 0:
        print("You must run this command as root user")
        sys.exit(1)

    if re.match("^[-/]*uninstall$", sys.argv[1]):
        SetupLogFile = "/root/hpcagent_uninstall_%04u%02u%02u-%02u%02u%02u.log" % (t.tm_year, t.tm_mon, t.tm_mday, t.tm_hour, t.tm_min, t.tm_sec)
        uninstall()
    elif re.match("^[-/]*update$", sys.argv[1]):
        SetupLogFile = "/root/hpcagent_update_%04u%02u%02u-%02u%02u%02u.log" % (t.tm_year, t.tm_mon, t.tm_mday, t.tm_hour, t.tm_min, t.tm_sec)
        update()
    elif re.match("^[-/]*install$", sys.argv[1]):
        SetupLogFile = "/root/hpcagent_install_%04u%02u%02u-%02u%02u%02u.log" % (t.tm_year, t.tm_mon, t.tm_mday, t.tm_hour, t.tm_min, t.tm_sec)
        install()
    elif re.match("^[-/]*updatecert$", sys.argv[1]):
        SetupLogFile = "/root/hpcagent_updatecert_%04u%02u%02u-%02u%02u%02u.log" % (t.tm_year, t.tm_mon, t.tm_mday, t.tm_hour, t.tm_min, t.tm_sec)
        updatecert()
    else:
        print("Invalid arguments")
        Usage()
        sys.exit(1)
    sys.exit(0)

def Run(cmd,chk_err=True):
    retcode,out=RunGetOutput(cmd,chk_err)
    return retcode

def RunGetOutput(cmd,chk_err=True):
    try:
        output=subprocess.check_output(cmd,stderr=subprocess.STDOUT,shell=True)
    except subprocess.CalledProcessError as e:
        if chk_err :
            Error('CalledProcessError.  Error Code is ' + str(e.returncode)  )
            Error('CalledProcessError.  Command result was ' + (e.output[:-1]).decode('latin-1'))
        return e.returncode,e.output.decode('latin-1')
    return 0,output.decode('latin-1')


def ReplaceStringInFile(fname,src,repl):
    """
    Replace 'src' with 'repl' in file.
    """
    try:
        updated=''
        with open(fname, 'r') as F:
            for line in F.readlines():
                n = line.replace(src, repl)
                updated += n
        ReplaceFileContentsAtomic(fname,updated)
    except :
        raise
    return


def ReplaceFileContentsAtomic(filepath, contents):
    """
    Write 'contents' to 'filepath' by creating a temp file, and replacing original.
    """
    handle, temp = tempfile.mkstemp(dir = os.path.dirname(filepath))
    if type(contents) == str :
        contents=contents.encode('latin-1')
    try:
        os.write(handle, contents)
    except IOError as e:
        Error('ReplaceFileContentsAtomic Writing to file ' + filepath + ' Exception is ' + str(e))
        return None
    finally:
        os.close(handle)
    try:
        os.rename(temp, filepath)
        return None
    except IOError as e:
        Error('ReplaceFileContentsAtomic Renaming ' + temp + ' to ' + filepath + ' Exception is ' + str(e))
    try:
        os.remove(filepath)
    except IOError as e:
        Error('ReplaceFileContentsAtomic Removing '+ filepath + ' Exception is ' + str(e))
    try:
        os.rename(temp,filepath)
    except IOError as e:
        Error('ReplaceFileContentsAtomic Removing '+ filepath + ' Exception is ' + str(e))
        return 1
    return 0

def SetFileContents(filepath, contents):
    """
    Write 'contents' to 'filepath'.
    """
    if type(contents) == str :
        contents=contents.encode('latin-1', 'ignore')
    try:
        with open(filepath, "wb+") as F :
            F.write(contents)
    except IOError as e:
        Error('Failed to Write to file ' + filepath + ' Exception is ' + str(e))
        return None
    return 0

def LogWithPrefix(prefix, message):
    t = time.localtime()
    t = "%04u/%02u/%02u %02u:%02u:%02u " % (t.tm_year, t.tm_mon, t.tm_mday, t.tm_hour, t.tm_min, t.tm_sec)
    t += prefix
    for line in message.split('\n'):
        line = t + line
        line = ''.join(filter(lambda x : x in string.printable, line))
        print(line)
        try:
            with open(SetupLogFile, "a") as F :
                F.write(line + "\n")
        except IOError as e:
            print(e)
            pass

def Log(message):
    LogWithPrefix("", message)

def Error(message):
    LogWithPrefix("ERROR:", message)
    
def Warn(message):
    LogWithPrefix("WARNING:", message)

if __name__ == '__main__':
    main()