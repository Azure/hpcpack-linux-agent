## Compiling and Dependencies

### Compile from source code
The code is compiled on Ubuntu(16.04) with the following dependencies:

* [Boost](https://www.boost.org/) - can be installed by package libboost-all-dev(1.58)
* [C++ REST SDK](https://github.com/Microsoft/cpprestsdk) - can be installed by package libcpprest-dev(2.8.0-2)
* [spdlog](https://github.com/gabime/spdlog) - only header files in the `include` dir. Just clone the branch `v1.x` from GitHub. Note that the package libspdlog-dev(1.6-1) is too old to use.

Depending on where the spdlog headers are put, you may need to modify makefile to modify path for spdlog headers.

### Compile from docker image
We offer an image to help build artifacts. You can build and get artifacts by running the script `./build_and_get_artifact.sh`.

## Conding Convention

Namespaces should be rooted from "hpc", and have at most 2 layers, which means, you can only define one more layer under "hpc".

Files which contain contents under a sub namespace, should be put into a sub folder with the same name as the sub namespace.

One file should contain only one class.

Private fields, variables should be named using camel convention, while class/struct/methods should be named using Pascal convention.

File names should be in Pascal convention exception main.cpp, while folder names should be in lower case.

## Namespace Description

* arguments: All data structures passed from head directly.
* common: Anything which doesn't depend on anything outside of common, and possibly be used by anything outside of common.
* core: Core logic of node manager.
* data: Core data structures used internally.
* scripts: All shell scripts.
* test: Unit test code.
* utils: Utilities, which could be of general purpose, shouldn't couple with node manager logic and concepts, and could be used by other projects.
