#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "run.sh not specified" && exit 202
[ -z "$3" ] && echo "username not specified" && exit 202
[ -z "$4" ] && echo "task folder not specified" && exit 202

taskId=$1
runPath=$2
userName=$3
taskFolder=$4

cp {TestMutualTrust.sh,WaitForTrust.sh} $taskFolder

# Generate hostfile or machinefile for Intel MPI, Open MPI, MPICH or other MPI applications
export CCP_MPI_HOSTFILE=$taskFolder/mpi_hostfile
case "$CCP_MPI_HOSTFILE_FORMAT" in
    "1") echo $CCP_NODES_CORES | awk '{ORS=NR%2==0?":":"\n"}1' RS=" " | tail -n +2 > $CCP_MPI_HOSTFILE;;
    "2") echo $CCP_NODES_CORES | awk '{ORS=NR%2==0?" slots=":"\n"}1' RS=" " | tail -n +2 > $CCP_MPI_HOSTFILE;;
    "3") echo $CCP_NODES_CORES | awk '{ORS=NR%2==0?" ":"\n"}1' RS=" " | tail -n +2 > $CCP_MPI_HOSTFILE;;
    *) echo $CCP_NODES_CORES | tr ' ' '\n' | sed -n 'n;p' > $CCP_MPI_HOSTFILE;;
esac

isDockerTask=$(CheckDockerImageNameNotEmpty $taskFolder)
if $isDockerTask; then
	containerId=$(GetContainerId $taskFolder)
    docker exec $containerId /bin/bash -c "$taskFolder/TestMutualTrust.sh $taskId $taskFolder $userName" &&\
    docker exec -u $userName $containerId /bin/bash $runPath
    exit
fi

cgDisabled=$(CheckDisableCgroupSet $taskFolder)
if $CGInstalled && ! $cgDisabled; then
    groupName=$(GetCGroupName "$taskId")
    group=$CGroupSubSys:$groupName
    cgexec -g "$group" /bin/bash $taskFolder/TestMutualTrust.sh "$taskId" "$taskFolder" "$userName" && (\
    [ "$CCP_SWITCH_USER" == "1" ] && (cgexec -g "$group" su $userName -m -c "/bin/bash $runPath" || exit) ||\
    cgexec -g "$group" sudo -H -E -u $userName env "PATH=$PATH" /bin/bash $runPath)
else
    /bin/bash $taskFolder/TestMutualTrust.sh "$taskId" "$taskFolder" "$userName" && (\
    [ "$CCP_SWITCH_USER" == "1" ] && (su $userName -m -c "/bin/bash $runPath" || exit) ||\
    sudo -H -E -u $userName env "PATH=$PATH" /bin/bash $runPath)
fi
