#!/bin/bash

echo "Start to build from image aaronyll/nodemanager_build_1804"
docker run -it aaronyll/nodemanager_build_1804
echo "Start to copy artifacts to ./hpcnodemanager-latest"
docker cp $(docker ps -qa | head -n 1):/hpcpack-linux-agent/nodemanager/bin/release ./hpcnodemanager-latest
tput setaf 2
echo "Done."
