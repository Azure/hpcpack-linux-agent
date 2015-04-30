#include "HttpReporter.h"
#include "../utils/Logger.h"

using namespace web::http;
using namespace web::http::client;
using namespace hpc::core;
using namespace hpc::utils;

void HttpReporter::Report()
{
    const std::string& uri = this->reportUri;

    auto jsonBody = this->valueFetcher();
    if (jsonBody.is_null())
    {
        Logger::Info("Skipped reporting to {0} because json is null", uri);
        return;
    }

    Logger::Info("---------> Report to {0} with {1}", uri, jsonBody);

    http_client_config config;
    config.set_validate_certificates(false);
    utility::seconds timeout(5l);
    config.set_timeout(timeout);
    http_client client(uri, config);

    try
    {
        http_response response = client.request(methods::POST, "", jsonBody, this->cts.get_token()).get();

        Logger::Debug("---------> Reported to {0} response code {1}", uri, response.status_code());
    }
    catch (const http_exception& httpEx)
    {
        Logger::Warn("HttpException occurred when report to {0}, ex {1}", this->reportUri, httpEx.what());
    }
    catch (const std::exception& ex)
    {
        Logger::Error("Exception occurred when report to {0}, ex {1}", this->reportUri, ex.what());
    }
    catch (...)
    {
        Logger::Error("Unknown error occurred when report to {0}", this->reportUri);
    }
}
