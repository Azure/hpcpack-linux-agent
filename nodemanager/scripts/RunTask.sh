#!/bin/bash

. common.sh

[ -z "$2" ] && echo "run.sh not specified" && exit 202
[ -z "$3" ] && echo "user name not specified" && exit 202

/bin/bash WaitForTrust.sh "$3" > /opt/hpcnodemanager/logs/trust.txt 2>&1 || (cp /opt/hpcnodemanager/logs/trust.txt /opt/hpcnodemanager/logs/${1}failed_trust.txt; mkdir /opt/hpcnodemanager/logs/${1}_${3} && cp -r /home/$3/.ssh/* /opt/hpcnodemanager/logs/$3/; exit 203) && /bin/bash $2

