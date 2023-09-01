FROM ubuntu:18.04
RUN apt-get update
RUN apt-get -y install git
RUN apt install -y make
RUN apt install -y cmake
RUN apt install -y build-essential libboost-all-dev libssl-dev
WORKDIR /
RUN git clone https://github.com/Azure/hpcpack-linux-agent.git
RUN git clone https://github.com/EvanCui/spdlog.git
RUN ln -s /spdlog /evanspdlog
RUN git clone https://github.com/microsoft/cpprestsdk.git
WORKDIR /cpprestsdk
RUN git submodule update --init && cmake -DBUILD_SHARED_LIBS=OFF
RUN make install -j8
WORKDIR /hpcpack-linux-agent/nodemanager
RUN git checkout v2 && git pull
RUN awk '{sub(/LIB = .*/,"LIB = -l:libcpprest.a -l:libz.a -l:libboost_system.a -l:libssl.a -l:libcrypto.a -ldl -lpthread"); print}' makefile > tmp && mv tmp makefile
RUN sed -i '14i#include <boost/algorithm/string.hpp>' ./core/Process.h
CMD make