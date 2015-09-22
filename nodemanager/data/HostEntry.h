#ifndef HOSTENTRY_H
#define HOSTENTRY_H

namespace hpc
{
    namespace data
    {
        class HostEntry
        {
            public:
                HostEntry()
                {
                }
                
                HostEntry(const std::string& hostName, const std::string& ipAddress):
                    hostName(hostName), IPAddress(ipAddress)
                {
                }

                
                HostEntry(const char* hostName, const char* ipAddress):
                    hostName(hostName), IPAddress(ipAddress)
                {
                }

                static HostEntry FromJson(const web::json::value& jsonValue);
                bool operator == (const HostEntry &entry) const
                {
                    return ((this->HostName.compare(entry.HostName) == 0) && (this->IPAddress.compare(entry.IPAddress) == 0));
                }
               
                std::string HostName;
                std::string IPAddress;
        };
    }
}

#endif // HOSTENTRY_H