#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202

taskId=$1

containerName=$(GetContainerName "${taskId}_${MpiContainerSuffix}")
mpiContainerLabelSkipSshSetup=$(docker inspect --format '{{ index .Config.Labels "CCP_DOCKER_SKIP_SSH_SETUP"}}' $containerName)
docker rm -f $containerName
if [ "$mpiContainerLabelSkipSshSetup" == "0" ]
then
    $(GetSshStartCommand)
fi