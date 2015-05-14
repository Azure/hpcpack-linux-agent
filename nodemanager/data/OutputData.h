#ifndef OUTPUTDATA_H
#define OUTPUTDATA_H

#include <cpprest/json.h>
#include <string>

using namespace web;

class OutputData
{
    public:
        OutputData(const std::string& nodeName, int order, const std::string& content)
            : NodeName(nodeName), Order(order), Content(content)
        {
        }

        json::value ToJson() const;

        std::string NodeName;
        int Order;
        std::string Content;

    protected:
    private:
};

#endif // OUTPUTDATA_H
