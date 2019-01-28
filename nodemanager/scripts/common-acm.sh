#!/bin/bash

CGroupSubSys=cpuacct,cpuset,memory,freezer
CGInstalled=false
command -v cgexec > /dev/null 2>&1 && CGInstalled=true

function GetCGroupName
{
	local taskId=$1
	echo "acmgroup_$taskId"
}

function GetExistingTaskIdsInCGroup
{
	lscgroup | grep 'cpuset:/acmgroup_.*' | sed -e 's/.*acmgroup_\(.*\)/\1/' | uniq
}

function GetGroupPath
{
	local groupName=$1
	local subsys=$2
	local groupPath=$(lssubsys -am | grep $subsys | cut -d' ' -f2)
	echo $groupPath/$groupName
}

function GetGroupFile
{
	local groupName=$1
	local subsys=$2
	local fileName=$3
	echo "$(GetGroupPath "$groupName" "$subsys")"/"$fileName"
}

function GetCpusFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuset cpuset.cpus
}

function GetMemsFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuset cpuset.mems
}

function GetCpusetTasksFile
{
	local groupName=$1
	GetGroupFile "$groupName" cpuset tasks
}

function GetFreezerStateFile
{
	local groupName=$1
	GetGroupFile "$groupName" freezer freezer.state
}

MpiContainerSuffix="MPI"
DebugContainerSuffix="DEBUG"
TmpSshDir="/tmp/hpcSshKey/.ssh"
ContainerPlaceholderCommand="/bin/bash"

function GetContainerName
{
	local taskId=$1
	echo "acmcontainer_$taskId"
}

function GetCGroupNameOfDockerTask
{
	local containerId=$1
	local cgroupfsName="docker/$containerId"
	local systemdName="system.slice/docker-$containerId.scope"
	local testFile=$(GetCpusetTasksFile $cgroupfsName)
	if [ -f $testFile ]; then
		echo $cgroupfsName
	else
		echo $systemdName
	fi
}

function GetContainerPlaceholder
{
	local taskFolder=$1
	echo "$taskFolder/placeholder"
}

function GetDockerTaskEnvFile
{
	local taskFolder=$1
	echo "$taskFolder/environments"
}

function GetContainerIdFile
{
	local taskFolder=$1
	echo "$taskFolder/containerId"
}

function GetContainerId
{
	local taskFolder=$1
	cat $(GetContainerIdFile $taskFolder)
}

function CheckDockerEnvFileExist
{
	local taskFolder=$1
	if [ -f $(GetDockerTaskEnvFile $taskFolder) ]; then
		echo 1
	else
		echo 0
	fi
} 

function GetUserSshDir
{
	local userName=$1
	if [ "$userName" == "root" ]; then
		echo "/root/.ssh"
	else
		echo "/home/$userName/.ssh"
	fi
}

function GetMpiContainerStartOption
{
	local userName=$1
	local userSshDir=$(GetUserSshDir $userName)
	local sshDirMountOption="-v $userSshDir:$TmpSshDir:ro"
	local networkHostOption="--network host"
	echo "$sshDirMountOption $networkHostOption"
}

function CheckMpiTask
{
	local taskFolder=$1
	local nodeNum=$(cat $(GetDockerTaskEnvFile $taskFolder) | grep "CCP_NODES=" | sed -r 's/^CCP_NODES=([0-9]+) .*/\1/g')
	if [ "$nodeNum" -gt 1 ]; then
		echo 1
	else
		echo 0
	fi
}

function CheckDockerDebugMode
{
	local taskFolder=$1
	debugOption=$(cat $(GetDockerTaskEnvFile $taskFolder) | grep "CCP_DOCKER_DEBUG=" | cut -d '=' -f 2)
	if [ -z $debugOption ] || [ "$debugOption" == "0" ]; then
		echo 0
	else
		echo 1
	fi	
}

function GetDockerEngine
{
	local nvidiaOption=$1
	if [ -z $nvidiaOption ] || [ "$nvidiaOption" == "0" ]; then
		echo "docker"
	else
		local taskFolder=$1
		local nvidiaOption=$(cat $(GetDockerTaskEnvFile $taskFolder) | grep "CCP_DOCKER_NVIDIA=" | cut -d '=' -f 2)
		if [ -z $nvidiaOption ] || [ "$nvidiaOption" == "0" ]; then
			echo "docker"
		else
			echo "nvidia-docker"
		fi
	fi	
}

function GetDockerImageName
{
	local taskFolder=$1
	cat $(GetDockerTaskEnvFile $taskFolder) | grep "CCP_DOCKER_IMAGE=" | cut -d '=' -f 2
}

function GetDockerVolumeOption
{
	local taskFolder=$1
	cat $(GetDockerTaskEnvFile $taskFolder) | grep "CCP_DOCKER_VOLUMES=" | sed -e 's/^CCP_DOCKER_VOLUMES=/-v /g' -e 's/,/ -v /g'
}

function GetDockerAdditionalOption
{
	local taskFolder=$1
	cat $(GetDockerTaskEnvFile $taskFolder) | grep "CCP_DOCKER_START_OPTION=" | sed 's/^CCP_DOCKER_START_OPTION=//'
}

function GetSshStartCommand
{
	local version=$(python -mplatform) || local version=$(cat /etc/*release | grep NAME=)
	echo $version | grep -iq ubuntu && echo "service ssh start" || echo "service sshd start"
}

function GetSshStopCommand
{
	local version=$(python -mplatform) || local version=$(cat /etc/*release | grep NAME=)
	echo $version | grep -iq ubuntu && echo "service ssh stop" || echo "service sshd stop"
}
