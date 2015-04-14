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

	[ ! -f $tasks ] && echo "$tasks doesn't exist" && exit -200
	[ ! -f $freezerState ] && echo "$freezerState doesn't exist" && exit -201

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
		[ -d /proc/$pid ] && kill -TERM $pid
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
	kill -s TERM $(pstree -l -p $processId | grep "([[:digit:]]*)" -o | tr -d '()')
fi

exit 0
