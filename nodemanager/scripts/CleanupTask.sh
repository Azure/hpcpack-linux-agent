#!/bin/bash
# end the task with task id.

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "process id not specified" && exit 202
[ -z "$3" ] && echo "task folder not specified" && exit 202

taskId=$1
processId=$2
taskFolder=$3

isDockerTask=$(CheckDockerEnvFileExist $taskFolder)
if [ "$isDockerTask" == "1" ]; then
	isDebugMode=$(CheckDockerDebugMode $taskFolder)
	if [ "$isDebugMode" == "0" ]; then
		containerId=$(GetContainerId $taskFolder)
		docker rm -f $containerId
	fi

	isMpiTask=$(CheckMpiTask $taskFolder)
	if [ "$isMpiTask" == "1" ]; then
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

if $CGInstalled; then
	groupName=$(GetCGroupName "$taskId")
	group=$CGroupSubSys:$groupName
	cgdelete -g "$group"
fi
