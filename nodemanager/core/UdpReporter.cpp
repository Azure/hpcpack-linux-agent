#include <sys/types.h>
#include <sys/socket.h>
#include <netdb.h>
#include <unistd.h>
#include <cpprest/http_client.h>

#include "UdpReporter.h"
#include "NodeManagerConfig.h"

using namespace hpc::core;
using namespace hpc::utils;

UdpReporter::UdpReporter(
    const std::string& name,
    std::function<std::string()> getReportUri,
    int hold,
    int interval,
    std::function<std::vector<unsigned char>()> fetcher)
    : Reporter<std::vector<unsigned char>>(name, getReportUri, hold, interval, fetcher)
{
}

void UdpReporter::ReConnect()
{
    if (this->s)
    {
        close(this->s);
    }

    std::string uri;

    try
    {
        uri = this->getReportUri();
    }
    catch (const http::http_exception& httpEx)
    {
        Logger::Warn("HttpException occurred when {2} report to {0}, ex {1}", uri, httpEx.what(), this->name);
    }
    catch (const std::exception& ex)
    {
        Logger::Error("Exception occurred when {2} report to {0}, ex {1}", uri, ex.what(), this->name);
    }
    catch (...)
    {
        Logger::Error("Unknown error occurred when {1} report to {0}", uri, this->name);
    }

    if (!uri.empty())
    {
        auto tokens = String::Split(uri, '/');
        auto endpoint = String::Split(tokens[2], ':');
        auto server = endpoint[0];
        auto port = endpoint[1];

        addrinfo hints;
        memset(&hints, 0, sizeof(hints));
        hints.ai_family = AF_INET;
        hints.ai_socktype = SOCK_DGRAM;
        hints.ai_flags = 0;
        hints.ai_protocol = 0;

        addrinfo* siRemote, *current;
        Logger::Info("getaddrinfo server {0}, port {1}", server, port);

        int ret = getaddrinfo(server.c_str(), port.c_str(), &hints, &siRemote);
        if (ret != 0)
        {
            Logger::Error("getaddrinfo failed {0}", gai_strerror(ret));
            return;
        }

        bool success = false;
        for (current = siRemote;
            current != nullptr;
            current = current->ai_next)
        {
            if ((this->s = socket(AF_INET, SOCK_DGRAM | SOCK_CLOEXEC, IPPROTO_UDP)) == -1)
            {
                Logger::Warn("create socket failed with errno {0}", errno);
                continue;
            }

            if (connect(this->s, current->ai_addr, current->ai_addrlen) != -1)
            {
                success = true;
                break;
            }

            close(this->s);
        }

        freeaddrinfo(siRemote);

        this->uri = uri;
        this->initialized = success;
    }
    else
    {
        this->initialized = false;
    }
}

UdpReporter::~UdpReporter()
{
    close(this->s);
    this->Stop();
}

int UdpReporter::Report()
{
    if (!this->initialized)
    {
        this->ReConnect();
        return -1;
    }

    auto data = this->valueFetcher();

//    std::vector<int> dataInt;
//    std::transform(data.cbegin(), data.cend(), std::back_inserter(dataInt), [] (unsigned char c) { return c; });
//    Logger::Debug("Report datasize {0}, data {1}", data.size(), String::Join<' '>(dataInt));

    auto buffer = &data[0];

    if (NodeManagerConfig::GetDebug())
    {
        std::vector<int> d;
        d.assign(data.begin(), data.end());

        Logger::Debug("Udp packet sent: {0}", String::Join<','>(d));
    }

    int ret = write(this->s, buffer, data.size());
    if (ret == -1)
    {
        Logger::Error(
            "Error when sendto {0}, socket {1}, errno {2}",
            this->uri,
            this->s,
            errno);

        this->initialized = false;
    }

    return ret;
}
