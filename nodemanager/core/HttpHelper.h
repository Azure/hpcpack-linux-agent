#ifndef HTTPHELPER_H
#define HTTPHELPER_H

#include <cpprest/http_client.h>

#include "NodeManagerConfig.h"

namespace hpc
{
    namespace core
    {
        using namespace web;

        class HttpHelper
        {
            public:
                static http::http_request GetHttpRequest(
                    const http::method& mtd,
                    const json::value &body)
                {
                    http::http_request msg(mtd);
                    msg.set_request_uri("");
                    msg.set_body(body);
                    msg.headers().add(AuthenticationHeaderKey, NodeManagerConfig::GetClusterAuthenticationKey());
                    return msg;
                }

                static http::client::http_client GetHttpClient(const std::string& uri)
                {
                    http::client::http_client_config config;
                    utility::seconds timeout(5l);
                    config.set_timeout(timeout);
                    Logger::Debug(
                        "Create client to {0}, configure: timeout {1} seconds, chuck size {2}",
                        uri, config.timeout().count(), config.chunksize());

                    return http::client::http_client(uri, config);
                }

                static bool FindHeader(const http::http_request& request, const std::string& headerKey, std::string& header)
                {
                    auto h = request.headers().find(headerKey);
                    if (h != request.headers().end())
                    {
                        header = h->second;
                        return true;
                    }

                    return false;
                }

                static const std::string AuthenticationHeaderKey;
            protected:
            private:
        };
    }
}

#endif // HTTPHELPER_H
