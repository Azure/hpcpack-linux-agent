#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202

taskId=$1

containerName=$(GetContainerName "$taskId_$MpiContainerSuffix")

docker rm -f $containerName
$(GetSshStartCommand)