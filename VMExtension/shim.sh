#!/usr/bin/env bash

# This is the main driver file for HPC Pack Linux NodeAgent extension. This file first checks if Python 3 or 2 is available on the VM 
# and if yes then uses that Python (if both are available then, default is set to python3) to run extension operations in hpcnodemanager.py
# Control arguments passed to the shim are redirected to hpcnodemanager.py without validation.

COMMAND="./hpcnodemanager.py"
PYTHON=""
ARG="$@"

function find_python() {
    local python_exec_command=$1

    if command -v python3 >/dev/null 2>&1 ; then
        eval ${python_exec_command}="python3"
    elif command -v python2 >/dev/null 2>&1 ; then
        eval ${python_exec_command}="python2"
    elif command -v /usr/libexec/platform-python >/dev/null 2>&1 ; then
        # If a user-installed python isn't available, check for a platform-python. This is typically only used in RHEL 8.0.
        echo "User-installed python not found. Using /usr/libexec/platform-python as the python interpreter."
        eval ${python_exec_command}="/usr/libexec/platform-python"
    fi
}

find_python PYTHON

if [ -z "$PYTHON" ] # If python is not installed, we will fail the install with the following error, requiring cx to have python pre-installed
then
    echo "No Python interpreter found. Please install Python 3, or Python 2 if the former is unavailable." >&2
    exit 52 # Missing Dependency
else
    ${PYTHON} --version 2>&1
fi

${PYTHON} ${COMMAND} ${ARG}
exit $?