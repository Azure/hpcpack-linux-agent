#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 2
[ -z "$2" ] && echo "run.sh not specified" && exit 2

taskId=$1

if $CGInstalled; then
    groupName=$(GetCGroupName $taskId)
    group=$CGroupSubSys:$groupName
    cgexec -g $group /bin/bash $2
else
    /bin/bash $2
fi

