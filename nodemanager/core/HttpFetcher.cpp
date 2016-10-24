#include "HttpFetcher.h"
#include "HttpHelper.h"
#include "../utils/Logger.h"

using namespace web::http;
using namespace web::http::client;
using namespace hpc::core;
using namespace hpc::utils;

int HttpFetcher::Report()
{
    std::string uri;
    try
    {
        uri = this->getReportUri();
        http_client client = HttpHelper::GetHttpClient(uri);

        http_request request = HttpHelper::GetHttpRequest(methods::GET);
        if (this->requestHandler)
        {
            if (!this->requestHandler(request))
            {
                Logger::Warn("Skipped fetching from {0} because of failure in request handler", uri);
                return -1;
            }
        }

        http_response response = client.request(request, this->cts.get_token()).get();
        Logger::Debug("---------> Fetched from {0} response code {1}", uri, response.status_code());
        if(this->responseHandler)
        {
            if (!this->responseHandler(response))
            {
                Logger::Warn("Error in response handler for the fetch request to {0}", uri);
            }
        }

        return 0;
    }
    catch (const http_exception& httpEx)
    {
        Logger::Warn("HttpException occurred when {2} fetching from {0}, ex {1}", uri, httpEx.what(), this->name);
    }
    catch (const std::exception& ex)
    {
        Logger::Error("Exception occurred when {2} fetching from {0}, ex {1}", uri, ex.what(), this->name);
    }
    catch (...)
    {
        Logger::Error("Unknown error occurred when {1} fetching from {0}", uri, this->name);
    }

    return -1;
}
