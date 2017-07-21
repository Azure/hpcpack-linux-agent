#!/bin/bash
# end the task with task id.

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "process id not specified" && exit 202

taskId=$1
processId=$2

isDockerTask=$(CheckNotEmpty $3)

if [ "$isDockerTask" == "1" ]; then
	docker rm -f $(GetContainerName $taskId)
	exit
fi

/bin/bash ./EndTask.sh "$taskId" "$processId" "1"

if $CGInstalled; then
	groupName=$(GetCGroupName "$taskId")
	group=$CGroupSubSys:$groupName
	cgdelete -g "$group"
fi
