#include <fstream>

#include "Configuration.h"
#include "../common/ErrorCodes.h"
#include "WriterLock.h"

using namespace hpc::utils;
using namespace hpc::common;

Configuration::Configuration(const std::string& configurationFile)
    : confFile(configurationFile)
{
    std::ifstream ifs(confFile, std::ios::in);

    if (ifs.good())
    {
        std::error_code error;
        this->data = web::json::value::parse(ifs, error);

        if (error)
        {
            Logger::Error("Failed to read {0}: {1}", confFile, error.message());
            exit(error.value());
        }

        ifs.close();
    }
    else
    {
        Logger::Error("Failed to open {0}", confFile);
        exit((int)ErrorCodes::ConfigurationFileError);
    }
}

void Configuration::Save()
{
    WriterLock writerLock(&this->lock);

    std::ofstream ofs(confFile, std::ios::trunc);

    if (ofs.good())
    {
        this->data.serialize(ofs);

        ofs.close();
    }
    else
    {
        Logger::Error("Failed to save {0}", confFile);
        exit((int)ErrorCodes::ConfigurationFileError);
    }
}
