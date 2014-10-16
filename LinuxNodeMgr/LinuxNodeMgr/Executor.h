#ifndef EXECUTOR_H
#define EXECUTOR_H

#include<stdio.h>
#include<string.h>
#include<stdlib.h>
#include<signal.h>
#include<pthread.h>
#include<unistd.h>
#include<malloc.h>
#include<fcntl.h>

#include<cerrno>
#include<iostream>

#include<sys/time.h>
#include<sys/types.h>
#include<sys/wait.h>
#include<sys/resource.h>
#include<sys/socket.h>
#include<cpprest/json.h>
#include<boost/regex.hpp>

#include "JobTaskDb.h"

struct ComputeClusterTaskInformation;

class Executor
{
    public:
        Executor();
        virtual ~Executor();

        ComputeClusterTaskInformation* StartTask(int jobId, int taskId, ProcessStartInfo* startInfo, const std::string& callbackUri);
        void EndTask(ComputeClusterTaskInformation* taskInfo);

    protected:
    private:
};

#endif // EXECUTOR_H
