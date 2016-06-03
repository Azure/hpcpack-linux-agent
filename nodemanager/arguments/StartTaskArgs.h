#ifndef STARTTASKARGS_H
#define STARTTASKARGS_H

#include <cpprest/json.h>

#include "ProcessStartInfo.h"

namespace hpc
{
    namespace arguments
    {
        struct StartTaskArgs
        {
            public:
                StartTaskArgs(int jobId, int taskId, ProcessStartInfo&& startInfo);

                int JobId;
                int TaskId;
                ProcessStartInfo StartInfo;

                static StartTaskArgs FromJson(const web::json::value& jsonValue);

             protected:
            private:
        };
    }
}

#endif // STARTTASKARGS_H
