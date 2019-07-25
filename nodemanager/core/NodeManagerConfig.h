#ifndef NODEMANAGERCONFIG_H
#define NODEMANAGERCONFIG_H

#include "../utils/Configuration.h"
#include "NamingClient.h"

using namespace hpc::utils;

namespace hpc
{
    namespace core
    {
#define AddConfigurationItem(T, name) \
                static T Get##name() \
                { \
                    return instance.ReadValue<T>(#name); \
                } \
                \
                static void Save##name(const T& v) \
                { \
                    instance.WriteValue(#name, v); \
                    instance.Save(); \
                }

#define AddReadOnlyConfigurationItem(T, name) \
                static T Get##name() \
                { \
                    return instance.ReadValue<T>(#name); \
                }

typedef std::map<std::string, int> map_string_int_t;

        class NodeManagerConfig : Configuration
        {
            public:
                NodeManagerConfig() : Configuration("nodemanager.json") { }

                AddConfigurationItem(std::string, ClusterAuthenticationKey);
                AddConfigurationItem(std::string, TrustedCAPath);
                AddConfigurationItem(std::string, TrustedCAFile);
                AddConfigurationItem(std::string, CertificateChainFile);
                AddConfigurationItem(std::string, PrivateKeyFile);
                AddConfigurationItem(std::string, ListeningUri);
                AddConfigurationItem(std::string, DefaultServiceName);
                AddConfigurationItem(std::string, UdpMetricServiceName);
                AddConfigurationItem(bool, UseDefaultCA);
                AddConfigurationItem(bool, Debug);
                AddConfigurationItem(int, HostsFetchInterval);
                AddConfigurationItem(int, LogLevel);
                AddConfigurationItem(std::vector<std::string>, NamingServiceUri);
                AddConfigurationItem(std::string, MetricUri);
                AddConfigurationItem(std::string, HeartbeatUri);
                AddConfigurationItem(std::string, TaskCompletionUri);
                AddConfigurationItem(std::string, HostsFileUri);
                AddConfigurationItem(std::string, AzureInstanceMetaDataUri);
                AddConfigurationItem(long, HttpRequestTimeoutSeconds);

                static std::string ResolveRegisterUri(pplx::cancellation_token token)
                {
                    std::string uri = NodeManagerConfig::GetRegisterUri();
                    return ResolveUri(uri, [token](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName(), token); });
                }

                static std::string ResolveHeartbeatUri(pplx::cancellation_token token)
                {
                    std::string uri = NodeManagerConfig::GetHeartbeatUri();
                    return ResolveUri(uri, [token](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName(), token); });
                }

                static std::string ResolveMetricUri(pplx::cancellation_token token)
                {
                    std::string uri = NodeManagerConfig::GetMetricUri();
                    return ResolveUri(uri, [token](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetUdpMetricServiceName(), token); });
                }

                static std::string ResolveHostsFileUri(pplx::cancellation_token token)
                {
                    std::string uri = NodeManagerConfig::GetHostsFileUri();
                    return ResolveUri(uri, [token](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName(), token); });
                }

                static std::string ResolveMetricInstanceIdsUri(pplx::cancellation_token token)
                {
                    std::string uri = NodeManagerConfig::GetMetricInstanceIdsUri();
                    return ResolveUri(uri, [token](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName(), token); });
                }

                static std::string ResolveTaskCompletedUri(const std::string& uri, pplx::cancellation_token token)
                {
                    std::string configUri = NodeManagerConfig::GetTaskCompletionUri();
                    if (configUri.empty())
                    {
                        configUri = uri;
                    }

                    return ResolveUri(configUri, [token](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName(), token); });
                }

            protected:
            private:
                AddConfigurationItem(std::string, RegisterUri);
                AddConfigurationItem(std::string, MetricInstanceIdsUri);

                static std::string ResolveUri(const std::string& uri, std::function<std::string(std::shared_ptr<NamingClient>)> resolver)
                {
                    std::string u = uri;
                    size_t pos = u.find("{0}");
                    if (pos != std::string::npos)
                    {
                        auto namingServiceUri = NodeManagerConfig::GetNamingServiceUri();
                        if (namingServiceUri.size() > 0)
                        {
                            // only when naming service configured.
                            return u.replace(pos, 3, resolver(NamingClient::GetInstance(NodeManagerConfig::GetNamingServiceUri())));
                        }
                        else
                        {
                            return "";
                        }
                    }

                    return std::move(u);
                }

                static NodeManagerConfig instance;
        };
    }
}

#endif // NODEMANAGERCONFIG_H
