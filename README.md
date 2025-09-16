# Linux Node Agent

This is a .NET version of HPC Pack node agent for Linux, replacing the old C++ version.

## Installation

### Build setup pacakge

A setup package can be built by

```ps1
.\build\Build.ps1
```

The build result is put in `out\linux-x64-Release`, like

```
Mode                 LastWriteTime         Length Name
----                 -------------         ------ ----
-a---           9/15/2025  3:28 PM       41031615 hpcnodeagent.tar.gz
-a---           8/10/2025  4:37 PM          26645 setup.py
```

### Install agent on compute node

Firstly, uninstall the old C++ version. On compute a node, under the directory `/opt/hpcnodemanager`, execute

```bash
python3 setup.py -uninstall -keepcert
```

Then, install the new .NET version. Suppose the setup files are already put in `/mnt/reminst/NewAgent` on a compute node. Then under that directory, execute

```bash
python3 setup.py -install -connectionstring:leiz-hpctest-2 -keepcert
```

Note here the `leiz-hpctest-2` is my head node name. You should replace it with yours.

### Runtime Environment

As an ASP.NET program, the agent respects the environment variable `ASPNETCORE_ENVIRONMENT`. Set it to 'Development' to enable debug logging, as well as other settings for development, and 'Production' or unset for production environment.

## Development

### Remote Testing in Visual Studio

Visual Studio can run test remotely. The configure file is [testEnvironments.json](./src/testEnvironments.json). Here we have options for WSL and container. But there're some prerequisites for them, separtely.

See more at https://learn.microsoft.com/en-us/visualstudio/test/remote-testing?view=vs-2022

#### WSL

1. Install a Linux distribution in WSL. See the supported versions of Linux distros in testEnvironments.json.
2. Install .NET SDK in the distro.
3. Make sure the distro's default user is "root" in file "/etc/wsl.conf". See help at https://learn.microsoft.com/en-us/windows/wsl/wsl-config#user-settings

#### Container

1. Make sure Docker Desktop is installed and started.
2. Optinally, build a local image of [Dockerfile.test](./src/Dockerfile.test) once, like `docker build -t local/netsdk:9.0 -f .\Dockerfile.test .`. Note the current directory for the build command is `src`.

NOTE

When you see error like

```
StreamJsonRpc.RemoteInvocationException: /usr/share/dotnet/dotnet process failed to connect to vstest.console process after 90 seconds.
```

You may need to upgrade the base image of `Dockerfile.test` to a higher version of .NET SDK, with which the [VS Test package](https://www.nuget.org/packages/Microsoft.TestPlatform.CLI#supportedframeworks-body-tab) that is being used to run the tests in the remote environment is built.
