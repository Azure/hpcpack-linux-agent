#include <sstream>

#include "RemoteCommunicator.h"
#include "../utils/String.h"
#include "../utils/System.h"
#include "../arguments/StartJobAndTaskArgs.h"

using namespace web::http;
using namespace hpc::utils;
using namespace hpc::arguments;
using namespace hpc::core;

RemoteCommunicator::RemoteCommunicator(IRemoteExecutor& exec) :
    listeningUri(this->GetListeningUri()), isListening(false), executor(exec), listener(listeningUri)
{
    this->listener.support(
        methods::POST,
        [this](http_request request) { this->HandlePost(request); });

    this->processors["startjobandtask"] = std::bind(&RemoteCommunicator::StartJobAndTask, this, std::placeholders::_1);
    this->processors["starttask"] = std::bind(&RemoteCommunicator::StartTask, this, std::placeholders::_1);
    this->processors["endjob"] = std::bind(&RemoteCommunicator::EndJob, this, std::placeholders::_1);
    this->processors["endtask"] = std::bind(&RemoteCommunicator::EndTask, this, std::placeholders::_1);
    this->processors["ping"] = std::bind(&RemoteCommunicator::Ping, this, std::placeholders::_1);
    this->processors["metric"] = std::bind(&RemoteCommunicator::Metric, this, std::placeholders::_1);
}

RemoteCommunicator::~RemoteCommunicator()
{
    this->Close();
}

void RemoteCommunicator::Open()
{
    if (!this->isListening)
    {
        this->listener.open().then([this](pplx::task<void> t)
        {
            this->isListening = !IsError(t);
            Logger::Info(
                "Opening at {0}, result {1}",
                this->listener.uri().to_string(),
                this->isListening ? "opened." : "failed.");
        });
    }
}

void RemoteCommunicator::Close()
{
    if (this->isListening)
    {
        try
        {
            this->listener.close().wait();
            this->isListening = false;
        }
        catch (const std::exception& ex)
        {
            Logger::Error("Exception happened while close the listener {0}, {1}", this->listener.uri().to_string().c_str(), ex.what());
        }
    }
}

void RemoteCommunicator::HandlePost(http_request request)
{
    auto uri = request.relative_uri().to_string();
    Logger::Info("Request: {0}", uri);

    std::vector<std::string> tokens;
    String::Split(uri, '/', tokens);

    // skip the first '/'.
    int p = 1;
    auto apiSpace = tokens[p++];
    auto nodeName = tokens[p++];
    auto methodName = tokens[p++];
    Logger::Info("Request:");

    Logger::Info("Request: Uri {0}, Node {1}, Method {2}", uri, nodeName, methodName);

    if (apiSpace != ApiSpace)
    {
        Logger::Error("Not allowed ApiSpace {0}", apiSpace);
        request.reply(status_codes::NotFound, U("Not found"))
            .then([this](pplx::task<void> t) { IsError(t); });
        return;
    }

    auto callbackHeader = request.headers().find(CallbackUriKey);
    if (callbackHeader != request.headers().end())
    {
        this->callbackUri = callbackHeader->second;
        Logger::Info("CallbackUri found {0}", this->callbackUri.c_str());
    }

    auto processor = this->processors.find(methodName);

    if (processor != this->processors.end())
    {
        request.extract_json().then([processor, request](pplx::task<json::value> t)
        {
            processor->second(t.get());
        })
        .then([request](pplx::task<void> t)
        {
            if (!IsError(t))
            {
                request.reply(status_codes::OK, "").then([](pplx::task<void> t) { IsError(t); });
            }
            else
            {
                request.reply(status_codes::InternalError, "").then([](pplx::task<void> t) { IsError(t); });
            }
        });
    }
    else
    {
        Logger::Warn("Unable to find the method {0}", methodName.c_str());
        request.reply(status_codes::NotFound, "").then([](pplx::task<void> t) { IsError(t); });
    }
}

bool RemoteCommunicator::StartJobAndTask(const json::value& val)
{
    auto args = StartJobAndTaskArgs::FromJson(val);
    return this->executor.StartJobAndTask(args);
}

bool RemoteCommunicator::StartTask(const json::value& val)
{
    auto args = StartTaskArgs::FromJson(val);
    return this->executor.StartTask(args);
}

bool RemoteCommunicator::EndJob(const json::value& val)
{
    auto args = EndJobArgs::FromJson(val);
    return this->executor.EndJob(args);
}

bool RemoteCommunicator::EndTask(const json::value& val)
{
    auto args = EndTaskArgs::FromJson(val);
    return this->executor.EndTask(args);
}

bool RemoteCommunicator::Ping(const json::value& val)
{
    return this->executor.Ping(this->callbackUri);
}

bool RemoteCommunicator::Metric(const json::value& val)
{
    return this->executor.Metric(this->callbackUri);
}

const std::string RemoteCommunicator::ApiSpace = "api";
const std::string RemoteCommunicator::CallbackUriKey = "CallbackURI";

std::string RemoteCommunicator::GetListeningUri()
{
    std::ostringstream oss;
    oss << "http://" << System::GetIpAddress(IpAddressVersion::V4, "eth0") << ":50000";
    std::string uri = oss.str();
    return uri;
}

