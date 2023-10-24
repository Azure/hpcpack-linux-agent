#!/bin/bash
set -e

# Activate Holy Build Box environment.
source /hbb_exe/activate

set -x

rpm --import "http://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
yum-config-manager --add-repo http://download.mono-project.com/repo/centos/
yum update -y
yum install perl-IPC-Cmd kernel-devel mono-complete -y
curl -o /usr/local/bin/nuget.exe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe

cd /hpcpack-linux-agent/nodemanager
../vcpkg/bootstrap-vcpkg.sh
cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=../vcpkg/scripts/buildsystems/vcpkg.cmake
cmake --build build -- -j`nproc`

# publish nuget package if PUBLISH_PACKAGE_FEED is defined
if [ ! -z "$PUBLISH_PACKAGE_FEED" ]; then
    cd build
    mono /usr/local/bin/nuget.exe pack ../HPCPackLinuxAgent.nuspec -Version $PACKAGE_VERSION
    # generate empty nuget.config
    echo -e "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <packageSources>\n    <clear />\n  </packageSources>\n</configuration>" > nuget.config
    mono /usr/local/bin/nuget.exe sources Add -Name "MySource" -Source $PUBLISH_PACKAGE_FEED -UserName $PUBLISH_PACKAGE_USERNAME -Password $PUBLISH_PACKAGE_PASSWORD
    mono /usr/local/bin/nuget.exe push *.nupkg -Source MySource -ApiKey Az
fi
