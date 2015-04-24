#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task id not specified" && exit 202
[ -z "$2" ] && echo "run.sh not specified" && exit 202
[ -z "$3" ] && echo "username not specified" && exit 202

taskId=$1

if $CGInstalled; then
    groupName=$(GetCGroupName $taskId)
    group=$CGroupSubSys:$groupName
    cgexec -g $group /bin/bash RunTask.sh $*
else
    /bin/bash RunTask.sh $*
fi

