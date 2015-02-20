#ifndef STRING_H
#define STRING_H

#include <string>
#include <vector>
#include <sstream>

namespace hpc
{
    namespace utils
    {
        class String
        {
            public:
                static std::vector<std::string>& Split(const std::string& str, char delim, std::vector<std::string>& tokens);

                template <typename T, typename ... args>
                static std::string&& Join(const T& delim, std::string ...)
                {
                    std::ostringstream oss;
                    int i = 0;

                    auto tmp = { ((i == 0) ? oss : oss << delim <<  args)... };

                    return std::move(oss.str());
                }
            protected:
            private:
        };
    }
}

#endif // STRING_H
