#include <sstream>

#include "String.h"

namespace hpc
{
    namespace utils
    {
        std::vector<std::string>& String::Split(const std::string& str, char delim, std::vector<std::string>& tokens)
        {
            std::istringstream iss(str);
            std::string token;

            while (getline(iss, token, delim))
            {
                tokens.push_back(token);
            }

            return tokens;
        }
    }
}

