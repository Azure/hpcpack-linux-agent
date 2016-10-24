#ifndef REMOTECOMMUNICATOR_H
#define REMOTECOMMUNICATOR_H

#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "../utils/Logger.h"
#include "../filters/ExecutionFilter.h"
#include "IRemoteExecutor.h"

namespace hpc
{
    using namespace utils;
    using namespace web;
    using namespace filters;

    namespace core
    {
        class RemoteCommunicator
        {
            public:
                RemoteCommunicator(IRemoteExecutor& executor, const web::http::experimental::listener::http_listener_config& config, const std::string& uri);
                ~RemoteCommunicator();

                void Open();
                void Close();

            protected:
            private:
                void HandlePost(web::http::http_request message);
                void HandleGet(web::http::http_request message);

                template <typename T>
                static bool IsError(pplx::task<T>& t, std::string& errorMessage)
                {
                    try { t.wait(); return false; }
                    catch (const web::http::http_exception& httpEx)
                    {
                        Logger::Error("Http exception occurred: {0}", httpEx.what());
                        errorMessage = httpEx.what();
                    }
                    catch (const std::exception& ex)
                    {
                        Logger::Error("Exception occurred: {0}", ex.what());
                        errorMessage = ex.what();
                    }

                    return true;
                }

                template <typename T>
                static bool IsError(pplx::task<T>& t)
                {
                    std::string errorMessage;
                    return IsError(t, errorMessage);
                }

                pplx::task<json::value> StartJobAndTask(json::value&& val, std::string&&);
                pplx::task<json::value> StartTask(json::value&& val, std::string&&);
                pplx::task<json::value> EndJob(json::value&& val, std::string&&);
                pplx::task<json::value> EndTask(json::value&& val, std::string&&);
                pplx::task<json::value> Ping(json::value&& val, std::string&&);
                pplx::task<json::value> Metric(json::value&& val, std::string&&);
                pplx::task<json::value> MetricConfig(json::value&& val, std::string&&);

                static const std::string ApiSpace;
                static const std::string CallbackUriKey;
                const std::string listeningUri;

                bool isListening;

                std::map<std::string, std::function<pplx::task<json::value>(json::value&&, std::string&&)>> processors;

                IRemoteExecutor& executor;

                web::http::experimental::listener::http_listener listener;
                ExecutionFilter filter;
        };
    }
}

#endif // REMOTECOMMUNICATOR_H
