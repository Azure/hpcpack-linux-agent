#!/bin/bash

CGroupSubSys=cpuset,freezer
CGroupRoot=/sys/fs/cgroup
CGInstalled=false
if [ -f /usr/bin/cgexec ]; then
	CGInstalled=true
fi

function GetCGroupName
{
	local taskId=$1
	echo "nmgroup_$taskId"
}
