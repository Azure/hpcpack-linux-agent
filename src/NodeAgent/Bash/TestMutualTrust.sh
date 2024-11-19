#!/bin/bash

[ -z "$1" ] && echo "task execution id not specified" && exit 202
[ -z "$2" ] && echo "run directory not specified" && exit 202
[ -z "$3" ] && echo "user name not specified" && exit 202

taskId=$1
runDir=$2
userName=$3

trustLogFile="${runDir}/${taskId}_trust.txt"
failedTrustLogFile="${runDir}/failed_${taskId}_trust.txt"
trustKeysDir="${runDir}/${taskId}_${userName}/"
eval sshFolder=~${userName}/.ssh/

mkdir -p "$runDir" > /dev/null
/bin/bash $runDir/WaitForTrust.sh "$userName" "$taskId" "$runDir" > "$trustLogFile" 2>&1
if [ $? -ne 0 ]; then
	echo "Mutual trust failure." >&2
	mv "$trustLogFile" "$failedTrustLogFile"
	cp -rf "${sshFolder}" "$trustKeysDir"
	exit 203
fi