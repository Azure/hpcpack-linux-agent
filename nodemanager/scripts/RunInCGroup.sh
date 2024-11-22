#!/bin/bash

cgroupPath=$1
commandToRun=$2

echo $$ > $cgroupPath

/bin/bash $commandToRun