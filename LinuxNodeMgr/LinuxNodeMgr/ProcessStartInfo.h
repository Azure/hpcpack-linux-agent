#ifndef ProcessStartInfo_H_INCLUDED
#define ProcessStartInfo_H_INCLUDED
#include<string.h>
#include<malloc.h>

#include <iostream>
#include <map>

#include<sys/time.h>
#include<sys/resource.h>
#include<cpprest/json.h>
#include<boost/regex.hpp>
#include<cpprest/http_client.h>

using namespace web;

struct ProcessStartInfo
{
    ProcessStartInfo()
        : threadId(NULL) {}

    static ProcessStartInfo* FromJson(const web::json::value& jsonValue);

    /// Input Parameters
    std::string commandLine;
    std::string stdInText;
    std::string stdOutText;
    std::string stdErrText;
    std::string workingDirectory;
    std::vector<long long> affinity;
    std::map<std::string, std::string> environmentVariables;

    ///Task thread&process information.
    rusage Usage;
    pthread_t* threadId;
    pid_t processId;
    int exitCode;
};

#endif // ProcessStartInfo_H_INCLUDED
