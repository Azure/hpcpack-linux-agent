#ifndef VERSION_H_INCLUDED
#define VERSION_H_INCLUDED

#include <string>
#include <map>
#include <vector>
#include <iostream>

namespace hpc
{
    class Version
    {
        public:
            static const std::map<std::string, std::vector<std::string>>& GetVersionHistory()
            {
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
                            "Added version history support",
                        }
                    },
                    { "1.1.1.3",
                        {
                            "Fixed a long running issue because of callback contract mismatch",
                        }
                    },
                    { "1.1.1.4",
                        {
                            "Retry when create cgroup failed",
                            "Return the exit code and error message when PrepareTask",
                            "Record the output to message in Process",
                        }
                    },
                    { "1.1.1.5",
                        {
                            "Print out version history",
                        }
                    },
                    { "1.1.1.6",
                        {
                            "Changed the process chain which will handle the user permission, std streams better",
                            "Added utilities to handle users",
                            "Added unit test framework",
                            "Added test case for some echo Process",
                            "Fixed a bug that a requeued task cannot be canceled",
                            "Fixed a bug that json not-existing value is handled incorrectly",
                        }
                    },
                    { "1.1.1.7",
                        {
                            "Make file dependency auto detect",
                        }
                    },
                };

                return versionHistory;
            }

            static const std::string& GetVersion()
            {
                auto& h = GetVersionHistory();
                auto it = --h.end();
                return it->first;
            }

            static void PrintVersionHistory()
            {
                auto& h = GetVersionHistory();
                for (auto& v : h)
                {
                    std::cout << v.first << std::endl;
                    std::cout << "================================================================" << std::endl;

                    int number = 0;
                    for (auto& m : v.second)
                    {
                        number++;
                        std::cout << number << ". " << m << std::endl;
                    }

                    std::cout << std::endl;
                }
            }

        private:

    };
}

#endif // VERSION_H_INCLUDED
