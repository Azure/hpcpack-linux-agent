#ifndef STRING_H
#define STRING_H

#include <string>
#include <vector>
#include <sstream>
#include <algorithm>

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

                template <char delim, typename T>
                static std::string Join(const std::vector<T>& values)
                {
                    std::ostringstream oss;
                    bool first = true;

                    for (const auto& v : values)
                    {
                        (first ? oss : oss << delim) << v;
                        first = false;
                    }

                    return std::move(oss.str());
                }

                static inline std::string Trim(const std::string& s)
                {
                    auto front = std::find_if_not(s.cbegin(), s.cend(), [](int c) { return std::isspace(c); });
                    return std::string(
                        front,
                        std::find_if_not(
                            s.crbegin(),
                            std::string::const_reverse_iterator(front),
                            [](int c) { return std::isspace(c); }).base());
                }

                static inline std::string GetUserName(const std::string& domainUserName)
                {
                    std::string userName = domainUserName;
                    auto userNameTokens = String::Split(userName, '\\');
                    if (userNameTokens.size() > 1)
                    {
                        userName = userNameTokens[userNameTokens.size() - 1];
                    }

                    return std::move(userName);
                }

                template <typename T>
                static inline T ConvertTo(const std::string& str)
                {
                    T v;
                    std::istringstream iss(str);
                    iss >> v;
                    return v;
                }

            protected:
            private:
        };
    }
}

#endif // STRING_H
