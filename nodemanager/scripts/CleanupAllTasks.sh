#!/bin/bash

. common.sh

if $CGInstalled; then
	groupNames=$(GetExistingCGroupNames)
	for gn in $groupNames;
	do
		echo "Cleaning up $gn"
		/bin/bash ./CleanupTask.sh "$gn" "0"
	done
	exit 0
fi

