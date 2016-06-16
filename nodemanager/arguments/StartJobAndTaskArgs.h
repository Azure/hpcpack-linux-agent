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
                StartJobAndTaskArgs(
                    int jobId,
                    int taskId,
                    ProcessStartInfo&& startInfo,
                    std::string&& userName,
                    std::string&& password);

                StartJobAndTaskArgs(StartJobAndTaskArgs&& args) = default;
                web::json::value ToJson() const;

                int JobId;
                int TaskId;
                ProcessStartInfo StartInfo;
                std::string UserName;
                std::string Password;
                std::vector<unsigned char> Certificate;
                std::string PrivateKey;
                std::string PublicKey;

                static StartJobAndTaskArgs FromJson(const web::json::value& jsonValue);

            protected:
            private:
        };
    }
}

#endif // STARTJOBANDTASKARGS_H
