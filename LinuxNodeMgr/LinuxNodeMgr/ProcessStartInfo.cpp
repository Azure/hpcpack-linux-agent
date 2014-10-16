#include "ProcessStartInfo.h"

ProcessStartInfo* ProcessStartInfo::FromJson(const web::json::value& jsonInfo)
{
    ProcessStartInfo *processStartInfo = new ProcessStartInfo();
    processStartInfo->commandLine = jsonInfo.at(U("commandLine")).as_string();

    return processStartInfo;
}
