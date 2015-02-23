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
                static std::vector<std::string> Split(const std::string& str, char delim);

                template <typename T, typename ... Args>
                static std::string Join(const T& delim, const Args& ...args)
                {
                    std::ostringstream oss;
                    bool first = true;

                    auto tmp = { ((first ? oss : oss << delim) << args, first = false)... };
                    [&tmp]() { };

                    return std::move(oss.str());
                }
            protected:
            private:
        };
    }
}

#endif // STRING_H
