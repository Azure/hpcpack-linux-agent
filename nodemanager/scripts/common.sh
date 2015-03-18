#!/bin/bash

CGroupSubSys=cpuset,freezer
CGroupRoot=/sys/fs/cgroup
CGInstalled=$([ -f /usr/bin/cgexec ])

function GetCGroupName
{
	local taskId=$1
	echo "nmgroup_$taskId"
}
