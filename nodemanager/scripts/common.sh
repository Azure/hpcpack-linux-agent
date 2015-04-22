#!/bin/bash

CGroupSubSys=cpuset,freezer
CGroupRoot=/cgroup
[ -d $CGroupRoot ] || CGroupRoot=/sys/fs/cgroup

CGInstalled=false
command -v cgexec > /dev/null 2>&1 && CGInstalled=true

function GetCGroupName
{
	local taskId=$1
	echo "nmgroup_$taskId"
}

function GetCpusFile
{
	local groupName=$1
	[ "$CGroupRoot" == "/cgroup" ] && echo "$CGroupRoot/$groupName/cpuset.cpus" || echo "$CGroupRoot/cpuset/$groupName/cpuset.cpus"
}

function GetMemsFile
{
	local groupName=$1
	[ "$CGroupRoot" == "/cgroup" ] && echo "$CGroupRoot/$groupName/cpuset.mems" || echo "$CGroupRoot/cpuset/$groupName/cpuset.mems"
}

function GetCpusetTasksFile
{
	local groupName=$1
	[ "$CGroupRoot" == "/cgroup" ] && echo "$CGroupRoot/$groupName/tasks" || echo "$CGroupRoot/cpuset/$groupName/tasks"
}

function GetFreezerStateFile
{
	local groupName=$1
	[ "$CGroupRoot" == "/cgroup" ] && echo "$CGroupRoot/$groupName/freezer.state" || echo "$CGroupRoot/freezer/$groupName/freezer.state"
}

