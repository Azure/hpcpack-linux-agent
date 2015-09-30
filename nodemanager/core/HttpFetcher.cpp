#include "HttpFetcher.h"
#include "HttpHelper.h"
#include "../utils/Logger.h"

using namespace web::http;
using namespace web::http::client;
using namespace hpc::core;
using namespace hpc::utils;

void HttpFetcher::Report()
{
    try
    {
        const std::string& uri = this->reportUri;
        http_client client = HttpHelper::GetHttpClient(uri);

        http_request request = HttpHelper::GetHttpRequest(methods::GET);
        if (this->requestHandler)
        {
            if (!this->requestHandler(request))
            {
                Logger::Warn("Skipped fetching from {0} because of failure in request handler", uri);
                return;
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
    }
    catch (const http_exception& httpEx)
    {
        Logger::Warn("HttpException occurred when fetching from {0}, ex {1}", this->reportUri, httpEx.what());
    }
    catch (const std::exception& ex)
    {
        Logger::Error("Exception occurred when fetching from {0}, ex {1}", this->reportUri, ex.what());
    }
    catch (...)
    {
        Logger::Error("Unknown error occurred when fetching from {0}", this->reportUri);
    }
}
