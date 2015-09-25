#ifndef HOSTSMANAGER_H
#define HOSTSMANAGER_H

#include <string>
#include <vector>
#include "HttpFetcher.h"
#include "../data/HostEntry.h"

namespace hpc
{
    namespace core
    {
        using namespace web::http;
        class HostsManager
        {
            public:
                const std::string HostsFilePath = "/etc/hosts";
                const std::string HPCHostEntryPattern = R"delimiter(^([0-9\.]+)\s+([^\s#]+)\s+#HPC\s*)delimiter";
                const std::string UpdateIdHeaderName = "UpdateId";

                HostsManager(const std::string& hostsUri, int fetchInterval);
                ~HostsManager() { this->Stop(); }

                void Start() { this->hostsFetcher->Start(); }
                void Stop() { this->hostsFetcher->Stop(); }

            protected:
            private:
                bool HostsResponseHandler(const http_response& response);
                void UpdateHostsFile(const std::vector<hpc::data::HostEntry>& hostEntries);
                std::string updateId;
                std::unique_ptr<HttpFetcher> hostsFetcher;
        };
    }
}

#endif // HOSTSMANAGER_H
