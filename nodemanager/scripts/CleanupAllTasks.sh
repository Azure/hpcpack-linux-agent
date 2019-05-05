#!/bin/bash

. common.sh

echo

docker version > /dev/nul
if [ $? -eq 0 ]; then
	echo "Cleaning up docker containers..."
	containers=$(docker ps -a -q -f name=^/$(GetContainerName))
	[ -z "$containers" ] || docker rm -f $containers
	ec=$?
	if [ $ec -ne 0 ]
	then
		echo "Failed to cleanup docker containers. Exitcode: $ec"
	fi	
fi

if $CGInstalled; then
	echo "Cleaning up tasks in CGroup..."
	taskIds=$(GetExistingTaskIdsInCGroup)
	for taskId in $taskIds;
	do
		echo "$taskId"
		/bin/bash ./CleanupTask.sh "$taskId" "0"
	done
	exit 0
fi