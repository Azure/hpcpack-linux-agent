#include <sstream>
#include <stdlib.h>

#include "RemoteCommunicator.h"
#include "../utils/String.h"
#include "../utils/System.h"
#include "../arguments/StartJobAndTaskArgs.h"
#include "../common/ErrorCodes.h"
#include "NodeManagerConfig.h"
#include "HttpHelper.h"
#include "../filters/FilterException.h"
#include "../arguments/MetricCountersConfig.h"

using namespace web::http;
using namespace web;
using namespace hpc::utils;
using namespace hpc::arguments;
using namespace hpc::core;
using namespace hpc::common;
using namespace web::http::experimental::listener;
using namespace hpc::filters;

RemoteCommunicator::RemoteCommunicator(IRemoteExecutor& exec, const http_listener_config& config) :
    listeningUri(NodeManagerConfig::GetListeningUri()), isListening(false), executor(exec),
    listener(listeningUri, config)
{
    this->listener.support(
        methods::POST,
        [this](auto request) { this->HandlePost(request); });

    if (NodeManagerConfig::GetDebug())
    {
        this->listener.support(
            methods::GET,
            [this](auto request) { this->HandleGet(request); });
    }

    this->processors["startjobandtask"] = [this] (auto&& j, auto&& c) mutable -> pplx::task<json::value> { return this->StartJobAndTask(std::move(j), std::move(c)); };
    this->processors["starttask"] = [this] (auto&& j, auto&& c) mutable -> pplx::task<json::value> { return this->StartTask(std::move(j), std::move(c)); };
    this->processors["endjob"] = [this] (auto&& j, auto&& c) mutable -> pplx::task<json::value> { return this->EndJob(std::move(j), std::move(c)); };
    this->processors["endtask"] = [this] (auto&& j, auto&& c) { return this->EndTask(std::move(j), std::move(c)); };
    this->processors["ping"] = [this] (auto&& j, auto&& c) { return this->Ping(std::move(j), std::move(c)); };
    this->processors["metric"] = [this] (auto&& j, auto&& c) { return this->Metric(std::move(j), std::move(c)); };
    this->processors["metricconfig"] = [this] (auto&& j, auto&& c) { return this->MetricConfig(std::move(j), std::move(c)); };
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
                this->listeningUri,
                this->isListening ? "opened." : "failed.");

            if (!this->isListening)
            {
                exit((int)ErrorCodes::FailedToOpenPort);
            }
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

void RemoteCommunicator::HandleGet(http_request request)
{
    auto uri = request.relative_uri().to_string();
    Logger::Info("Request (GET): Uri {0}", uri);

    json::value body;
    body["status"] = json::value::string("node manager working");
    request.reply(status_codes::OK, body).then([this](auto t) { this->IsError(t); });
}

