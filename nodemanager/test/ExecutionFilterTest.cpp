#include "ExecutionFilterTest.h"

#ifdef DEBUG

#include <iostream>
#include <fstream>
#include <string>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>
#include <boost/algorithm/string/predicate.hpp>

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

bool ExecutionFilterTest::JobStart()
{
    bool result = true;
    const std::string networkName = "";
    RemoteExecutor executor(networkName);

    http_listener_config config;
    config.set_ssl_context_callback([] (auto& ctx)
    {
        HttpHelper::ConfigListenerSslContext(ctx);
    });

    RemoteCommunicator rc(executor, config, "http://localhost:40000");
    rc.Open();

    http_client client(U("http://localhost:40000/"));
    auto nodeName = System::GetNodeName();
    std::string uri = "/api/" + nodeName + "/startjobandtask";
    uri_builder builder(uri);

    std::string stderrFile = "/tmp/JobStartFilterTest";
    std::string output;
    System::ExecuteCommandOut(output, "rm", stderrFile);

    ProcessStartInfo psi(
        "mpiexec hostname",
        "",
        "",
        std::string(stderrFile),
        "",
        0,
        std::vector<uint64_t>(),
        { { "CCP_MPI_SOURCE", "invalidPath" } });

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
    std::ifstream outFile(stderrFile, std::ios::in);
    if (outFile)
    {
        std::string output((std::istreambuf_iterator<char>(outFile)), std::istreambuf_iterator<char>());
        std::string expectedSuffix = "invalidPath/mpiexec: No such file or directory\n";
        Logger::Debug("stdout value \"{0}\", expected value \"... {1}\"", output, expectedSuffix);
        result &= boost::algorithm::ends_with(output, expectedSuffix);
    }
    else
    {
        Logger::Debug("Stdout file not found {0}", stderrFile);
        result = false;
    }

    return result;
}

#endif // DEBUG
