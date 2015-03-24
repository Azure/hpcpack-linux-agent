#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 2
[ -z "$2" ] && echo "affinity not specified" && exit 2

taskId=$1

if $CGInstalled; then
    groupName=$(GetCGroupName $taskId)
    group=$CGroupSubSys:$groupName
    cgcreate -g $group
    echo "$2" > $CGroupRoot/cpuset/$groupName/cpuset.cpus
    echo 0 > $CGroupRoot/cpuset/$groupName/cpuset.mems
fi

