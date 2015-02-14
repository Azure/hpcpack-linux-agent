#include <iostream>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "utils/Logger.h"
#include "RemoteCommunicator.h"
#include "RemoteExecutor.h"

using namespace std;
using namespace hpc;

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
