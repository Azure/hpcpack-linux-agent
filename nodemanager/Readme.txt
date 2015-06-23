== Conding Convention ==

Namespaces should be rooted from "hpc", and have at most 2 layers, which means, you can only
define one more layer under "hpc".

Files which contain contents under a sub namespace, should be put into a sub folder with the
same name as the sub namespace.

One file should contain only one class.

Private fields, variables should be named using camel convention, while class/struct/methods
should be named using Pascal convention.

File names should be in Pascal convention exception main.cpp, while folder names should be
in lower case.
