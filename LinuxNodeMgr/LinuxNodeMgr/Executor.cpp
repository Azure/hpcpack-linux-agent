#include "Executor.h"

void Callback(std::string callbackUri, web::json::value callbackBody)
{
    web::http::client::http_client client(U(callbackUri));
    web::http::http_request request;
    request.set_method(http::methods::POST);

    std::string jsonBody = callbackBody.serialize();

    std::cout << "Callback to " << callbackUri << " with " << jsonBody << std::endl;

    request.set_body(jsonBody, U("application/json"));
    client.request(request).get();
}

/// arg: the pointer of the input.
void* RunThread(void* arg)
{
    ComputeClusterTaskInformation* taskInfo = (ComputeClusterTaskInformation*) arg;
    taskInfo->Message = U("");
    taskInfo->ProcessIds = U("");
    ProcessStartInfo* startInfo = taskInfo->StartInfo;
    int pipeForStdOut[2], pipeForStdErr[2];

    if (-1 == pipe(pipeForStdOut))
    {
        taskInfo->Message += U("Error:");
        std::cout << "Error Pipe >>>>>>>>>>>>>>>>>>>>" << errno << std::endl;
        taskInfo->Message += errno;
        taskInfo->Message += U("\n");
    }

    if (-1 == pipe(pipeForStdErr))
    {
        taskInfo->Message += U("Error:");
        std::cout << "Error pipe >>>>>>>>>>>>>>>>>>>" << errno << std::endl;
        taskInfo->Message += errno;
        taskInfo->Message += U("\n");
    }

    // Fork the task as child process.
    std::cout << "before fork" << std::endl;

    pid_t childPid = fork();
    if (childPid == 0)
    {
        // In child process, redirect the stdout and stderr to pipes.
        dup2(pipeForStdOut[1], 1) ;
        close(pipeForStdOut[0]);
        close(pipeForStdOut[1]);
        dup2(pipeForStdErr[1], 2);
        close(pipeForStdErr[0]);
        close(pipeForStdErr[1]);
        execl(startInfo->commandLine.c_str(), startInfo->commandLine.c_str(), NULL);
        exit(0);
    }
    else
    {
        std::cout << "after fork" << childPid << std::endl;
        startInfo->processId = childPid;

        /// Wait the end of child process.
        wait3(&startInfo->exitCode, 0 ,&startInfo->Usage);

        char buf[32] = {0};
        close(pipeForStdOut[1]);
        close(pipeForStdErr[1]);

        ssize_t bytesRead;

        /// Read data from pipe for stdout.
        while((bytesRead = read(pipeForStdOut[0], buf, sizeof(buf)-1)) > 0)
        {
            buf[bytesRead] = 0;
            startInfo->stdOutText += buf;
        }

        close(pipeForStdOut[0]);

        /// Read data from pipe for stderr.
        while((bytesRead = read(pipeForStdErr[0], buf, sizeof(buf)-1)) > 0)
        {
            buf[bytesRead] = 0;
            startInfo->stdErrText += buf;
        }

        close(pipeForStdErr[0]);

        taskInfo->Message += startInfo->stdOutText + startInfo->stdErrText;
        taskInfo->ExitCode = startInfo->exitCode;
        taskInfo->Exited = true;
        taskInfo->NumberOfProcesses = 1;
       // taskInfo->ProcessIds += (int)childPid;
        taskInfo->TaskRequeueCount = 0;
    }

    try
    {
        Callback(taskInfo->CallbackUri, taskInfo->GetEventArgJson());
        delete taskInfo;
        JobTaskDb::GetInstance().RemoveTask(taskInfo->JobId, taskInfo->TaskId);
    }
    catch (const web::http::http_exception& ex)
    {
        std::cout << "Http Exception Occurred" << ex.what() << std::endl;
    }
    catch (...)
    {
        std::cout << "Exception occurred" << std::endl;
    }

    pthread_exit(0);
}

Executor::Executor()
{
    //ctor
}

Executor::~Executor()
{
    //dtor
}

ComputeClusterTaskInformation* Executor::StartTask(int jobId, int taskId, ProcessStartInfo* startInfo, const std::string& callbackUri)
{
    ComputeClusterTaskInformation* taskInfo = new ComputeClusterTaskInformation(jobId, taskId, startInfo, callbackUri);

//    std::cout << "Before set env" << std::endl;
//
//    ///Set Environment Variable
//    for (std::map<std::string, std::string>::iterator it = processStartInfo->environmentVariables.begin(); it != processStartInfo->environmentVariables.end(); it++)
//    {
//        std::string command = it->first + "=" + it->second;
//        putenv((char*)command.c_str());
//    }

//    std::cout << "After set env" << std::endl;

    // Create the thread for task.
    pthread_t *threadId = new pthread_t();
    taskInfo->StartInfo->threadId = threadId;
    pthread_create(threadId, NULL, RunThread, (void*)taskInfo);

    std::cout << "created thread." << std::endl;

    return taskInfo;
}

void Executor::EndTask(ComputeClusterTaskInformation* taskInfo)
{
    kill(taskInfo->StartInfo->processId, SIGQUIT);
}

