#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "run.sh not specified" && exit 202
[ -z "$3" ] && echo "username not specified" && exit 202

taskId=$1
runPath=$2
userName=$3

dockerImage=$4
isDockerTask=$(CheckNotEmpty $dockerImage)

if [ "$isDockerTask" == "1" ]; then
    containerName=$(GetContainerName $taskId)
    docker exec $containerName useradd $userName 2> /dev/null
    docker exec -u $userName $containerName /bin/bash $runPath
    exit
fi

if $CGInstalled; then
    groupName=$(GetCGroupName "$taskId")
    group=$CGroupSubSys:$groupName
    cgexec -g "$group" /bin/bash RunTask.sh "$@"
else
    /bin/bash RunTask.sh "$@"
fi

