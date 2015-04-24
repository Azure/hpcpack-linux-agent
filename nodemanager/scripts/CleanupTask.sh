#!/bin/bash
# end the task with task id.

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "process id not specified" && exit 202

taskId=$1
processId=$2

/bin/bash ./EndTask.sh "$1" "$2"

if $CGInstalled; then
	groupName=$(GetCGroupName $taskId)
	group=$CGroupSubSys:$groupName
	cgdelete -g $group
fi
