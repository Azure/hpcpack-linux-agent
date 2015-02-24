#ifndef JOBINFO_H
#define JOBINFO_H

#include <cpprest/json.h>
#include <map>

#include "TaskInfo.h"

namespace hpc
{
    namespace data
    {
        struct JobInfo
        {
            public:
                JobInfo(int jobId) : JobId(jobId) { }

                web::json::value ToJson() const;

                int JobId;
                std::map<int, TaskInfo> Tasks;
            protected:
            private:
        };
    }
}

#endif // JOBINFO_H
