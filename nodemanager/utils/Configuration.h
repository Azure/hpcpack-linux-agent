#ifndef CONFIGURATION_H
#define CONFIGURATION_H

#include <string>
#include <cpprest/json.h>

#include "Logger.h"
#include "JsonHelper.h"

namespace hpc
{
    namespace utils
    {
        /// The Configuration class is for general json configuration file.
        /// It shouldn't know anything about the actual configuration item.
        /// So it should be put under utils.
        class Configuration
        {
            public:
                Configuration(const std::string& configurationFile);
                ~Configuration() { pthread_rwlock_destroy(&this->lock); }
                void Save();

                template <typename T>
                T ReadValue(const std::string& name)
                {
                    return JsonHelper<T>::Read(name, this->data);
                }

                template <typename T>
                void WriteValue(const std::string& name, const T& v)
                {
                    JsonHelper<T>::Write(name, this->data, v);
                }

            protected:
            private:
                std::string confFile;
                web::json::value data;
                pthread_rwlock_t lock = PTHREAD_RWLOCK_INITIALIZER;
        };
    }
}

#endif // CONFIGURATION_H
