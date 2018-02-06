#include "ProxyTest.h"

#ifdef DEBUG

#include <iostream>
#include <fstream>
#include <string>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "../utils/JsonHelper.h"
#include "../core/Process.h"
#include "../utils/Logger.h"
#include "../core/RemoteCommunicator.h"
#include "../core/RemoteExecutor.h"
#include "../core/NodeManagerConfig.h"
#include "../common/ErrorCodes.h"
#include "../core/HttpHelper.h"
#include "../utils/System.h"

using namespace hpc::tests;
using namespace hpc::core;
using namespace hpc::data;
using namespace hpc::utils;

using namespace web::http;
using namespace web;
using namespace web::http::experimental::listener;
using namespace web::http::client;

bool ProxyTest::ProxyToLocal()
{
    bool result = true;
    const std::string networkName = "";
    RemoteExecutor executor(networkName);

    http_listener_config config;
    config.set_ssl_context_callback([] (auto& ctx)
    {
        HttpHelper::ConfigListenerSslContext(ctx);
    });

    // listen to local host.
    RemoteCommunicator rc(executor, config, "http://localhost:40000");
    rc.Open();

    // TODO: enable the proxy test, need another listener.
    // request to localhost.
    http_client client(U("http://localhost:40000/"));
    auto nodeName = System::GetNodeName();

    // localhost will be redirected to local node name.
    std::string uri = "/api/localhost/startjobandtask";
    uri_builder builder(uri);

    std::string stdoutFile = "/tmp/JobStartFilterTest";
    std::string output;
    System::ExecuteCommandOut(output, "rm", stdoutFile);

    ProcessStartInfo psi(
        "echo 123",
        "",
        std::string(stdoutFile),
        "",
        "",
        0,
        std::vector<uint64_t>(),
        { { "CCP_ISADMIN", "1" } });

    StartJobAndTaskArgs arg(
        88,
        88,
        std::move(psi),
        "",
        "");

    json::value v1 = arg.ToJson();

    Logger::Debug("Debug JSON: {0}", v1.serialize());
    StartJobAndTaskArgs arg1 = StartJobAndTaskArgs::FromJson(v1);

    client.request(methods::POST, builder.to_string(), arg.ToJson()).then([&] (http_response response)
    {
        if (status_codes::OK != response.status_code())
        {
            Logger::Debug("The response code {0}", response.status_code());
            result = false;
        }
    }).wait();

    sleep(1);

    // verify the job start filter is called.
    // The command line is changed to echo 456.
    std::ifstream outFile(stdoutFile, std::ios::in);
    if (outFile)
    {
        int num;
        outFile >> num;
        Logger::Debug("stdout value {0}, expected value {1}", num, 456);
        result &= num == 456;
    }
    else
    {
        Logger::Debug("Stdout file not found {0}", stdoutFile);
        result = false;
    }

    return result;
}

#endif // DEBUG
