#!/bin/bash

. common.sh

containerName=$1
userName=$2

userSshDir=$(GetUserSshDir $userName)

docker exec $containerName /bin/cp -r $tmpSshDir $userSshDir 2>&1
ec1=$?
docker exec $containerName /bin/chown -R $userName $userSshDir 2>&1
ec2=$?
if [ $ec1 -ne 0 ] || [ $ec2 -ne 0 ]
then
    echo "Failed to set container ssh key"
    exit $ec
fi

/etc/init.d/ssh stop
ec=$?
if [ $ec -ne 0 ]
then
    echo "Failed to stop host ssh server"
    exit $ec
fi		

docker exec $containerName /etc/init.d/ssh start
ec=$?
if [ $ec -ne 0 ]
then
    echo "Failed to start container ssh server"
    exit $ec
fi	