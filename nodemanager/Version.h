#ifndef VERSION_H_INCLUDED
#define VERSION_H_INCLUDED

#include <string>
#include <map>
#include <vector>

namespace hpc
{
    class Version
    {
        public:
            static const std::string& GetVersion()
            {
                static std::string version;

                static std::map<std::string, std::vector<std::string>> versionHistory =
                {
                    { "1.1.1.1",
                        {
                            "Node manager main functionality support",
                            "Added version support",
                            "Fixed network card reversed order issue",
                            "Added trace",
                            "Added error codes definition",
                            "Fixed a potential node error issue",
                        }
                    },
                    { "1.1.1.2",
                        {
                            "Fixed a long running issue because of callback failure",
                        }
                    },
                };

                auto it = --versionHistory.end();
                version = it->first;

                return version;
            }
    };
}

#endif // VERSION_H_INCLUDED
