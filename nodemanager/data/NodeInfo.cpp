#include "NodeInfo.h"

using namespace web;
using namespace hpc::data;

NodeInfo::NodeInfo()
{
    //ctor
}


json::value NodeInfo::GetJson() const
{
    json::value j;
    return std::move(j);
}
