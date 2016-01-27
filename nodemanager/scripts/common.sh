#!/bin/bash

CGroupSubSys=cpuacct,cpuset,memory,freezer
CGInstalled=false
command -v cgexec > /dev/null 2>&1 && CGInstalled=true

function GetCGroupName
{
	local taskId=$1
	echo "nmgroup_$taskId"
}

function GetExistingCGroupNames
{
	lscgroup | grep 'cpuset:/nmgroup_.*' | sed -e 's/.*nmgroup_\(.*\)/\1/' | uniq
}

function GetGroupPath
{
	local groupName=$1
	local subsys=$2
	local groupPath=$(mount | grep cgroup | grep $subsys | cut -d' ' -f3)
	[ -z $groupPath ] && groupPath=$(mount | grep cgroup | cut -d' ' -f3)
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

