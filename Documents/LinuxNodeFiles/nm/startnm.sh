#!/bin/bash
# Set env vars
PATH=$PATH:/home/azureuser/nm
export PATH

LD_LIBRARY_PATH=$LD_LIBRARY_PATH:/home/azureuser/nm
export LD_LIBRARY_PATH

# Run NM
cd /home/azureuser/nm
LOG=/home/azureuser/nm/NM_`date +%y%m%d%H%M%S`.log
/home/azureuser/nm/NM > $LOG

echo "end of startnm" >> $LOG



