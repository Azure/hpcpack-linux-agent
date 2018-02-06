#ifndef FILTEREXCEPTION_H
#define FILTEREXCEPTION_H

#include <stdexcept>

namespace hpc
{
    namespace filters
    {
        class FilterException : public std::runtime_error
        {
            public:
                FilterException(int errorCode, const std::string& message) : std::runtime_error(message), code(errorCode)
                {
                }

                int GetErrorCode() const { return this->code; }

            private:
                int code;
        };
    }
}

#endif
