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
        [this](auto request) { this->HandlePost(request); });

    this->processors["startjobandtask"] = [this] (const auto& j, const auto& c) { return this->StartJobAndTask(j, c); };
    this->processors["starttask"] = [this] (const auto& j, const auto& c) { return this->StartTask(j, c); };
    this->processors["endjob"] = [this] (const auto& j, const auto& c) { return this->EndJob(j, c); };
    this->processors["endtask"] = [this] (const auto& j, const auto& c) { return this->EndTask(j, c); };
    this->processors["ping"] = [this] (const auto& j, const auto& c) { return this->Ping(j, c); };
    this->processors["metric"] = [this] (const auto& j, const auto& c) { return this->Metric(j, c); };
}

RemoteCommunicator::~RemoteCommunicator()
{
    this->Close();
}

void RemoteCommunicator::Open()
{
    if (!this->isListening)
    {
        this->listener.open().then([this](auto t)
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

    std::vector<std::string> tokens = String::Split(uri, '/');

    // skip the first '/'.
    int p = 1;
    auto apiSpace = tokens[p++];
    auto nodeName = tokens[p++];
    auto methodName = tokens[p++];

    Logger::Info("Request: Uri {0}, Node {1}, Method {2}", uri, nodeName, methodName);

    if (apiSpace != ApiSpace)
    {
        Logger::Error("Not allowed ApiSpace {0}", apiSpace);
        request.reply(status_codes::NotFound, U("Not found"))
            .then([this](auto t) { this->IsError(t); });
        return;
    }

    std::string callbackUri;
    auto callbackHeader = request.headers().find(CallbackUriKey);
    if (callbackHeader != request.headers().end())
    {
        callbackUri = callbackHeader->second;
        Logger::Debug("CallbackUri found {0}", callbackUri.c_str());
    }

    auto processor = this->processors.find(methodName);

    if (processor != this->processors.end())
    {
        request.extract_json().then([processor, callback = std::move(callbackUri)](pplx::task<json::value> t)
        {
            // todo: throw exception instead of using the return value.
            processor->second(t.get(), callback);
        })
        .then([request, this](auto t)
        {
            if (!this->IsError(t))
            {
                request.reply(status_codes::OK, "").then([this](auto t) { this->IsError(t); });
            }
            else
            {
                request.reply(status_codes::InternalError, "").then([this](auto t) { this->IsError(t); });
            }
        });
    }
    else
    {
        Logger::Warn("Unable to find the method {0}", methodName.c_str());
        request.reply(status_codes::NotFound, "").then([this](auto t) { this->IsError(t); });
    }
}

bool RemoteCommunicator::StartJobAndTask(const json::value& val, const std::string& callbackUri)
{
    Logger::Info("Json: {0}", val.serialize());
    auto args = StartJobAndTaskArgs::FromJson(val);
    return this->executor.StartJobAndTask(std::move(args), callbackUri);
}

bool RemoteCommunicator::StartTask(const json::value& val, const std::string& callbackUri)
{
    auto args = StartTaskArgs::FromJson(val);
    return this->executor.StartTask(std::move(args), callbackUri);
}

bool RemoteCommunicator::EndJob(const json::value& val, const std::string& callbackUri)
{
    auto args = EndJobArgs::FromJson(val);
    return this->executor.EndJob(std::move(args));
}

bool RemoteCommunicator::EndTask(const json::value& val, const std::string& callbackUri)
{
    auto args = EndTaskArgs::FromJson(val);
    return this->executor.EndTask(std::move(args));
}

bool RemoteCommunicator::Ping(const json::value& val, const std::string& callbackUri)
{
    return this->executor.Ping(callbackUri);
}

bool RemoteCommunicator::Metric(const json::value& val, const std::string& callbackUri)
{
    return this->executor.Metric(callbackUri);
}

const std::string RemoteCommunicator::ApiSpace = "api";
const std::string RemoteCommunicator::CallbackUriKey = "CallbackURI";

std::string RemoteCommunicator::GetListeningUri()
{
    std::ostringstream oss;
    oss << "http://" << System::GetIpAddress(IpAddressVersion::V4, "eth0") << ":50000";
    return std::move(oss.str());
}

