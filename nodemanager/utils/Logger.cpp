#include "Logger.h"

using namespace hpc::utils;

Logger::Logger()
{
    loggers.push_back(spdlog::stdout_logger_mt("console"));
    loggers.push_back(spdlog::rotating_logger_mt("nodemanager", "logs/nodemanager", 1048576 * 5, 3));

    spdlog::set_level(spdlog::level::debug);
}

Logger Logger::instance;
