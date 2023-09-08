# Wrapper module for agent utility
#
# waagent is not written as a module. This wrapper module is created 
# to use the waagent code as a module.
#
# Copyright 2014 Microsoft Corporation
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
# Requires Python 2.7+
#

import subprocess
import time
import string
import re
import os
import tempfile


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


class Logger(object):
    """
    The Agent's logging assumptions are:
    For Log, and LogWithPrefix all messages are logged to the
    self.file_path and to the self.con_path.  Setting either path
    parameter to None skips that log.  If Verbose is enabled, messages
    calling the LogIfVerbose method will be logged to file_path yet
    not to con_path.  Error and Warn messages are normal log messages
    with the 'ERROR:' or 'WARNING:' prefix added.
    """

    def __init__(self,filepath,conpath,verbose=False):
        """
        Construct an instance of Logger.
        """
        self.file_path=filepath
        self.con_path=conpath
        self.verbose=verbose

    def ThrottleLog(self,counter):
        """
        Log everything up to 10, every 10 up to 100, then every 100.
        """
        return (counter < 10) or ((counter < 100) and ((counter % 10) == 0)) or ((counter % 100) == 0)

    def LogToFile(self,message):
        """
        Write 'message' to logfile.
        """
        if self.file_path:
            try:
                with open(self.file_path, "a") as F :
                    message = ''.join(filter(lambda x : x in string.printable, message))
                    F.write(message + "\n")
            except IOError as e:
                print(e)
                pass

    def LogToCon(self,message):
        """
        Write 'message' to /dev/console.
        This supports serial port logging if the /dev/console
        is redirected to ttys0 in kernel boot options.
        """
        if self.con_path:
            try:
                with open(self.con_path, "w") as C :
                    message = ''.join(filter(lambda x : x in string.printable, message))
                    C.write(message + "\n")
            except IOError as e:
                print(e)
                pass

    def Log(self,message):
        """
        Standard Log function.
        Logs to self.file_path, and con_path
        """
        self.LogWithPrefix("", message)

    def LogWithPrefix(self,prefix, message):
        """
        Prefix each line of 'message' with current time+'prefix'.
        """
        t = time.localtime()
        t = "%04u/%02u/%02u %02u:%02u:%02u " % (t.tm_year, t.tm_mon, t.tm_mday, t.tm_hour, t.tm_min, t.tm_sec)
        t += prefix
        for line in message.split('\n'):
            line = t + line
            self.LogToFile(line)
            self.LogToCon(line)

    def NoLog(self,message):
        """
        Don't Log.
        """
        pass

    def LogIfVerbose(self,message):
        """
        Only log 'message' if global Verbose is True.
        """
        self.LogWithPrefixIfVerbose('',message)

    def LogWithPrefixIfVerbose(self,prefix, message):
        """
        Only log 'message' if global Verbose is True.
        Prefix each line of 'message' with current time+'prefix'.
        """
        if self.verbose == True:
            t = time.localtime()
            t = "%04u/%02u/%02u %02u:%02u:%02u " % (t.tm_year, t.tm_mon, t.tm_mday, t.tm_hour, t.tm_min, t.tm_sec)
            t += prefix
            for line in message.split('\n'):
                line = t + line
                self.LogToFile(line)
                self.LogToCon(line)

    def Warn(self,message):
        """
        Prepend the text "WARNING:" to the prefix for each line in 'message'.
        """
        self.LogWithPrefix("WARNING:", message)

    def Error(self,message):
        """
        Call ErrorWithPrefix(message).
        """
        self.ErrorWithPrefix("", message)

    def ErrorWithPrefix(self,prefix, message):
        """
        Prepend the text "ERROR:" to the prefix for each line in 'message'.
        Errors written to logfile, and /dev/console
        """
        self.LogWithPrefix("ERROR:", message)

