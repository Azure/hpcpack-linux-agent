#!/bin/bash

echo $CCP_NODES
hosts=($CCP_NODES)
nodes=()
testpids=()
userName=$1

echo "Preparing"

for host in ${!hosts[@]}
do
	if [ $(($host % 2 )) == 1 ]; then
		nodes+=(${hosts[$host]})
		echo "    adding ${hosts[$host]}"
	fi
done

if [ ${#nodes[@]} -lt 2 ]; then
	finished=true
	echo "no need to test trust for single node"
	exit 0
fi

for node in ${nodes[@]}
do
	sudo -u $userName ssh -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=3 $userName@$node echo 1 > /dev/null 2>&1 &
	testpids+=($!)
	echo "    created $! for $node"
done

start=$SECONDS
echo "start=$start"
finished=false
loopCount=0
while ! $finished && [ $((SECONDS-start)) -lt 30 ]
do
	((loopCount++))
	echo "looping $loopCount"
	finished=true
	for i in ${!testpids[@]}
	do
		echo "    pid is ${testpids[$i]}"
		wait ${testpids[$i]} && echo "    trusted ${nodes[$i]}" && unset testpids[$i] && unset nodes[$i]
		exitcode=$?
		echo "    exit code is $exitcode SECONDS=$SECONDS"
		if [ $exitcode != 0 ]; then
			finished=false
			sudo -u $userName ssh -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=3 $userName@${nodes[$i]} echo 1 > /dev/null 2>&1 &
			testpids[$i]=$!
			echo "    line: ssh -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=3 $userName@${nodes[$i]} echo 1 > /dev/null 2>&1 &"
			echo "    untrust ${nodes[$i]}, exitcode $exitcode, new pid $!"
		fi
		echo ""
	done

	echo "ending loop $loopCount, finished=$finished, SECONDS=$SECONDS"
	if ! $finished; then sleep 1; fi
done

echo "ending finished=$finished, SECONDS=$SECONDS, start=$start, elapsed=$((SECONDS-start))"

kill -9 $(jobs -p)

if $finished; then
	echo "all trusted"
	exit 0;
else
	echo "not all trusted"
	exit -1;
fi
