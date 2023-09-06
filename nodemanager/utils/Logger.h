#ifndef LOGGER_H
#define LOGGER_H

#include <syslog.h>
#include <stdio.h>
#include <stdarg.h>
#include <iostream>
#include <spdlog/spdlog.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/fmt/ostr.h>

#include "String.h"

namespace hpc
{
    namespace utils
    {
        enum class LogLevel
        {
            Critical = 0,	/* critical conditions */
            Error = 1,	/* error conditions */
            Warning = 2,	/* warning conditions */
            Info = 3,	/* informational */
            Debug = 4,	/* debug-level messages */
            Trace = 5	/* the most verbose messages */
        };

        class Logger
        {
            public:
                template <typename ...Args>
                static void Info(const char* fmt, Args ...args)
                {
                    Log(LogLevel::Info, fmt, args...);
                }

                template <typename ...Args>
                static void Error(const char* fmt, Args ...args)
                {
                    Log(LogLevel::Error, fmt, args...);
                }

                template <typename ...Args>
                static void Warn(const char* fmt, Args ...args)
                {
                    Log(LogLevel::Warning, fmt, args...);
                }

                template <typename ...Args>
                static void Debug(const char* fmt, Args ...args)
                {
                    Log(LogLevel::Debug, fmt, args...);
                }

                template <typename ...Args>
                static void Info(int jobId, int taskId, int requeue, const char* fmt, Args ...args)
                {
                    auto f = String::Join("", "Job ", jobId, ", Task ", taskId, ".", requeue, ": ", fmt);
                    Log(LogLevel::Info, f.c_str(), args...);
                }

                template <typename ...Args>
                static void Error(int jobId, int taskId, int requeue, const char* fmt, Args ...args)
                {
                    auto f = String::Join("", "Job ", jobId, ", Task ", taskId, ".", requeue, ": ", fmt);
                    Log(LogLevel::Error, f.c_str(), args...);
                }

                template <typename ...Args>
                static void Warn(int jobId, int taskId, int requeue, const char* fmt, Args ...args)
                {
                    auto f = String::Join("", "Job ", jobId, ", Task ", taskId, ".", requeue, ": ", fmt);
                    Log(LogLevel::Warning, f.c_str(), args...);
                }

                template <typename ...Args>
                static void Debug(int jobId, int taskId, int requeue, const char* fmt, Args ...args)
                {
                    auto f = String::Join("", "Job ", jobId, ", Task ", taskId, ".", requeue, ": ", fmt);
                    Log(LogLevel::Debug, f.c_str(), args...);
                }

                static void SetLevel(spdlog::level::level_enum l)
                {
                    spdlog::set_level(l);
                }

                template <typename ...Args>
                static void Log(LogLevel level, const char* fmt, Args ...args)
                {
                    for (auto logger : instance.loggers)
                    {
                        switch (level)
                        {
                            case LogLevel::Critical:
                                logger->critical(fmt, args...);
                                break;
                            case LogLevel::Error:
                                logger->error(fmt, args...);
                                break;
                            case LogLevel::Warning:
                                logger->warn(fmt, args...);
                                break;
                            case LogLevel::Info:
                                logger->info(fmt, args...);
                                break;
                            case LogLevel::Debug:
                                logger->debug(fmt, args...);
                                break;
                            default:
                                logger->trace(fmt, args...);
                        }

                        if (level <= LogLevel::Warning)
                        {
                            logger->flush();
                        }
                    }
                }

            private:
                Logger();

                static Logger instance;
                std::vector<std::shared_ptr<spdlog::logger>> loggers;
        };
    }
}

#endif // LOGGER_H
