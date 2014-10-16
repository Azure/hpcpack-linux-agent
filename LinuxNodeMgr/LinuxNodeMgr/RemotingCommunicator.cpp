#include "RemotingCommunicator.h"
#include <syslog.h>

const std::string RemotingCommunicator::ApiSpace = "/api";

void HandleError(pplx::task<void> t)
{
    try
    {
        t.wait();
    }
    catch (const web::http::http_exception& ex)
    {
        std::cout << "Http Exception Occurred: " << ex.what() << std::endl;
    }
    catch (const std::exception e)
    {
        std::cout << "Exception Occurred: " << e.what() << std::endl;
    }
}

/// handle post.
void RemotingCommunicator::handle_post(http_request message)
{
    json::value result = message.extract_json().get();
    std::string URI = message.relative_uri().to_string();

    std::string nodeName = "", functionName = "", apiName = "";
    int num = 0;
    for (size_t i = 0; i < URI.length(); i++)
    {
        std::string *pName = NULL;
        if (URI[i] == '/') num++;
        if (num == 1) pName  = &apiName;
        else if (num == 2) pName = &nodeName;
        else if (num == 3) pName = &functionName;
        else break;
        *pName += URI[i];
    }

    std::cout << "Request: " << URI << " " << nodeName << " " << functionName << std::endl;

    if (apiName != ApiSpace)
    {
        message.reply(status_codes::NotFound);
        std::cout << "Replied: NotFound" << std::endl;
        return;
    }

    std::string callBackUri = "";
    auto header = message.headers().find("CallBackURI");
    if (header != message.headers().end()) callBackUri = header->second;

    std::cout << "CallbackUri found: " << callBackUri << std::endl;

    if (functionName == "/startjobandtask")
    {
        HandleJson::StartJobAndTask(result, callBackUri);

        message.reply(status_codes::OK, U("")).then([=](pplx::task<void> t) { HandleError(t); });
        std::cout << "Replied OK" << std::endl;
    }
    else if (functionName == "/starttask")
    {
        HandleJson::StartTask(result, callBackUri);

        message.reply(status_codes::Accepted, U("")).then([](pplx::task<void> t) { HandleError(t); });
        std::cout << "Replied Accepted" << std::endl;
    }
    else if (functionName == "/endjob")
    {
        HandleJson::EndJob(result);

        message.reply(status_codes::Accepted, U("")).then([](pplx::task<void> t) { HandleError(t); });
        std::cout << "Replied Accepted" << std::endl;
    }
    else if (functionName == "/endtask")
    {
        HandleJson::EndTask(result);

        message.reply(status_codes::Accepted, U("")).then([](pplx::task<void> t) { HandleError(t); });
        std::cout << "Replied Accepted" << std::endl;
    }
    else if (functionName == "/ping")
    {
        JobTaskDb::GetInstance().SetReportUri(callBackUri);
        HandleJson::Ping(callBackUri);

        message.reply(status_codes::Accepted, U("")).then([](pplx::task<void> t) { HandleError(t); });
        std::cout << "Replied Accepted" << std::endl;
    }
    else
    {
        message.reply(status_codes::NotFound, U("")).then([](pplx::task<void> t) { HandleError(t); });
        std::cout << "Replied NotFound" << std::endl;
    }
}

void RemotingCommunicator::OpenListener()
{
    /// Open the listener.
    listener = new http_listener(U(this->AddressUri));
    listener->support(methods::POST, &handle_post);

    try
    {
        listener->open().wait();
    }
    catch (const std::exception ex)
    {
        syslog(LOG_INFO, "Exception While Open: %s", ex.what());
        throw;
    }
}

void RemotingCommunicator::CloseListener()
{
    ///Close the listener.
    if (listener != NULL)
    {
        listener->close().wait();
        delete listener;
        listener = NULL;
    }
}
