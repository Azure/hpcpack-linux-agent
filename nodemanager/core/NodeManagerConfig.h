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

                static std::string ResolveRegisterUri()
                {
                    std::string uri = NodeManagerConfig::GetRegisterUri();
                    return ResolveUri(uri, [](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName()); });
                }

                static std::string ResolveHeartbeatUri()
                {
                    std::string uri = NodeManagerConfig::GetHeartbeatUri();
                    return ResolveUri(uri, [](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName()); });
                }

                static std::string ResolveMetricUri()
                {
                    std::string uri = NodeManagerConfig::GetMetricUri();
                    return ResolveUri(uri, [](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetUdpMetricServiceName()); });
                }

                static std::string ResolveHostsFileUri()
                {
                    std::string uri = NodeManagerConfig::GetHostsFileUri();
                    return ResolveUri(uri, [](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName()); });
                }

                static std::string ResolveMetricInstanceIdsUri()
                {
                    std::string uri = NodeManagerConfig::GetMetricInstanceIdsUri();
                    return ResolveUri(uri, [](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName()); });
                }

                static std::string ResolveTaskCompletedUri(const std::string& uri)
                {
                    return ResolveUri(uri, [](std::shared_ptr<NamingClient> namingClient) { return namingClient->GetServiceLocation(NodeManagerConfig::GetDefaultServiceName()); });
                }

            protected:
            private:
                AddConfigurationItem(std::string, RegisterUri);
                AddConfigurationItem(std::string, HostsFileUri);
                AddConfigurationItem(std::string, MetricInstanceIdsUri);

                static std::string ResolveUri(const std::string& uri, std::function<std::string(std::shared_ptr<NamingClient>)> resolver)
                {
                    std::string u = uri;
                    size_t pos = u.find("{0}");
                    if (pos != std::string::npos)
                    {
                        return u.replace(pos, 3, resolver(NamingClient::GetInstance(NodeManagerConfig::GetNamingServiceUri())));
                    }

                    return std::move(u);
                }

                static NodeManagerConfig instance;
        };
    }
}

#endif // NODEMANAGERCONFIG_H
