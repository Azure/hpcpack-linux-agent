#include "RemotingExecutor.h"
#include "JobTaskDb.h"

void HandleJson::StartTask(json::value jsonObj, std::string callBackUri)
{
    std::cout << "StartTask ... " << std::endl;

    // Get value from Json Object.
    auto arg = jsonObj.at(U("m_Item1"));
    auto startInfoJson = jsonObj.at(U("m_Item2"));
    int jobId = arg.at(U("JobId")).as_integer();
    int taskId = arg.at(U("TaskId")).as_integer();
    ProcessStartInfo *startInfo = ProcessStartInfo::FromJson(startInfoJson);

    std::cout << "start the task: " << startInfo->commandLine << std::endl;

    JobTaskDb::GetInstance().StartJobAndTask(jobId, taskId, startInfo, callBackUri);
}

void HandleJson::StartJobAndTask(json::value jsonObj, std::string callBackUri)
{
    std::cout << "StartJobAndTask ..." << std::endl;

    StartTask(jsonObj, callBackUri);
}

void HandleJson::EndJob(json::value jsonObj)
{
    int jobId = jsonObj.at(U("JobId")).as_integer();

    JobTaskDb::GetInstance().EndJob(jobId);
}

void HandleJson::EndTask(json::value jsonObj)
{
    /// Get the value for Json Object.
    int jobId = jsonObj.at(U("JobId")).as_integer();
    int taskId = jsonObj.at(U("TaskId")).as_integer();

    JobTaskDb::GetInstance().EndTask(jobId, taskId);
}

void HandleJson::Ping(std::string callBackUri)
{
    web::http::client::http_client client(U(callBackUri));
    web::http::http_request request;
    request.set_method(http::methods::POST);
    std::string body = JobTaskDb::GetInstance().GetNodeInfo().serialize();
    std::cout << "Reported to " << callBackUri << std::endl;
    std::cout << "Body: " << body << std::endl;

    request.set_body(body, U("application/json"));
    client.request(request).then([](pplx::task<web::http::http_response> t)
    {
        HandleError(t);
    });
}

template<typename T>
void HandleJson::HandleError(pplx::task<T>& t)
{
    try
    {
        t.wait();
    }
    catch (const web::http::http_exception& ex)
    {
        std::cout << "Http Exception Occurred: " << ex.what() << std::endl;
    }
    catch (const std::exception& e)
    {
        std::cout << "Exception Occurred" << e.what() << std::endl;
    }
}

