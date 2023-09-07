## Compiling and Dependencies

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
