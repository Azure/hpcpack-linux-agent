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
runDir=$(GetParentDir $runPath)
cp {TestMutualTrust.sh,WaitForTrust.sh} $runDir

if [ "$isDockerTask" == "1" ]; then
    containerName=$(GetContainerName $taskId)
    docker exec $containerName /bin/bash -c "$runDir/TestMutualTrust.sh $taskId $runDir $userName" &&\
    docker exec -u $userName $containerName /bin/bash $runPath
    exit
fi

if $CGInstalled; then
    groupName=$(GetCGroupName "$taskId")
    group=$CGroupSubSys:$groupName
    cgexec -g "$group" /bin/bash $runDir/TestMutualTrust.sh "$taskId" "$runDir" "$userName" &&\
    cgexec -g "$group" /bin/bash $runPath
else
    /bin/bash $runDir/TestMutualTrust.sh "$taskId" "$runDir" "$userName" &&\
    /bin/bash $runPath
fi
