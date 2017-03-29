#include "HttpReporter.h"
#include "HttpHelper.h"
#include "../utils/Logger.h"

using namespace web::http;
using namespace web::http::client;
using namespace hpc::core;
using namespace hpc::utils;

int HttpReporter::Report()
{
    std::string uri;

    try
    {
        uri = this->getReportUri();

        auto jsonBody = this->valueFetcher();
        if (jsonBody.is_null())
        {
            Logger::Error("Skipped reporting to {0} because json is null", uri);
            return -1;
        }

        Logger::Debug("---------> Report to {0} with {1}", uri, jsonBody);

        auto client = HttpHelper::GetHttpClient(uri);

        auto request = HttpHelper::GetHttpRequest(methods::POST, jsonBody);

        http_response response = client->request(*request, this->cts.get_token()).get();

        auto str = response.extract_string().get();
        std::istringstream iss(str);
        int milliseconds = 30000;
        iss >> milliseconds;

        if (milliseconds > 0)
        {
            this->intervalSeconds = milliseconds / 1000;
        }

        Logger::Debug("---------> Reported to {0} response code {1}, value {2}, interval {3}", uri, response.status_code(), milliseconds, this->intervalSeconds);

        if (response.status_code() == http::status_codes::OK)
        {
            return 0;
        }
        else
        {
            return -1;
        }
    }
    catch (const http_exception& httpEx)
    {
        Logger::Warn("HttpException occurred when {2} report to {0}, ex {1}", uri, httpEx.what(), this->name);
    }
    catch (const std::exception& ex)
    {
        Logger::Error("Exception occurred when {2} report to {0}, ex {1}", uri, ex.what(), this->name);
    }
    catch (...)
    {
        Logger::Error("Unknown error occurred when {1} report to {0}", uri, this->name);
    }

    return -1;
}