def LoggerInit(log_file_path,log_con_path,verbose=False):
    """
    Create log object and export its methods to global scope.
    """
    global Log,LogWithPrefix,LogIfVerbose,LogWithPrefixIfVerbose,Error,ErrorWithPrefix,Warn,NoLog,ThrottleLog,myLogger
    l=Logger(log_file_path,log_con_path,verbose)
    Log,LogWithPrefix,LogIfVerbose,LogWithPrefixIfVerbose,Error,ErrorWithPrefix,Warn,NoLog,ThrottleLog,myLogger = l.Log,l.LogWithPrefix,l.LogIfVerbose,l.LogWithPrefixIfVerbose,l.Error,l.ErrorWithPrefix,l.Warn,l.NoLog,l.ThrottleLog,l
    return myLogger

def GetFileContents(filepath,asbin=False):
    """
    Read and return contents of 'filepath'.
    """
    mode='r'
    if asbin:
        mode+='b'
    c=None
    try:
        with open(filepath, mode) as F :
            c=F.read()
    except IOError as e:
        ErrorWithPrefix('GetFileContents','Reading from file ' + filepath + ' Exception is ' + str(e))
        return None
    return c

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
        ErrorWithPrefix('SetFileContents','Writing to file ' + filepath + ' Exception is ' + str(e))
        return None
    return 0

def AppendFileContents(filepath, contents):
    """
    Append 'contents' to 'filepath'.
    """
    if type(contents) == str :
        contents=contents.encode('latin-1')
    try:
        with open(filepath, "a+") as F :
            F.write(contents)
    except IOError as e:
        ErrorWithPrefix('AppendFileContents','Appending to file ' + filepath + ' Exception is ' + str(e))
        return None
    return 0


def FindStringInFile(fname,matchs):
    """
    Return match object if found in file.
    """
    try:
        ms=re.compile(matchs)
        for l in (open(fname,'r')).readlines():
            m=re.search(ms,l)
            if m:
                return m
    except:
        raise

    return None

def ReplaceStringInFile(fname,src,repl):
    """
    Replace 'src' with 'repl' in file.
    """
    try:
        sr=re.compile(src)
        if FindStringInFile(fname,src):
            updated=''
            for l in (open(fname,'r')).readlines():
                n=re.sub(sr,repl,l)
                updated+=n
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
        ErrorWithPrefix('ReplaceFileContentsAtomic','Writing to file ' + filepath + ' Exception is ' + str(e))
        return None
    finally:
        os.close(handle)
    try:
        os.rename(temp, filepath)
        return None
    except IOError as e:
        ErrorWithPrefix('ReplaceFileContentsAtomic','Renaming ' + temp + ' to ' + filepath + ' Exception is ' + str(e))
    try:
        os.remove(filepath)
    except IOError as e:
        ErrorWithPrefix('ReplaceFileContentsAtomic','Removing '+ filepath + ' Exception is ' + str(e))
    try:
        os.rename(temp,filepath)
    except IOError as e:
        ErrorWithPrefix('ReplaceFileContentsAtomic','Removing '+ filepath + ' Exception is ' + str(e))
        return 1
    return 0

def Run(cmd,chk_err=True):
    """
    Calls RunGetOutput on 'cmd', returning only the return code.
    If chk_err=True then errors will be reported in the log.
    If chk_err=False then errors will be suppressed from the log.
    """
    retcode,out=RunGetOutput(cmd,chk_err)
    return retcode

def RunGetOutput(cmd,chk_err=True):
    """
    Wrapper for subprocess.check_output.
    Execute 'cmd'.  Returns return code and STDOUT, trapping expected exceptions.
    Reports exceptions to Error if chk_err parameter is True
    """
    LogIfVerbose(cmd)
    try:
        output=subprocess.check_output(cmd,stderr=subprocess.STDOUT,shell=True)
    except subprocess.CalledProcessError as e:
        if chk_err :
            Error('CalledProcessError.  Error Code is ' + str(e.returncode)  )
            Error('CalledProcessError.  Command string was ' + e.cmd  )
            Error('CalledProcessError.  Command result was ' + (e.output[:-1]).decode('latin-1'))
        return e.returncode,e.output.decode('latin-1')
    return 0,output.decode('latin-1')

