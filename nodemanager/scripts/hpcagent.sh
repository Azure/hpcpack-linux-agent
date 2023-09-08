#! /bin/bash
#
# Init file for HPC Pack Linux node agent.
#
# chkconfig: 2345 99 10
# description: Daemon of the HPC Pack Linux node agent
#
# Author: Microsoft HPC Pack Team
#
### BEGIN INIT INFO
# Provides:          hpcagent
# Required-Start:    $remote_fs $syslog
# Required-Stop:     $remote_fs $syslog
# Default-Start:     2 3 4 5
# Default-Stop:      0 1 6
# Short-Description: HPC Pack Linux node agent
# Description:       Daemon of the HPC Pack Linux node manager
### END INIT INFO

RETVAL=0
FriendlyName="HPC Pack Linux node agent"
NAME=hpcagent
AgentPath=/opt/hpcnodemanager/$NAME
SCRIPTNAME=/etc/init.d/$NAME
PIDFILE=/var/run/hpcnmdaemon.pid

# Exit if not run as root
if [[ $EUID != 0 ]]; then
    echo "This script must be run as root"
    exit 1
fi

# Exit if the package is not installed
[ -x "$AgentPath" ] || exit 0

# Read configuration variable file if it is present
[ -r /etc/default/$NAME ] && . /etc/default/$NAME

if command -v python3 >/dev/null 2>&1 ; then
    PYTHONEXECUTOR="python3"
elif command -v python2 >/dev/null 2>&1 ; then
    PYTHONEXECUTOR="python2"
elif command -v /usr/libexec/platform-python >/dev/null 2>&1 ; then
    PYTHONEXECUTOR="/usr/libexec/platform-python"
fi

#
# Function that starts the daemon/service
#
do_start()
{
    local RC=0
    echo "Starting $FriendlyName"
    $PYTHONEXECUTOR $AgentPath enable
    RC=$?
    if [ $RC = 0 ]; then
      	echo "$FriendlyName was started"
    else
        echo "Failed to start $FriendlyName : $RC"
    fi
    return $RC    
}

#
# Function that stops the daemon/service
#
do_stop()
{
    local RC=0
    echo "Starting $FriendlyName"
    $PYTHONEXECUTOR $AgentPath disable
    RC=$?
    if [ $RC = 0 ]; then
      	echo "$FriendlyName was stopped"
    else
        echo "Failed to stop $FriendlyName : $RC"
    fi
    return $RC
}

#
# Function that restarts the daemon/service
#
do_restart()
{
    local RC=0
    echo "restarting $FriendlyName"
    $PYTHONEXECUTOR $AgentPath restart
    RC=$?    
    if [ $RC = 0 ]; then
      	echo "$FriendlyName was restarted"
    else
        echo "Failed to restart $FriendlyName : $RC"
    fi
    return $RC
}

do_status()
{
    local daemon_pid=0
    [ -r $PIDFILE ] && read daemon_pid < $PIDFILE
    if [ $daemon_pid != 0 ]; then
        local cmdline=`ps -p $daemon_pid -o cmd=`
        [[ "$cmdline" = *"$AgentPath"* ]] || daemon_pid=0
    fi
    if [ $daemon_pid = 0 ]; then
        echo "$FriendlyName is not running"
    else
        echo "$FriendlyName is running"
    fi
    return 0
}

case "$1" in
  start)
        do_start
        RETVAL=$?
        ;;
  stop)
        do_stop
        RETVAL=$?	
        ;;
  status)
        do_status
        RETVAL=$?
        ;;
  restart|reload|force-reload)
        do_restart
        RETVAL=$?
        ;;
  *)
        echo "Usage: $SCRIPTNAME {start|stop|status|restart|force-reload}" >&2
        RETVAL=3
        ;;
esac
exit $RETVAL