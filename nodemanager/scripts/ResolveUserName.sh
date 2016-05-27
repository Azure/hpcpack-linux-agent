#!/bin/bash

## Introduction:
## This script is used to compose right runas user for HPC nodemanager to run
## job with sudo -E -u <composedUserName> <commands>
##
## By default, HPC will strip domainName and only use userName passed from
## HPC Windows HeadNode as runas user, this is intend to support Linux 
## environment without domain joined.
##
## When it's in an Active Directly integrated Linux environment, 
## it's necessary to compose right runas user with different settings, 
## such as: 'winbind seperator' set in /etc/samba/smb.conf for Winbind
## or 're_expression' set in /etc/sssd/sssd.conf for SSSD.
## to ensure right user is used when HPC run jobs.
##
## In this case, we need compose runas user, for example:
## composedUserName="$domainName.$userName" when Winbind Seperator set to . delimiter
## or In SSSD, when set re_expression = ((?P<domain>.+)\.(?P<name>[^\\\.@]+$))
## and echo back the composedUserName.

## Parameters:
## The 1st parameter passed into this script is the domainName used to submit
## job through HPC. 
## The 2nd parameter passed into this script is the userName used to submit
## job through HPC.


[ -z "$1" ] && echo "domainName not specified" && exit 202
[ -z "$2" ] && echo "userName not specified" && exit 202

domainName="$1"
userName="$2"

## Examples:
#composedUserName="$domainName.$userName", when . set as delimiter
#composedUserName="$domainName\$userName", when \ set as delimiter
composedUserName="$userName"

## To verify if composedUserName exists, comment out these lines if 
## performance is the first priority or it's in non AD integrated environment. 
## Be sure you do compose in the right way.
maxRetry=3
while [ $maxRetry -gt 0 ]
do
	getent passwd $composedUserName > /dev/null 
	ec=$?
	if [ $ec -eq 0 ]
	then
		break
	fi
	echo "Find no user $composedUserName, error code $ec, retry after .5 seconds"
	((maxRetry--))
	sleep .5
done

if [ $ec -ne 0 ]
then
	exit $ec
fi
#End of verify, which could be comment out for performance consideration.

#Echo back the composedUserName
echo "$composedUserName"

exit 0

