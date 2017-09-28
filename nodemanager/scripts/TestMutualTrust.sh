#!/bin/bash

[ -z "$1" ] && echo "task execution id not specified" && exit 202
[ -z "$2" ] && echo "run directory not specified" && exit 202
[ -z "$3" ] && echo "user name not specified" && exit 202

runDir="$2"
trustLogFile="${runDir}/${1}_trust.txt"
failedTrustLogFile="${runDir}/failed_${1}_trust.txt"
trustKeysDir="${runDir}/${1}_${3}/"
sshFolder="/home/${3}/.ssh/"
if [ "$3" = "root" ]; then
	sshFolder=/root/.ssh/
fi

mkdir -p "$runDir" > /dev/null
/bin/bash $runDir/WaitForTrust.sh "$3" "$1" "$runDir" > "$trustLogFile" 2>&1
if [ $? -ne 0 ]; then
	echo "Mutual trust failure." >&2
	mv "$trustLogFile" "$failedTrustLogFile"
	mkdir -p "$trustKeysDir" > /dev/null
	cp -rf "${sshFolder}*" "$trustKeysDir"

	exit 203
fi