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
                Logger::Info("Skipped sending to {0} because of failure in request handler", uri);
                return;
            }
        }

        http_response response = client.request(request, this->cts.get_token()).get();
        Logger::Debug("---------> Reported to {0} response code {1}", uri, response.status_code());
        if(this->responseHandler)
        {
            if (!this->responseHandler(response))
            {
                Logger::Info("Error in response handler for the request sent to {0}", uri);
            }
        }
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
