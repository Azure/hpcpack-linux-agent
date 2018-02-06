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
                static std::shared_ptr<http::http_request> GetHttpRequest(
                    const http::method& mtd)
                {
                    auto msg = std::make_shared<http::http_request>(mtd);
                    msg->set_request_uri("");
                    msg->headers().add(AuthenticationHeaderKey, NodeManagerConfig::GetClusterAuthenticationKey());
                    return msg;
                }

                template <typename T>
                static std::shared_ptr<http::http_request> GetHttpRequest(
                    const http::method& mtd,
                    const T &body)
                {
                    auto msg = GetHttpRequest(mtd);
                    msg->set_body(body);
                    return msg;
                }

                template <typename T>
                static std::shared_ptr<http::http_request> GetHttpRequest(
                    const http::method& mtd,
                    const T &body,
                    const std::string& callback)
                {
                    auto msg = GetHttpRequest(mtd, body);
                    msg->headers().add(CallbackUriKey, callback);
                    return msg;
                }

                static void ConfigListenerSslContext(context& ctx)
                {
                    ctx.set_options(boost::asio::ssl::context::default_workarounds);

                    auto certChain = NodeManagerConfig::GetCertificateChainFile();

                    if (!certChain.empty())
                    {
                        ctx.use_certificate_chain_file(certChain);
                        Logger::Debug("Use the certificate chain file {0}", certChain);
                    }

                    auto privateKey = NodeManagerConfig::GetPrivateKeyFile();

                    if (!privateKey.empty())
                    {
                        ctx.use_private_key_file(privateKey, context::pem);
                        Logger::Debug("Use the private key file {0}", privateKey);
                    }
                }

                static std::shared_ptr<http::client::http_client> GetHttpClient(const std::string& uri)
                {
                    http::client::http_client_config config;

                    config.set_validate_certificates(false);
                    config.set_ssl_context_callback([](context& ctx)
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

                    return std::make_shared<http::client::http_client>(uri, config);
                }

                static bool FindCallbackUri(http::http_request& request, std::string& uri)
                {
                    return FindHeader(request, CallbackUriKey, uri);
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

                static const std::string CallbackUriKey;

                static const std::string AuthenticationHeaderKey;
            protected:
            private:
        };
    }
}

#endif // HTTPHELPER_H
