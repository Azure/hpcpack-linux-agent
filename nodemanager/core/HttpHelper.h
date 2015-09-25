#ifndef HTTPHELPER_H
#define HTTPHELPER_H

#include <cpprest/http_client.h>
#include <boost/asio/ssl.hpp>

#include "NodeManagerConfig.h"

namespace hpc
{
    namespace core
    {
        using namespace web;
        using namespace boost::asio::ssl;

        class HttpHelper
        {
            public:
                static http::http_request GetHttpRequest(
                    const http::method& mtd)
                {
                    http::http_request msg(mtd);
                    msg.set_request_uri("");
                    msg.headers().add(AuthenticationHeaderKey, NodeManagerConfig::GetClusterAuthenticationKey());
                    return msg;
                }

                template <typename T>
                static http::http_request GetHttpRequest(
                    const http::method& mtd,
                    const T &body)
                {
                    http::http_request msg = GetHttpRequest(mtd);
                    msg.set_body(body);
                    return msg;
                }

                static void ConfigListenerSslContext(context& ctx)
                {
                    ctx.set_options(boost::asio::ssl::context::default_workarounds);

                    auto certChain = NodeManagerConfig::GetCertificateChainFile();

                    if (!certChain.empty())
                    {
                        ctx.use_certificate_chain_file(certChain);
                    }

                    auto privateKey = NodeManagerConfig::GetPrivateKeyFile();

                    if (!privateKey.empty())
                    {
                        ctx.use_private_key_file(privateKey, context::pem);
                    }
                }

                static http::client::http_client GetHttpClient(const std::string& uri)
                {
                    http::client::http_client_config config;

                    config.set_ssl_context_options_callback([](context& ctx)
                    {
                        if (NodeManagerConfig::GetUseDefaultCA())
                        {
                            ctx.set_default_verify_paths();
                        }

                        auto verifyPath = NodeManagerConfig::GetTrustedCAPath();
                        if (!verifyPath.empty())
                        {
                            ctx.add_verify_path(verifyPath);
                        }

                        auto verifyFile = NodeManagerConfig::GetTrustedCAFile();
                        if (!verifyFile.empty())
                        {
                            ctx.load_verify_file(verifyFile);
                        }
                    });

                    utility::seconds timeout(5l);
                    config.set_timeout(timeout);
                    Logger::Debug(
                        "Create client to {0}, configure: timeout {1} seconds, chuck size {2}",
                        uri, config.timeout().count(), config.chunksize());

                    return std::move(http::client::http_client(uri, config));
                }

                template <typename T>
                static bool FindHeader(const T& message, const std::string& headerKey, std::string& header)
                {
                    auto h = message.headers().find(headerKey);
                    if (h != message.headers().end())
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
