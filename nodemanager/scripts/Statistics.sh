#!/bin/bash
# collect the statistics of the cgroup

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202

taskId=$1

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
	echo GetGroupFile "$groupName" cpuacct tasks
}

function GetMemoryMaxusageFile
{
	local groupName=$1
	echo GetGroupFile "$groupName" memory memory.max_usage_in_bytes
}

if $CGInstalled; then
	groupName=$(GetCGroupName "$taskId")
	statFile=$(GetCpuStatFile "$groupName")
	tasksFile=$(GetCpuacctTasksFile "$groupName")
	workingSetFile=$(GetMemoryMaxusageFile "$groupName")

	cut -d" " -f2 "$statFile"
	cat "$workingSetFile"
	tr "\\n" " " < "$tasksFile"
	echo
else
    echo $userTime10Ms
    echo $kernelTime10Ms
    echo $workingSetBytes
    echo $processes
    echo
fi
