#ifndef ENDTASKARGS_H
#define ENDTASKARGS_H

#include <cpprest/json.h>

namespace hpc
{
    namespace arguments
    {
        struct EndTaskArgs
        {
            public:
                EndTaskArgs(int jobId, int taskId);

                int JobId;
                int TaskId;

                static EndTaskArgs FromJson(const web::json::value& jsonValue);

             protected:
            private:
        };
    }
}

#endif // ENDTASKARGS_H
