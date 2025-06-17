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
workingSetBytes=0
processes=""

function GetCpuStatFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuacct cpuacct.stat
}

function GetCpuStatFileV2
{
	local groupName=$1
	GetGroupFileV2 "$groupName" cpu.stat
}

function GetMemoryMaxusageFile
{
	local groupName=$1
	GetGroupFile "$groupName" memory memory.max_usage_in_bytes
}

function GetMemoryMaxusageFileV2
{
	local groupName=$1
	GetGroupFileV2 "$groupName" memory.peak
}

cgDisabled=$(CheckCgroupDisabledInFlagFile $taskFolder)
if ! $cgDisabled; then
	if ! $CGroupV1; then
		if $isDockerTask; then
			containerId=$(GetContainerId $taskFolder)
			groupName=$(GetCGroupNameOfDockerTaskV2 $containerId)
		else
			groupName=$(GetCGroupName "$taskId")
		fi
	
		statFile=$(GetCpuStatFileV2 "$groupName")
		workingSetFile=$(GetMemoryMaxusageFileV2 "$groupName")
		tasksFile=$(GetCpusetTasksFileV2 "$groupName")

		read ignore tempUserTime < <(sed -n 2p "$statFile")
		[ -z "$tempUserTime" ] || userTime10Ms=$((tempUserTime / 10000))
		read ignore tempKernelTime < <(sed -n 3p "$statFile")
		[ -z "$tempKernelTime" ] || kernelTime10Ms=$((tempKernelTime / 10000))
		tempWorkingSet=`cat "$workingSetFile"`
		[ -z "$tempWorkingSet" ] || workingSetBytes=$tempWorkingSet
		tempProcesses=`cat "$tasksFile"`
		[ -z "$tempProcesses" ] || processes=$tempProcesses
	elif $CGInstalled; then
	if $isDockerTask; then
		containerId=$(GetContainerId $taskFolder)
		groupName=$(GetCGroupNameOfDockerTask $containerId)
	else
		groupName=$(GetCGroupName "$taskId")
	fi
	
	statFile=$(GetCpuStatFile "$groupName")
	workingSetFile=$(GetMemoryMaxusageFile "$groupName")
	tasksFile=$(GetCpusetTasksFile "$groupName")

	read ignore tempUserTime < <(sed -n 1p "$statFile")
	[ -z "$tempUserTime" ] || userTime10Ms=$tempUserTime
	read ignore tempKernelTime < <(sed -n 2p "$statFile")
	[ -z "$tempKernelTime" ] || kernelTime10Ms=$tempKernelTime
	tempWorkingSet=`cat "$workingSetFile"`
	[ -z "$tempWorkingSet" ] || workingSetBytes=$tempWorkingSet
	tempProcesses=`cat "$tasksFile"`
	[ -z "$tempProcesses" ] || processes=$tempProcesses
	fi
fi

echo $userTime10Ms
echo $kernelTime10Ms
echo $workingSetBytes
echo $processes
echo
