#include <iostream>
#include <iterator>
#include <regex>
#include <list>
#include <fstream>
#include <iomanip>
#include "HostsManager.h"
#include "HttpHelper.h"

using namespace hpc::utils;
using namespace hpc::data;
using namespace web::http;
using namespace hpc::core;

HostsManager::HostsManager(const std::string& hostsUri, int fetchInterval)
{
    this->hostsFetcher =
        std::unique_ptr<HttpFetcher>(
            new HttpFetcher(
                hostsUri,
                0,
                fetchInterval,
                [this](http_request& request)
                {
                    if (!this->updateId.empty())
                    {
                        request.headers().add(UpdateIdHeaderName, this->updateId);
                    }

                    return true;
                },
                [this](http_response& response)
                {
                    return this->HostsResponseHandler(response);
                }));
}

bool HostsManager::HostsResponseHandler(const http_response& response)
{
    if (response.status_code() == status_codes::NoContent)
    {
        Logger::Info("Hosts file manager: response received with no update");
        return true;
    }
    else if (response.status_code() != status_codes::OK)
    {
        Logger::Warn("Hosts file manager: failed to fetch hosts file: {0}", response.status_code());
        return false;
    }

    std::string respUpdateId;
    if (HttpHelper::FindHeader(response, UpdateIdHeaderName, respUpdateId))
    {
        this->updateId = respUpdateId;
        Logger::Info("Hosts file manager: hosts file update received with update Id {0}", respUpdateId);
        std::vector<HostEntry> hostEntries = JsonHelper<std::vector<HostEntry>>::FromJson(response.extract_json().get());
        this->UpdateHostsFile(hostEntries);
        return true;
    }

    return false;
}

void HostsManager::UpdateHostsFile(const std::vector<HostEntry>& hostEntries)
{
    Logger::Info("Hosts file manager: update local hosts file {0}", HostsFilePath);
    std::list<std::string> unmanagedLines;
    std::ifstream ifs(HostsFilePath, std::ios::in);
    std::string line;
    while (getline(ifs, line))
    {
        std::regex entryRegex(HPCHostEntryPattern);
        std::smatch entryMatch;
        // Strip the original HPC entries
        if(!std::regex_match(line, entryMatch, entryRegex))
        {
            unmanagedLines.push_back(line);
        }
        else
        {
            Logger::Debug("Strip the existing HPC host entry: ({0}, {1})", entryMatch[2], entryMatch[1]);
        }
    }

    ifs.close();

    std::ofstream ofs(HostsFilePath);
    auto it = unmanagedLines.cbegin();
    while(it != unmanagedLines.cend())
    {
        ofs << *it++ << std::endl;
    }

    // Append the HPC entries at the end
    for(std::size_t i=0; i<hostEntries.size(); i++)
    {
        Logger::Debug("Add HPC host entry: ({0}, {1})", hostEntries[i].HostName, hostEntries[i].IPAddress);
        ofs << std::left << std::setw(24) << hostEntries[i].IPAddress << std::setw(30) << hostEntries[i].HostName << "#HPC" << std::endl;
    }

    ofs.close();
}

