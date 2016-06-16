#ifndef ENDJOBARGS_H
#define ENDJOBARGS_H

#include <cpprest/json.h>

namespace hpc
{
    namespace arguments
    {
        struct EndJobArgs
        {
            public:
                EndJobArgs(int jobId);

                int JobId;

                static EndJobArgs FromJson(const web::json::value& jsonValue);

            protected:
            private:
        };
    }
}

#endif // ENDJOBARGS_H
