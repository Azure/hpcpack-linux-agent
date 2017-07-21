#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "affinity not specified" && exit 202

taskId=$1
affinity=$2

# for docker command
taskFolder=$3
dockerImage=$4

isDockerTask=$(CheckNotEmpty $dockerImage)

if [ "$isDockerTask" == "1" ]; then
	placeholderCommand="/bin/bash"
	docker run -id --name $(GetContainerName $taskId) --cpuset-cpus $affinity -v $taskFolder:$taskFolder:z $dockerImage $placeholderCommand 2>&1
	
	ec=$?
	if [ $ec -ne 0 ]
	then
		exit $ec
	fi	

	groupName=$(GetCGroupNameOfDockerTask $taskId)
	tasks=$(GetCpusetTasksFile "$groupName")
	cat $tasks > $(GetContainerPlaceholder $taskFolder)

	ec=$?
	if [ $ec -ne 0 ]
	then
		echo "Failed to set docker container placeholder $tasks"
		exit $ec
	fi	
fi

if $CGInstalled; then
	groupName=$(GetCGroupName "$taskId")
	group=$CGroupSubSys:$groupName

	maxLoop=3
	while [ $maxLoop -gt 0 ]
	do
		cgcreate -g "$group"
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
		cpusFile=$(GetCpusFile "$groupName")
		echo "$2" > "$cpusFile"
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
		memsFile=$(GetMemsFile "$groupName")
		echo 0 > "$memsFile"
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

	tasks=$(GetCpusetTasksFile "$groupName")
	freezerState=$(GetFreezerStateFile "$groupName")

	[ ! -f "$tasks" ] && echo "$tasks doesn't exist" && exit 200
	[ ! -f "$freezerState" ] && echo "$freezerState doesn't exist" && exit 201

	exit 0
fi

