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