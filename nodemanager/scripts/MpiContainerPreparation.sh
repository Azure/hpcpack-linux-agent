#!/bin/bash

. common.sh

container=$1
userName=$2

userSshDir=$(GetUserSshDir $userName)

docker exec $container cp -rT $TmpSshDir $userSshDir 2>&1
ec1=$?
docker exec $container chown -R $userName $userSshDir 2>&1
ec2=$?
docker exec $container rm $userSshDir/known_hosts
if [ $ec1 -ne 0 ] || [ $ec2 -ne 0 ]
then
    echo "Failed to set container ssh key. $ec1, $ec2"
    exit 210
fi

$(GetSshStopCommand)
ec=$?
if [ $ec -ne 0 ]
then
    echo "Failed to stop host ssh server"
    exit $ec
fi		

# need to change along with various linux images
docker exec $container /etc/init.d/ssh start
ec=$?
if [ $ec -ne 0 ]
then
    echo "Failed to start container ssh server"
    exit $ec
fi	

docker exec $container mount -a
