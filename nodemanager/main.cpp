#include <iostream>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "utils/Logger.h"
#include "core/RemoteCommunicator.h"
#include "core/RemoteExecutor.h"
#include "Version.h"

using namespace std;
using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc;

int main()
{
    std::cout << "Node manager started." << std::endl;
    Logger::Info("Log system works.");
    Logger::Info("Version: {0}", Version::GetVersion());

    const std::string networkName = "eth0";
    RemoteExecutor executor(networkName);

    RemoteCommunicator rc(networkName, executor);
    rc.Open();

    while (true)
    {
        sleep(100);
    }

  //  rc.Close();

    return 0;
}
