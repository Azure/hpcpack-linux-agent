#!/bin/bash

echo $CCP_NODES
hosts=($CCP_NODES)
nodes=()
testpids=()
userName=$1
taskExecutionId=$2

echo "Preparing for $taskExecutionId"

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
	timeout -s SIGKILL 3s sudo -u $userName ssh -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=3 $userName@$node echo 1 > /dev/null 2>&1 &
	testpids+=($!)
	echo "    created $! for $node"
done

start=$SECONDS
echo "start=$start task=$taskExecutionId"
finished=false
loopCount=0
while ! $finished && [ $((SECONDS-start)) -lt 30 ]
do
	((loopCount++))
	echo "looping $loopCount SECONDS=$SECONDS task=$taskExecutionId"
	finished=true
	for i in ${!testpids[@]}
	do
		echo "    pid is ${testpids[$i]} SECONDS=$SECONDS task=$taskExecutionId"
		wait ${testpids[$i]} && echo "    trusted ${nodes[$i]} SECONDS=$SECONDS" && unset testpids[$i] && unset nodes[$i]
		exitcode=$?
		echo "    exit code is $exitcode SECONDS=$SECONDS task=$taskExecutionId"
		if [ $exitcode != 0 ]; then
			finished=false
			timeout -s SIGKILL 3s sudo -u $userName ssh -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=3 $userName@${nodes[$i]} echo 1 > /dev/null 2>&1 &
			testpids[$i]=$!
			echo "    line: timeout -s SIGKILL 3s ssh -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=3 $userName@${nodes[$i]} echo 1 > /dev/null 2>&1 &"
			echo "    untrust ${nodes[$i]}, exitcode $exitcode, new pid $! task=$taskExecutionId"
		fi
		echo ""
	done

	echo "ending loop $loopCount, finished=$finished, SECONDS=$SECONDS task=$taskExecutionId"
	if ! $finished; then sleep 1; fi
done

echo "ending finished=$finished, SECONDS=$SECONDS, start=$start, elapsed=$((SECONDS-start)), task=$taskExecutionId"

kill -9 $(jobs -p)

if $finished; then
	echo "all trusted task=$taskExecutionId"
	sync
	exit 0;
else
	echo "not all trusted task=$taskExecutionId"
	sync
	exit -1;
fi
