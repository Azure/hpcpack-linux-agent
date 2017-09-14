#ifndef PEEKTASKOUTPUTARGS_H
#define PEEKTASKOUTPUTARGS_H

#include <cpprest/json.h>

namespace hpc
{
    namespace arguments
    {
        struct PeekTaskOutputArgs
        {
            public:
                PeekTaskOutputArgs(int jobId, int taskId);

                int JobId;
                int TaskId;

                static PeekTaskOutputArgs FromJson(const web::json::value& jsonValue);

             protected:
            private:
        };
    }
}

#endif // PEEKTASKOUTPUTARGS_H