void RemoteCommunicator::HandlePost(http_request request)
{
    auto uri = request.relative_uri().to_string();
    Logger::Info("Request: Uri {0}", uri);

    std::vector<std::string> tokens = String::Split(uri, '/');

    if (tokens.size() < 4)
    {
        Logger::Warn("Not supported uri {0}", uri);
        return;
    }

    // skip the first '/'.
    int p = 1;
    auto apiSpace = tokens[p++];
    auto nodeName = tokens[p++];
    auto methodName = tokens[p++];

    Logger::Debug("Request: Uri {0}, Node {1}, Method {2}", uri, nodeName, methodName);

    if (apiSpace != ApiSpace)
    {
        Logger::Error("Not allowed ApiSpace {0}", apiSpace);
        request.reply(status_codes::NotFound, U("Not found"))
            .then([this](auto t) { this->IsError(t); });
        return;
    }

    std::string authenticationKey;
    if (HttpHelper::FindHeader(request, HttpHelper::AuthenticationHeaderKey, authenticationKey))
    {
        Logger::Debug("AuthenticationKey found");
    }

    if (NodeManagerConfig::GetClusterAuthenticationKey() != authenticationKey)
    {
        Logger::Warn("Authentication key validation failed.");
        request.reply(status_codes::Unauthorized, "").then([this](auto t) { this->IsError(t); });
        return;
    }

    std::string callbackUri;
    if (HttpHelper::FindHeader(request, CallbackUriKey, callbackUri))
    {
        Logger::Debug("CallbackUri found {0}", callbackUri.c_str());
    }

    auto processor = this->processors.find(methodName);

    if (processor != this->processors.end())
    {
        request.extract_json().then(
        [processor, callback = std::move(callbackUri)] (pplx::task<json::value> t)
        {
            auto j = t.get();
         //   Logger::Debug("Json: {0}", j.serialize());
            auto uri = callback;

            return processor->second(std::move(j), std::move(uri));
        })
        .then([request] (pplx::task<json::value> t)
        {
            try
            {
                request.reply(status_codes::OK, t.get()).then([](auto t) { IsError(t); });
            }
            catch (const web::http::http_exception& httpEx)
            {
                const std::string errorMessage = httpEx.what();
                Logger::Error("Http exception occurred: {0}", errorMessage);
                request.reply(status_codes::InternalError, errorMessage).then([](auto t) { IsError(t); });
            }
            catch (const FilterException& filterEx)
            {
                const std::string errorMessage = filterEx.what();
                Logger::Error("Filter exception occurred: {0}", errorMessage);
                request.reply(status_codes::InternalError + 50, errorMessage).then([](auto t) { IsError(t); });
            }
            catch (const std::exception& ex)
            {
                const std::string errorMessage = ex.what();
                Logger::Error("Exception occurred: {0}", errorMessage);
                request.reply(status_codes::InternalError, errorMessage).then([](auto t) { IsError(t); });
            }
        });
    }
    else
    {
        Logger::Warn("Unable to find the method {0}", methodName.c_str());
        request.reply(status_codes::NotFound, "Cannot find the method").then([this](auto t) { this->IsError(t); });
    }
}

pplx::task<json::value> RemoteCommunicator::StartJobAndTask(json::value&& val, std::string&& callbackUri)
{
    auto args = StartJobAndTaskArgs::FromJson(val);

    return this->filter.OnJobStart(args.JobId, args.TaskId, args.StartInfo.TaskRequeueCount, val).then(
    [this, callback = std::move(callbackUri)](pplx::task<json::value> t)
    {
        auto filteredJson = t.get();
        auto uri = callback;
        return this->executor.StartJobAndTask(StartJobAndTaskArgs::FromJson(filteredJson), std::move(uri));
    });
}

pplx::task<json::value> RemoteCommunicator::StartTask(json::value&& val, std::string&& callbackUri)
{
    auto args = StartTaskArgs::FromJson(val);

    return this->filter.OnTaskStart(args.JobId, args.TaskId, args.StartInfo.TaskRequeueCount, val).then(
    [this, callback = std::move(callbackUri)](pplx::task<json::value> t)
    {
        auto filteredJson = t.get();
        auto uri = callback;
        return this->executor.StartTask(StartTaskArgs::FromJson(filteredJson), std::move(uri));
    });
}

pplx::task<json::value> RemoteCommunicator::EndJob(json::value&& val, std::string&& callbackUri)
{
    auto args = EndJobArgs::FromJson(val);
    this->filter.OnJobEnd(args.JobId, val).then([this](pplx::task<json::value> t) { this->IsError(t); });
    return this->executor.EndJob(std::move(args));
}

pplx::task<json::value> RemoteCommunicator::EndTask(json::value&& val, std::string&& callbackUri)
{
    auto args = EndTaskArgs::FromJson(val);
    return this->executor.EndTask(std::move(args), std::move(callbackUri));
}

pplx::task<json::value> RemoteCommunicator::Ping(json::value&& val, std::string&& callbackUri)
{
    return this->executor.Ping(std::move(callbackUri));
}

pplx::task<json::value> RemoteCommunicator::Metric(json::value&& val, std::string&& callbackUri)
{
    return this->executor.Metric(std::move(callbackUri));
}

pplx::task<json::value> RemoteCommunicator::MetricConfig(json::value&& val, std::string&& callbackUri)
{
    auto args = MetricCountersConfig::FromJson(val);
    return this->executor.MetricConfig(std::move(args), std::move(callbackUri));
}

const std::string RemoteCommunicator::ApiSpace = "api";
const std::string RemoteCommunicator::CallbackUriKey = "CallbackURI";

