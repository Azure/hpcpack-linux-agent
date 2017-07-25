#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "user name not specified" && exit 202
[ -z "$3" ] && echo "docker image not specified" && exit 202

taskId=$1
userName=$2
dockerImage=$3

containerName=$(GetContainerName "MPI_$taskId")
mpiContainerStartOption=$(GetMpiContainerStartOption $userName)

docker run -id \
        --name $containerName \
        $mpiContainerStartOption \
        $dockerImage $containerPlaceholderCommand 2>&1

if [ $? -ne 0 ]
then
    exit $ec
fi

docker exec $containerName useradd -m $userName
/bin/bash MpiContainerPreparation.sh $containerName $userName
