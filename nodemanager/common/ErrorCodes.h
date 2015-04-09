#ifndef ERRORCODES_H
#define ERRORCODES_H

namespace hpc
{
    namespace common
    {
        enum class ErrorCodes
        {
            EndJobExitCode = -101,
            EndTaskExitCode = -102,
            BuildScriptError = -103,
            DefaultExitCode = -999,
            GetHostNameError = -104,
            PopenError = -105,
        };
    }
}

#endif // ERRORCODES_H
