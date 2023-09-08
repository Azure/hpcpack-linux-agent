cmake_minimum_required(VERSION 3.15)

project(nodemanager CXX)

set(VCPKG_LIBRARY_LINKAGE static)
set(VCPKG_TARGET_TRIPLET x64-linux)

find_package(fmt REQUIRED)
find_package(spdlog REQUIRED)
find_package(Boost REQUIRED)
find_package(cpprestsdk REQUIRED)

set(CMAKE_CXX_STANDARD 14)

file(GLOB SOURCES "arguments/*.cpp" "arguements/*.h" "common/*.h" "core/*.cpp" "core/*.h" "data/*.cpp" "data/*.h" "filters/*.cpp" "filters/*.h" "utils/*.cpp" "utils/*.h" "main.cpp" "Version.cpp" "Version.h")

add_executable(nodemanager ${SOURCES})

target_link_libraries(nodemanager PRIVATE fmt::fmt spdlog::spdlog Boost::boost cpprestsdk::cpprest -static-libstdc++)

# pack scripts and compiled executable into hpcnodeagent.tar.gz
add_custom_target(
    hpcnodeagent.tar.gz ALL
    COMMAND mkdir -p ${CMAKE_BINARY_DIR}/hpcnodeagent
    COMMAND mkdir -p ${CMAKE_BINARY_DIR}/hpcnodeagent/lib
    COMMAND mkdir -p ${CMAKE_BINARY_DIR}/hpcnodeagent/Utils
    COMMAND cp -r ${CMAKE_SOURCE_DIR}/scripts/* ${CMAKE_BINARY_DIR}/hpcnodeagent
    COMMAND cp ${CMAKE_BINARY_DIR}/nodemanager ${CMAKE_BINARY_DIR}/hpcnodeagent
    COMMAND cp ${CMAKE_SOURCE_DIR}/config/nodemanager.json ${CMAKE_BINARY_DIR}/hpcnodeagent/nodemanager.json.sample
    COMMAND tar -czf ${CMAKE_BINARY_DIR}/hpcnodeagent.tar.gz -C ${CMAKE_BINARY_DIR}/hpcnodeagent .
    WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
    DEPENDS nodemanager
)

