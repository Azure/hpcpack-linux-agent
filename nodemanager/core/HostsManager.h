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
                const int FetchInterval = 300000;
                const std::string HPCHostEntryPattern = R"delimiter(^([0-9\.]+)\s+([^\s#]+)\s+#HPC\s*)delimiter";
                const std::string UpdateIdHeaderName = "UpdateId";
                HostsManager(const std::string& hostsUri);
                ~HostsManager();
                void Start();
                void Stop();
                
            protected:
            private:
                bool HostsResponseHandler(const http_response& response);
                void UpdateHostsFile(const std::vector<HostEntry>& hostEntries);
                std::string updateId;
                std::unique_ptr<HttpFetcher> hostsFetcher;
        };
    }
}

#endif // HOSTSMANAGER_H
