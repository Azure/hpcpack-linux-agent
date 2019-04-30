#!/bin/bash
# end the task with task id.

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "process id not specified" && exit 202

taskId=$1
processId=$2
taskFolder=$3

isDockerTask=$(CheckDockerEnvFileExist $taskFolder)
if $isDockerTask; then
	isDebugMode=$(CheckDockerDebugMode $taskFolder)
	if ! $isDebugMode; then
		containerId=$(GetContainerId $taskFolder)
		docker rm -f $containerId
	fi

	isMpiTask=$(CheckMpiTask $taskFolder)
	if $isMpiTask; then
		$(GetSshStartCommand)
		ec=$?
		if [ $ec -ne 0 ]
		then
			echo "Failed to start host ssh service."
			exit $ec
		fi	
	fi
	
	exit
fi

/bin/bash ./EndTask.sh "$taskId" "$processId" "1"

cgDisabled=$(CheckCgroupDisabledInFlagFile $taskFolder)
if $CGInstalled && ! $cgDisabled; then
	groupName=$(GetCGroupName "$taskId")
	group=$CGroupSubSys:$groupName
	cgdelete -g "$group"
fi
