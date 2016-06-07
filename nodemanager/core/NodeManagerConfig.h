#ifndef NODEMANAGERCONFIG_H
#define NODEMANAGERCONFIG_H

#include "../utils/Configuration.h"

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

                AddConfigurationItem(std::string, RegisterUri);
                AddConfigurationItem(std::string, HeartbeatUri);
                AddConfigurationItem(std::string, MetricUri);
                AddConfigurationItem(std::string, HostsFileUri);
                AddConfigurationItem(std::string, ClusterAuthenticationKey);
                AddConfigurationItem(std::string, TrustedCAPath);
                AddConfigurationItem(std::string, TrustedCAFile);
                AddConfigurationItem(std::string, CertificateChainFile);
                AddConfigurationItem(std::string, PrivateKeyFile);
                AddConfigurationItem(std::string, ListeningUri);
                AddConfigurationItem(bool, UseDefaultCA);
                AddConfigurationItem(bool, Debug);
                AddConfigurationItem(int, HostsFetchInterval);

            protected:
            private:
                static NodeManagerConfig instance;
        };
    }
}

#endif // NODEMANAGERCONFIG_H
