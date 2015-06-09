#!/bin/bash

. common.sh

[ -z "$1" ] && echo "task execution id not specified" && exit 202
[ -z "$2" ] && echo "run.sh not specified" && exit 202
[ -z "$3" ] && echo "user name not specified" && exit 202

rootLogFolder=/opt/hpcnodemanager/logs
trustLogFile=${rootLogFolder}/${1}_trust.txt
failedTrustLogFile=${rootLogFolder}/failed_${1}_trust.txt
trustKeysDir=${rootLogFolder}/${1}_${3}/
sshFolder=/home/${3}/.ssh/
if [ "$3" = "root" ]; then
	sshFolder=/root/.ssh/
fi

/bin/bash WaitForTrust.sh "$3" "$1" > $trustLogFile 2>&1
if [ $? -ne 0 ]; then
	mv $trustLogFile $failedTrustLogFile;
	mkdir -p $trustKeysDir > /dev/null
	cp -rf ${sshFolder}* $trustKeysDir;

	exit 203
fi

/bin/bash $2

