#include "EndJobArgs.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

EndJobArgs::EndJobArgs(int jobId) : JobId(jobId)
{
    //ctor
}

EndJobArgs EndJobArgs::FromJson(const json::value& j)
{
    EndJobArgs args(JsonHelper<int>::Read("JobId", j));

    return std::move(args);
}
