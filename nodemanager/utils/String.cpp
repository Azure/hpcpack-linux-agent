#include <sstream>

#include "String.h"

using namespace hpc::utils

std::vector<std::string> String::Split(const std::string& str, char delim)
{
    std::istringstream iss(str);
    std::string token;

    std::vector<std::string> tokens;

    while (getline(iss, token, delim))
    {
        tokens.push_back(token);
    }

    return std::move(tokens);
}

