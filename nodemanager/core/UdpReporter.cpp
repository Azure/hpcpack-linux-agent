#include <sys/types.h>
#include <sys/socket.h>
#include <netdb.h>
#include <unistd.h>

#include "UdpReporter.h"

using namespace hpc::core;
using namespace hpc::utils;

UdpReporter::UdpReporter(
    const std::string& uri,
    int hold,
    int interval,
    std::function<std::vector<unsigned char>()> fetcher)
    : Reporter<std::vector<unsigned char>>(uri, hold, interval, fetcher)
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

    int ret = getaddrinfo(server.c_str(), port.c_str(), &hints, &siRemote);
    if (ret != 0)
    {
        Logger::Error("getaddrinfo failed {0}", gai_strerror(ret));
        throw std::runtime_error(String::Join(" ", "getaddrinfo failed", gai_strerror(ret)));
    }

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
            break;
        }

        close(this->s);
    }

    freeaddrinfo(siRemote);
}

UdpReporter::~UdpReporter()
{
    close(this->s);
}

void UdpReporter::Report()
{
    const std::string& uri = this->reportUri;

    auto tokens = String::Split(String::Split(uri, '/')[2], ':');
    auto serverName = tokens[0];
    auto port = tokens[1];

    auto data = this->valueFetcher();

    auto buffer = &data[0];

    int ret = write(this->s, buffer, data.size());
    if (ret == -1)
    {
        Logger::Error(
            "Error when sendto {0}, socket {1}, errno {2}",
            uri,
            this->s,
            errno);
    }
}
