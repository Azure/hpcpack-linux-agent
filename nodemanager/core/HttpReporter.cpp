#include "HttpReporter.h"
#include "HttpHelper.h"
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

    http_client client = HttpHelper::GetHttpClient(uri);

    try
    {
        http_request request = HttpHelper::GetHttpRequest(methods::POST, jsonBody);

        http_response response = client.request(request, this->cts.get_token()).get();

        auto str = response.extract_string().get();
        std::istringstream iss(str);
        int milliseconds = 30000;
        iss >> milliseconds;
        if (milliseconds > 0)
        {
            this->intervalSeconds = milliseconds / 1000;
        }

        Logger::Debug("---------> Reported to {0} response code {1}, value {2}", uri, response.status_code(), milliseconds);
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
