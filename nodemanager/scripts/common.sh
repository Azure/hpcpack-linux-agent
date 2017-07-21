#!/bin/bash

CGroupSubSys=cpuacct,cpuset,memory,freezer
CGInstalled=false
command -v cgexec > /dev/null 2>&1 && CGInstalled=true

function GetContainerName
{
	local taskId=$1
	echo "hpcTask_$taskId"
}

function GetCGroupNameOfDockerTask
{
	local taskId=$1
	local containerId=$(docker ps -a -q --no-trunc -f name=^/$(GetContainerName $taskId)$)
	local cgroupfsName="docker/$containerId"
	local systemdName="system.slice/docker-$containerId.scope"
	local testFile=$(GetCpusetTasksFile $cgroupfsName)
	if [ -f $testFile ]; then
		echo $cgroupfsName
	else
		echo $systemdName
	fi
}

function GetContainerPlaceholder
{
	local taskFolder=$1
	echo "$taskFolder/placeholder"
}

function GetCGroupName
{
	local taskId=$1
	echo "nmgroup_$taskId"
}

function GetExistingTaskIdsInCGroup
{
	lscgroup | grep 'cpuset:/nmgroup_.*' | sed -e 's/.*nmgroup_\(.*\)/\1/' | uniq
}

function GetGroupPath
{
	local groupName=$1
	local subsys=$2
	local groupPath=$(lssubsys -am | grep $subsys | cut -d' ' -f2)
	echo $groupPath/$groupName
}

function GetGroupFile
{
	local groupName=$1
	local subsys=$2
	local fileName=$3
	echo "$(GetGroupPath "$groupName" "$subsys")"/"$fileName"
}

function GetCpusFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuset cpuset.cpus
}

function GetMemsFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuset cpuset.mems
}

function GetCpusetTasksFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuset tasks
}

function GetFreezerStateFile
{
	local groupName=$1
	GetGroupFile "$groupName" freezer freezer.state
}

function CheckNotEmpty
{
	if [ ! -z $1 ]; then
		echo 1
	else
		echo 0
	fi
}