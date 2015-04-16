#!/bin/bash

CGroupSubSys=cpuset,freezer
CGroupRoot=/sys/fs/cgroup
CGInstalled=false
command -v cgexec > /dev/null 2>&1 && CGInstalled=true

function GetCGroupName
{
	local taskId=$1
	echo "nmgroup_$taskId"
}
