#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "affinity not specified" && exit 202

taskId=$1

if $CGInstalled; then
	groupName=$(GetCGroupName $taskId)
	group=$CGroupSubSys:$groupName

	maxLoop=3
	while [ $maxLoop -gt 0 ]
	do
		cgcreate -g $group
		ec=$?
		if [ $ec -eq 0 ]
		then
			break
		fi

		echo "Failed to create cgroup $group, error code $ec, retry after .5 seconds"
		((maxLoop--))
		sleep .5
	done

	if [ $ec -ne 0 ]
	then
		exit $ec
	fi

	maxLoop=3
	while [ $maxLoop -gt 0 ]
	do
		cpusFile=$(GetCpusFile $groupName)
		echo "$2" > $cpusFile
		ec=$?
		if [ $ec -eq 0 ]
		then
			break
		fi

		echo "Failed to set cpus for $group, error code $ec, retry after .5 seconds"
		((maxLoop--))
		sleep .5
	done

	if [ $ec -ne 0 ]
	then
		exit $ec
	fi

	maxLoop=3
	while [ $maxLoop -gt 0 ]
	do
		memsFile=$(GetMemsFile $groupName)
		echo 0 > $memsFile
		ec=$?
		if [ $ec -eq 0 ]
		then
			break
		fi

		echo "Failed to set mems for $group, error code $ec, retry after .5 seconds"
		((maxLoop--))
		sleep .5
	done	

	if [ $ec -ne 0 ]
	then
		exit $ec
	fi

	tasks=$(GetCpusetTasksFile $groupName)
	freezerState=$(GetFreezerStateFile $groupName)

	[ ! -f $tasks ] && echo "$tasks doesn't exist" && exit 200
	[ ! -f $freezerState ] && echo "$freezerState doesn't exist" && exit 201

	exit 0
fi

