#include <iostream>
#include <string>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "utils/Logger.h"
#include "core/RemoteCommunicator.h"
#include "core/RemoteExecutor.h"
#include "Version.h"
#include "core/NodeManagerConfig.h"
#include "common/ErrorCodes.h"
#include "core/HttpHelper.h"

#ifdef DEBUG
    #include "test/TestRunner.h"

    using namespace hpc::tests;
#endif // DEBUG

using namespace std;
using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc;
using namespace hpc::common;
using namespace web::http::experimental::listener;

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

/*
    trace = 0,
    debug = 1,
    info = 2,
    notice = 3,
    warn = 4,
    err = 5,
    critical = 6,
    alert = 7,
    emerg = 8,
    off = 9
*/
    auto level = spdlog::level::info;
    try { level = (spdlog::level::level_enum)NodeManagerConfig::GetLogLevel(); } catch (...) { std::cout << "No log level found in config file" << std::endl; }
    std::cout << "Log Level " << level << std::endl;
    Logger::SetLevel(level);
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
        if (string("-t") == argv[1])
        {
            TestRunner tr;
            bool result = tr.Run();
            return result ? 0 : (int)ErrorCodes::TestRunFailed;
        }
    }

#endif // DEBUG

    Cleanup();

    Logger::Debug(
        "Trusted CA File: {0}",
        NodeManagerConfig::GetTrustedCAFile());

    const std::string networkName = "";
    RemoteExecutor executor(networkName);

    http_listener_config config;
    config.set_ssl_context_callback([] (auto& ctx)
    {
        HttpHelper::ConfigListenerSslContext(ctx);
    });

    RemoteCommunicator rc(executor, config);
    rc.Open();

    while (true)
    {
        sleep(100);
    }

  //  rc.Close();

    return 0;
}
