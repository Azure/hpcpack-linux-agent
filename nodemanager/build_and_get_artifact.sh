#!/bin/bash
set -e

# Activate Holy Build Box environment.
source /hbb_exe/activate

set -x

yum update -y
yum install perl-IPC-Cmd kernel-devel -y

cd /hpcpack-linux-agent/nodemanager
../vcpkg/bootstrap-vcpkg.sh
cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=../vcpkg/scripts/buildsystems/vcpkg.cmake
cmake --build build -- -j`nproc`