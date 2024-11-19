#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "affinity not specified" && exit 202
[ -z "$3" ] && echo "task folder not specified" && exit 202
[ -z "$4" ] && echo "user name not specified" && exit 202

taskId=$1
affinity=$2
taskFolder=$3
userName=$4

isDockerTask=$(CheckDockerEnvFileExist $taskFolder)
if $isDockerTask; then
	isMpiTask=$(CheckMpiTask $taskFolder)
	skipSshSetup=$(CheckSkipSshSetup $taskFolder)
	if $isMpiTask; then
		mpiContainerStartOption=$(GetMpiContainerStartOption $userName)
	fi

	if $skipSshSetup; then
		mpiContainerStartOption=""
	fi

	isDebugMode=$(CheckDockerDebugMode $taskFolder)
	if $isDebugMode; then
		taskId=${taskId}_${DebugContainerSuffix}
	fi

	containerName=$(GetContainerName $taskId)
	dockerImage=$(GetDockerImageName $taskFolder)
	volumeOption=$(GetDockerVolumeOption $taskFolder)
	additionalOption=$(GetDockerAdditionalOption $taskFolder)
	envFile=$(GetDockerTaskEnvFile $taskFolder)
	containerIdFile=$(GetContainerIdFile $taskFolder)
	dockerEngine=$(GetDockerEngine $taskFolder)
	$dockerEngine run -id \
				$additionalOption \
				$volumeOption \
				$mpiContainerStartOption \
				--name $containerName \
				--cpuset-cpus $affinity \
				--env-file $envFile \
				--cidfile $containerIdFile \
				-v $taskFolder:$taskFolder:z \
				$dockerImage 2>&1
	
	ec=$?
	if [ $ec -ne 0 ]
	then
		echo "Failed to start docker container"
		exit $ec
	fi	

	containerId=$(GetContainerId $taskFolder)
	docker exec $containerId useradd -m $userName
    docker exec $containerId chown $userName $taskFolder
	if $isMpiTask && ! $skipSshSetup; then
		/bin/bash MpiContainerPreparation.sh $containerId $userName
	fi

	exit
fi

cgDisabled=$(CheckCgroupDisabledInFlagFile $taskFolder)
if $CGInstalled && ! $cgDisabled; then
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
		echo "$affinity" > "$cpusFile"
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
		numaMaxIndex=$((`lscpu | grep 'NUMA node(s)' | awk '{print $NF}'` - 1))
		echo 0-$numaMaxIndex > "$memsFile"
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

