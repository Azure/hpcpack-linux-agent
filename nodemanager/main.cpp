#include <iostream>
#include <string>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "utils/Logger.h"
#include "core/RemoteCommunicator.h"
#include "core/RemoteExecutor.h"
#include "Version.h"
#include "common/ErrorCodes.h"

#ifdef DEBUG
    #include "test/TestRunner.h"

    using namespace hpc::tests;
#endif // DEBUG

using namespace std;
using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc;
using namespace hpc::common;

void Cleanup()
{
    Logger::Info("Cleaning up zombie processes");
    Process::Cleanup();
}

int main(int argc, char* argv[])
{
    if (argc > 1)
    {
        if (string("-v") == argv[1])
        {
            Version::PrintVersionHistory();
            return 0;
        }
    }

    std::cout << "Node manager started." << std::endl;
    Logger::Info("Log system works.");
    Logger::Info("Version: {0}", Version::GetVersion());

    std::string output;
    int ret = System::ExecuteCommandOut(
        output,
        "sed -i --",
        "'s/\\(Defaults\\s\\+\\)\\(requiretty\\)/\\1!\\2/g'",
        "/etc/sudoers");

    if (ret != 0)
    {
        Logger::Error(
            "Failed when execute {0}, exit code {1}",
            "sed -i -- 's/\\(Defaults\\s\\+\\)\\(requiretty\\)/\\1!\\2/g' /etc/sudoers",
            ret);

        return ret;
    }

#ifdef DEBUG

    if (argc > 1)
    {
        cout << "Testing";

        if (string("-t") == argv[1])
        {
            TestRunner tr;
            bool result = tr.Run();
            return result ? 0 : (int)ErrorCodes::TestRunFailed;
        }
    }

#endif // DEBUG

    Cleanup();

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
