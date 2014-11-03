#ifndef REMOTINGEXECUTOR_H_INCLUDED
#define REMOTINGEXECUTOR_H_INCLUDED

#include<cpprest/json.h>

#include"ProcessStartInfo.h"
#include"UnionFindSet.h"

using namespace web;

namespace HandleJson
{
//public:
    /// Receive the response and start task.
    void StartTask(json::value, std::string);

    /// Force to end the task.
    void EndTask(json::value);

    /// Start Job.
    void StartJobAndTask(json::value, std::string);

    /// EndJob
    void EndJob(json::value);

    /// Ping
    void Ping(std::string);

    template<typename T>
    void HandleError(pplx::task<T>& t);
};

#endif // REMOTINGEXECUTOR_H_INCLUDED
