#ifndef REMOTECOMMUNICATOR_H
#define REMOTECOMMUNICATOR_H

#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "../utils/Logger.h"
#include "IRemoteExecutor.h"

namespace hpc
{
    using namespace utils;
    using namespace web;

    namespace core
    {
        class RemoteCommunicator
        {
            public:
                RemoteCommunicator(const std::string& networkName, IRemoteExecutor& executor);
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

                std::string GetListeningUri(const std::string& networkName);

                json::value StartJobAndTask(const json::value& val, const std::string&);
                json::value StartTask(const json::value& val, const std::string&);
                json::value EndJob(const json::value& val, const std::string&);
                json::value EndTask(const json::value& val, const std::string&);
                json::value Ping(const json::value& val, const std::string&);
                json::value Metric(const json::value& val, const std::string&);
                json::value MetricConfig(const json::value& val, const std::string&);

                static const std::string ApiSpace;
                static const std::string CallbackUriKey;
                const std::string listeningUri;

                bool isListening;

                std::map<std::string, std::function<json::value(const json::value&, const std::string&)>> processors;

                IRemoteExecutor& executor;

                web::http::experimental::listener::http_listener listener;
        };
    }
}

#endif // REMOTECOMMUNICATOR_H
