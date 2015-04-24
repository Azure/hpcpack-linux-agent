#!/bin/bash

. common.sh

[ -z "$2" ] && echo "run.sh not specified" && exit 202
[ -z "$3" ] && echo "user name not specified" && exit 202

/bin/bash WaitForTrust.sh "$3" || exit 203 && /bin/bash $2

