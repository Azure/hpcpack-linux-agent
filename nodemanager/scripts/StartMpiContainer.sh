#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "user name not specified" && exit 202
[ -z "$3" ] && echo "docker image not specified" && exit 202

taskId=$1
userName=$2
dockerImage=$3
nvidiaOption=$4
additionalOption=$5
skipSshSetup=$6

containerName=$(GetContainerName "${taskId}_${MpiContainerSuffix}")
dockerEngine=$(GetDockerEngine $nvidiaOption)
mpiContainerStartOption=$(GetMpiContainerStartOption $userName)
mpiContainerLabelSkipSshSetup="CCP_DOCKER_SKIP_SSH_SETUP=0"
if [ "$skipSshSetup" == "1" ]
then
    mpiContainerStartOption=""
    mpiContainerLabelSkipSshSetup="CCP_DOCKER_SKIP_SSH_SETUP=1"
fi

$dockerEngine run -id \
            $additionalOption \
            --name $containerName \
            $mpiContainerStartOption \
            --label "$mpiContainerLabelSkipSshSetup" \
            $dockerImage 2>&1

ec=$?
if [ $ec -ne 0 ]
then
    echo "Failed to start docker container"
    exit $ec
fi

docker exec $containerName useradd -m $userName
if [ "$skipSshSetup" != "1" ]
then
    /bin/bash MpiContainerPreparation.sh $containerName $userName
fi

