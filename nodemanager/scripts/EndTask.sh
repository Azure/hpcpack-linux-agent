#!/bin/bash
# end the task with task id.

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 2
[ -z "$2" ] && echo "process id not specified" && exit 2

taskId=$1
processId=$2

if $CGInstalled; then
	groupName=$(GetCGroupName $taskId)
	group=$CGroupSubSys:$groupName
	tasks=$CGroupRoot/cpuset/$groupName/tasks
	freezerState=$CGroupRoot/freezer/$groupName/freezer.state

	[ ! -f $tasks ] && echo "task is ended" && exit 1
	[ ! -f $freezerState ] && echo "task is ended" && exit 1

	# freeze the task
	echo FROZEN > $freezerState

	while [ -f $freezerState ] && ! grep -Fxq FROZEN $freezerState; do
		echo FROZEN > $freezerState
		sleep .1
	done

	# kill all tasks
	for pid in $(cat $tasks); do
		[ -d /proc/$pid ] && kill -TERM $pid
	done

	# resume tasks
	echo THAWED > $freezerState

	while [ -f $freezerState ] && ! grep -Fxq THAWED $freezerState; do
		echo THAWED > $freezerState
		sleep .1
	done
else
	kill -s TERM $(pstree -l -p $taskId | grep "([[:digit:]]*)" -o | tr -d '()')
fi

exit 0
