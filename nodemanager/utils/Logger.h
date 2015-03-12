#ifndef LOGGER_H
#define LOGGER_H

#include <syslog.h>
#include <stdio.h>
#include <stdarg.h>
#include <iostream>
#include <spdlog/spdlog.h>

namespace hpc
{
    namespace utils
    {
        enum class LogLevel
        {
            Emergency =	0,	/* system is unusable */
            Alert = 1,	/* action must be taken immediately */
            Critical = 2,	/* critical conditions */
            Error = 3,	/* error conditions */
            Warning = 4,	/* warning conditions */
            Notice = 5,	/* normal but significant condition */
            Info = 6,	/* informational */
            Debug = 7	/* debug-level messages */
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
                static void Log(LogLevel level, const char* fmt, Args ...args)
                {
                    for (auto logger : instance.loggers)
                    {
                        switch (level)
                        {
                            case LogLevel::Emergency:
                                logger->emerg(fmt, args...);
                                break;
                            case LogLevel::Alert:
                                logger->alert(fmt, args...);
                                break;
                            case LogLevel::Critical:
                                logger->critical(fmt, args...);
                                break;
                            case LogLevel::Error:
                                logger->error(fmt, args...);
                                break;
                            case LogLevel::Warning:
                                logger->warn(fmt, args...);
                                break;
                            case LogLevel::Notice:
                                logger->notice(fmt, args...);
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
