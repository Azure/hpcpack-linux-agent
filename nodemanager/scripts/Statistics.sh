#!/bin/bash
# collect the statistics of the cgroup

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "task folder not specified" && exit 202

taskId=$1
taskFolder=$2

isDockerTask=$(CheckDockerEnvFileExist $taskFolder)

userTime10Ms=0
kernelTime10Ms=0
processes=""
workingSetBytes=0

function GetCpuStatFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuacct cpuacct.stat
}

function GetCpuacctTasksFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuacct tasks
}

function GetMemoryMaxusageFile
{
	local groupName=$1
	GetGroupFile "$groupName" memory memory.max_usage_in_bytes
}

cgDisabled=$(CheckCgroupDisabledInFlagFile $taskFolder)
if $CGInstalled && [ "$cgDisabled" == "0" ]; then
	if [ "$isDockerTask" == "1" ]; then
		containerId=$(GetContainerId $taskFolder)
		groupName=$(GetCGroupNameOfDockerTask $containerId)
	else
		groupName=$(GetCGroupName "$taskId")
	fi
	
	statFile=$(GetCpuStatFile "$groupName")
	tasksFile=$(GetCpuacctTasksFile "$groupName")
	workingSetFile=$(GetMemoryMaxusageFile "$groupName")

	cut -d" " -f2 "$statFile"
	cat "$workingSetFile"

	if [ "$isDockerTask" == "1" ]; then
		containerPlaceholder=$(GetContainerPlaceholder $taskFolder)
		cat $tasksFile | sed "/^$(cat $containerPlaceholder)$/d" | tr "\\n" " " 
	else
		tr "\\n" " " < "$tasksFile"
	fi

	echo
else
    echo $userTime10Ms
    echo $kernelTime10Ms
    echo $workingSetBytes
    echo $processes
    echo
fi
