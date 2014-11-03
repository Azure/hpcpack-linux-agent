#ifndef REMOTINGCOMMUNICATOR_H_INCLUDED
#define REMOTINGCOMMUNICATOR_H_INCLUDED

#include <iostream>
#include <vector>
#include <fstream>
#include <iostream>
#include <pthread.h>
#include <string.h>
#include <boost/regex.hpp>
#include <cpprest/http_listener.h>
#include <cpprest/json.h>

#include "JobTaskDb.h"
#include "RemotingExecutor.h"

using namespace web::http::experimental::listener;
using namespace web::http;
using namespace web;

class RemotingCommunicator
{
public:
    RemotingCommunicator(const std::string& address)
        : listener(NULL), AddressUri(address) {}

    ~RemotingCommunicator()
    {
        this->CloseListener();
    }

    void OpenListener();
    void CloseListener();

private:
    static void handle_post(http_request message);

    web::http::experimental::listener::http_listener* listener;

    static const std::string ApiSpace;
    const std::string& AddressUri;
    std::string callbackUri;
};

#endif // REMOTINGCOMMUNICATOR_H_INCLUDED
