# Linux Node Agent

## Notes on file EOL

File EOL is critical for a cross-platform project like this, which is developed on Windows with Visual Studio but run on Linux.

Basically, it's required that

* All text files except .sh files (for Bash script) have CRLF as EOL.
* .sh files have LF as EOL.
* No mixed EOL (some lines end with LF, while others end with CRLF) is allowed.

To mandate this, a [.gitattributes file](./.gitattributes) is present, and you're also required to make the following Git settings

* `git config set core.safecrlf true`
* `git config set core.autocrlf false`

You can check your file EOL by executing `git ls-files --eol` under the project root directory. An example result is like

```
i/lf    w/crlf  attr/text=auto eol=crlf .config/tsaoptions.json
i/lf    w/crlf  attr/text=auto eol=crlf .gitattributes
i/lf    w/crlf  attr/text=auto eol=crlf .gitignore
i/lf    w/crlf  attr/text=auto eol=crlf README.md
i/lf    w/crlf  attr/text=auto eol=crlf nuget.config
i/lf    w/crlf  attr/text=auto eol=crlf owners.txt
i/lf    w/crlf  attr/text=auto eol=crlf pipelines/OneBranch.Buddy.CrossPlat.yml
i/lf    w/crlf  attr/text=auto eol=crlf pipelines/OneBranch.Official.CrossPlat.yml
i/lf    w/crlf  attr/text=auto eol=crlf src/NodeAgent.Test/Mocks/MockConfigManager.cs
...
i/lf    w/crlf  attr/text=auto eol=crlf src/NodeAgent/appsettings.json
i/lf    w/crlf  attr/text=auto eol=crlf src/NodeAgent/nodemanager.json
```

Make sure the first column is always `i/lf` for all types of text files. This means all text files are saved with LF as EOF in the Git index tree. But for the Git working tree (in the second column), it depends. It can be `w/crlf` (for all text files except .sh files) or `w/lf` (for .sh files only).

When in doubt of EOL, check it with the command.

## Remote Testing in Visual Studio

Visual Studio can run test remotely. The configure file is [testEnvironments.json](./src/testEnvironments.json). Here we have options for WSL and container. But there're some prerequisites for them, separtely.

See more at https://learn.microsoft.com/en-us/visualstudio/test/remote-testing?view=vs-2022

### WSL

1. Install a Linux distribution in WSL. See the supported versions of Linux distros in testEnvironments.json.
2. Install .NET SDK in the distro.
3. Make sure the distro's default user is "root" in file "/etc/wsl.conf". See help at https://learn.microsoft.com/en-us/windows/wsl/wsl-config#user-settings

### Container

1. Make sure Docker Desktop is installed and started.
2. Build a local image of [Dockerfile.test](./src/Dockerfile.test) once, like `docker build -t local/netsdk:8.0 -f .\Dockerfile.test .`. Note the current directory for the build command is `src`.

