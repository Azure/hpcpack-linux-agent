#include <iostream>
#include <iterator>
#include <regex>
#include <list>
#include <fstream>
#include <iomanip>
#include "HostsManager.h"

using namespace hpc::utils;
using namespace hpc::data;
using namespace web::http;
using namespace hpc::core;

HostsManager::HostsManager(const std::string& hostsUri)
{
    this->hostsFetcher =
        std::unique_ptr<HttpFetcher>(
            new HttpFetcher(
                hostsUri,
                0,
                FetchInterval,
                [this](http_request& request) {
                    if(!this->updateId.empty())
                       request.headers().add(UpdateIdHeaderName, this->updateId);
                    return true;
                },
                [this](http_response& response) {
                    return this->HostsResponseHandler(response);
                }));    
}

HostsManager::~HostsManager()
{
    this->hostsFetcher->Stop();    
}

void HostsManager::Start()
{
    this->hostsFetcher->Start();
}

void HostsManager::Stop()
{
    this->hostsFetcher->Stop();
}

bool HostsManager::HostsResponseHandler(const http_response& response)
{
    if(response.status_code != 200)
    {
        return;
    }

    std::string respUpdateId;
    if(HttpHelper::FindHeader(response, UpdateIdHeaderName, respUpdateId))
    {
        this.updateId = respUpdateId;
        std::vector<HostEntry> hostEntries = JsonHelper<std::vector<HostEntry>>::FromJson(response.extract_json().get());
        this.UpdateHostsFile(hostEntries);
    }
}

void HostsManager::UpdateHostsFile(const std::vector<HostEntry>& hostEntries)
{
    std::list<std::string> unmanagedLines;
    std::ifstream ifs("/etc/hosts", std::ios::in);
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
    }
    ifs.close();

    std::ofstream ofs("/etc/hosts");
    auto it = unmanagedLines.cbegin();
    while(it != unmanagedLines.cend())
    {
        ofs << *it << endl;
    }
    
    // Append the HPC entries at the end
    for(size_t i=0; i<hostEntries.size(); i++)
    {
        ofs << left << setw(24) << hostEntries[i].IPAddress << setw(30) << hostEntries[i].HostName << "#HPC" << endl;
    }
    ofs.close();
}

