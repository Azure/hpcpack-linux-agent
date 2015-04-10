#include <iostream>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "utils/Logger.h"
#include "core/RemoteCommunicator.h"
#include "core/RemoteExecutor.h"
#include "common/ErrorCodes.h"

#ifdef DEBUG
    #include "test/TestRunner.h"

    using namespace hpc::tests;
#endif // DEBUG

using namespace std;
using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::common;

int main(int argc, char* argv[])
{
    std::cout << "Node manager started." << std::endl;
    Logger::Info("Log system works.");

#ifdef DEBUG

    if (argc > 1)
    {
        if (std::string(argv[1]) == "-t")
        {
            TestRunner tr;
            bool result = tr.Run();
            return result ? 0 : (int)ErrorCodes::TestRunFailed;
        }
    }

#endif // DEBUG

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
