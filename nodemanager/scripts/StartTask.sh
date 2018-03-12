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

isDockerTask=$(CheckDockerEnvFileExist $taskFolder)
if [ "$isDockerTask" == "1" ]; then
	containerId=$(GetContainerId $taskFolder)
    docker exec $containerId /bin/bash -c "$taskFolder/TestMutualTrust.sh $taskId $taskFolder $userName" &&\
    docker exec -u $userName $containerId /bin/bash $runPath
    exit
fi

if $CGInstalled; then
    groupName=$(GetCGroupName "$taskId")
    group=$CGroupSubSys:$groupName
    cgexec -g "$group" /bin/bash $taskFolder/TestMutualTrust.sh "$taskId" "$taskFolder" "$userName" &&\
    cgexec -g "$group" sudo -H -E -u $userName env "PATH=$PATH" /bin/bash $runPath
else
    /bin/bash $taskFolder/TestMutualTrust.sh "$taskId" "$taskFolder" "$userName" &&\
    sudo -H -E -u $userName env "PATH=$PATH" /bin/bash $runPath
fi
