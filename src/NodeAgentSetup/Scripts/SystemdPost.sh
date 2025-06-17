#!/bin/bash

mkdir /sys/fs/cgroup/hpcpack.slice/hpcagent.service/nodemanager

Ids=$(cat /sys/fs/cgroup/hpcpack.slice/hpcagent.service/cgroup.procs)

for Id in $Ids; do
  echo $Id > /sys/fs/cgroup/hpcpack.slice/hpcagent.service/nodemanager/cgroup.procs
done

echo "+cpu +cpuset +memory" > /sys/fs/cgroup/hpcpack.slice/hpcagent.service/cgroup.subtree_control