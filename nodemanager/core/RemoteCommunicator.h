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
                RemoteCommunicator(IRemoteExecutor& executor);
                ~RemoteCommunicator();

                void Open();
                void Close();

            protected:
            private:
                void HandlePost(web::http::http_request message);

                template <typename T>
                static bool IsError(pplx::task<T>& t)
                {
                    try { t.wait(); return false; }
                    catch (const web::http::http_exception& httpEx)
                    {
                        Logger::Error("Http exception occurred: {0}", httpEx.what());
                    }
                    catch (const std::exception& ex)
                    {
                        Logger::Error("Exception occurred: {0}", ex.what());
                    }

                    return true;
                }

                std::string GetListeningUri();

                bool StartJobAndTask(const json::value& val);
                bool StartTask(const json::value& val);
                bool EndJob(const json::value& val);
                bool EndTask(const json::value& val);
                bool Ping(const json::value& val);
                bool Metric(const json::value& val);

                static const std::string ApiSpace;
                static const std::string CallbackUriKey;
                const std::string listeningUri;

                bool isListening;

                std::map<std::string, std::function<bool(const json::value&)>> processors;
                std::string callbackUri;


                IRemoteExecutor& executor;

                web::http::experimental::listener::http_listener listener;
        };
    }
}

#endif // REMOTECOMMUNICATOR_H
