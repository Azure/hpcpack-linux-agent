#include "Logger.h"

using namespace hpc::utils;

Logger::Logger()
{
    loggers.push_back(spdlog::stdout_color_mt("console"));
    loggers.push_back(spdlog::rotating_logger_mt("nodemanager", "logs/nodemanager", 1048576 * 5, 100));

    spdlog::set_level(spdlog::level::debug);
    spdlog::set_pattern("[%m/%d %T.%e] %t %l: %v");
}

Logger Logger::instance;

