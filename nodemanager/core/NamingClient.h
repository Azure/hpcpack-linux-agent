#ifndef NAMINGCLIENT_H
#define NAMINGCLIENT_H

#include <cpprest/json.h>
#include <cpprest/http_client.h>
#include <functional>
#include "../utils/WriterLock.h"

namespace hpc
{
    namespace core
    {
        using namespace web;
        using namespace hpc::utils;

        class NamingClient
        {
            public:
                NamingClient(
                    const std::vector<std::string>& namingServices,
                    int interval) : intervalSeconds(interval), namingServicesUri(namingServices), lock(PTHREAD_RWLOCK_INITIALIZER)
                {
                }

                virtual ~NamingClient()
                {
                    this->cts.cancel();
                    pthread_rwlock_destroy(&this->lock);
                }

                static std::shared_ptr<NamingClient> GetInstance(const std::vector<std::string>& namingServices)
                {
                    if (!instance)
                    {
                        instance = std::make_shared<NamingClient>(namingServices, 1);
                    }

                    return instance;
                }

                std::string GetServiceLocation(const std::string& serviceName);

                static void InvalidateCache()
                {
                    if (instance)
                    {
                        WriterLock writerLock(&instance->lock);

                        instance->serviceLocations.clear();
                    }
                }

            private:
                void RequestForServiceLocation(const std::string& serviceName, std::string& serviceLocation);

                static std::shared_ptr<NamingClient> instance;

                int intervalSeconds;
                std::map<std::string, std::string> serviceLocations;
                std::vector<std::string> namingServicesUri;
                pplx::cancellation_token_source cts;
                pthread_rwlock_t lock;
        };
    }
}

#endif // NAMINGCLIENT_H
