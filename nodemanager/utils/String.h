#ifndef STRING_H
#define STRING_H

#include <string>
#include <vector>

namespace hpc
{
    namespace utils
    {
        class String
        {
            public:
                static std::vector<std::string>& Split(const std::string& str, char delim, std::vector<std::string>& tokens);

            protected:
            private:
        };
    }
}

#endif // STRING_H
