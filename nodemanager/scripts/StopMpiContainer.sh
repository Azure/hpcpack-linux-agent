#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202

taskId=$1

containerName=$(GetContainerName "MPI_$taskId")

docker rm -f $containerName
/etc/init.d/ssh start
