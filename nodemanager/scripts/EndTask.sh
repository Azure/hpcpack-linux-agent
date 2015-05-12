#!/bin/bash
# end the task with task id.

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "process id not specified" && exit 202

taskId=$1
processId=$2

if $CGInstalled; then
	groupName=$(GetCGroupName $taskId)
	group=$CGroupSubSys:$groupName
	tasks=$(GetCpusetTasksFile $groupName)
	freezerState=$(GetFreezerStateFile $groupName)

	[ ! -f $tasks ] && echo "$tasks doesn't exist" && exit 200
	[ ! -f $freezerState ] && echo "$freezerState doesn't exist" && exit 201

	# freeze the task
	echo FROZEN > $freezerState

	maxLoop=20
	while [ -f $freezerState ] && ! grep -Fxq FROZEN $freezerState && [ $maxLoop -gt 0 ]
	do
		sleep .1
		((maxLoop--))
	done

	# kill all tasks
	for pid in $(cat $tasks);
	do
		[ -d /proc/$pid ] && kill -9 $pid
	done

	# resume tasks
	echo THAWED > $freezerState

	maxLoop=20
	while [ -f $freezerState ] && ! grep -Fxq THAWED $freezerState && [ $maxLoop -gt 0 ]
	do
		sleep .1
		((maxLoop--))
	done
else
	kill -s 9 $(pstree -l -p $processId | grep "([[:digit:]]*)" -o | tr -d '()')
fi

exit 0
