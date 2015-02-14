#ifndef STARTJOBANDTASKARGS_H
#define STARTJOBANDTASKARGS_H

#include <cpprest/json.h>

#include "ProcessStartInfo.h"

namespace hpc
{
    namespace arguments
    {
        struct StartJobAndTaskArgs
        {
            public:
                StartJobAndTaskArgs(int jobId, int taskId, ProcessStartInfo&& startInfo);

                int JobId;
                int TaskId;
                ProcessStartInfo StartInfo;

                static StartJobAndTaskArgs&& FromJson(const web::json::value& jsonValue);

             protected:
            private:
        };
    }
}

#endif // STARTJOBANDTASKARGS_H
