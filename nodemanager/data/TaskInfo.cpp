#include "TaskInfo.h"
#include "../utils/JsonHelper.h"
#include "../utils/String.h"

using namespace web;
using namespace hpc::data;
using namespace hpc::utils;

json::value TaskInfo::ToJson() const
{
    json::value j;

    j["TaskId"] = this->TaskId;
    j["TaskRequeueCount"] = this->taskRequeueCount;
    j["ExitCode"] = this->ExitCode;
    j["Exited"] = this->Exited;
    j["KernelProcessorTime"] = (int64_t)this->KernelProcessorTime;
    j["UserProcessorTime"] = (int64_t)this->UserProcessorTime;
    j["WorkingSet"] = this->WorkingSet;
    j["NumberOfProcesses"] = this->NumberOfProcesses;
    j["PrimaryTask"] = this->IsPrimaryTask;
    j["Message"] = JsonHelper<std::string>::ToJson(this->Message);
    j["ProcessIds"] = JsonHelper<std::string>::ToJson(String::Join<','>(this->ProcessIds));

    return j;
}

json::value TaskInfo::ToCompletionEventArgJson() const
{
    json::value j = this->ToJson();

    json::value jobIdArg;
    jobIdArg["JobId"] = this->JobId;
    jobIdArg["TaskInfo"] = j;
    jobIdArg["NodeName"] = json::value::string(this->NodeName);

    return jobIdArg;
}
