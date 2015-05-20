#ifndef ERRORCODES_H
#define ERRORCODES_H

namespace hpc
{
    namespace common
    {
        enum class ErrorCodes
        {
            DefaultExitCode = 254,
            EndJobExitCode = 170,
            EndTaskExitCode = 171,
            BuildScriptError = 172,
            GetHostNameError = 173,
            PopenError = 174,
            SetUserPermission = 175,
            TestRunFailed = 176,
            FailedToOpenPort = 177,
        };
    }
}

#endif // ERRORCODES_H
