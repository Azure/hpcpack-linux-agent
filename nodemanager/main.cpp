#include <iostream>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "utils/Logger.h"
#include "core/RemoteCommunicator.h"
#include "core/RemoteExecutor.h"

using namespace std;
using namespace hpc::core;
using namespace hpc::utils;

int main()
{
    std::cout << "Node manager started." << std::endl;
    Logger::Info("Log system works.");

    RemoteExecutor executor;

    RemoteCommunicator rc(executor);
    rc.Open();

    while (true)
    {
        sleep(100);
    }

  //  rc.Close();

    return 0;
}
