#!/bin/bash

. common.sh

docker version > /dev/nul
if [ $? -eq 0 ]; then
	echo "Cleaning up docker containers..."
	docker rm -f $(docker ps -a -q -f name=^/$(GetContainerName)) 2>/dev/nul
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

