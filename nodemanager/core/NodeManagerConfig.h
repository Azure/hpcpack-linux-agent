#ifndef NODEMANAGERCONFIG_H
#define NODEMANAGERCONFIG_H

#include "../utils/Configuration.h"

using namespace hpc::utils;

namespace hpc
{
    namespace core
    {
#define AddConfigurationItem(T, name) \
                static std::string Get##name() \
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
                AddConfigurationItem(std::string, ClusterAuthenticationKey);

            protected:
            private:
                static NodeManagerConfig instance;
        };
    }
}

#endif // NODEMANAGERCONFIG_H
