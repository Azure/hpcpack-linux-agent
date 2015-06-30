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
            static const std::vector<std::pair<std::string, std::vector<std::string>>>& GetVersionHistory()
            {
                static std::vector<std::pair<std::string, std::vector<std::string>>> versionHistory =
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
                    { "1.1.1.8",
                        {
                            "Set timeout to let callback release locks to avoid deadlock",
                            "Request resync when callback fails",
                            "Adjust the maximum log size to avoid flushing important logs",
                            "Fixed the detection method of cgroup in CentOS 6.6",
                            "Make the return message of a task more verbose",
                            "Verify the cgroup creation status at the end of PrepareTask",
                            "Adjusted makefile output format",
                        }
                    },
                    { "1.1.1.9",
                        {
                            "Fixed the default working directory ownership",
                            "Write to the task message and log only when failure happened",
                            "Configure CentOS sudoers to allow non-tty execution",
                            "Changed test user name in unit test case",
                            "Won't delete the user in job ending to avoid multi-job interference",
                        }
                    },
                    { "1.1.1.10",
                        {
                            "Try to fix a thread pool leak issue",
                            "Support CGroup mounting point on CentOS 6.6",
                        }
                    },
                    { "1.1.1.11",
                        {
                            "Fix EndTask race condition with Task Completion",
                            "Order the version display",
                        }
                    },
                    { "1.2.1.1",
                        {
                            "Support CPU affinity",
                            "Cleanup the user ssh keys after job finish",
                            "Report the signal which kills the process",
                            "Fix a user name mis-match issue",
                        }
                    },
                    { "1.2.1.2",
                        {
                            "Wait for the node trust before running mpi job",
                        }
                    },
                    { "1.2.1.3",
                        {
                            "Run as root when the user is admin on head node",
                            "Change monitoring data to go through UDP packet",
                        }
                    },
                    { "1.2.1.4",
                        {
                            "Fix the monitoring data big-endian issue",
                        }
                    },
                    { "1.2.1.5",
                        {
                            "Fix the report uri cannot be resolved issue",
                        }
                    },
                    { "1.2.1.6",
                        {
                            "Correct the authorized_keys file permission",
                            "Report resource usage through cgroup statistics",
                            "Added Unit test for process count",
                        }
                    },
                    { "1.2.1.7",
                        {
                            "Set correct permissions on key files",
                        }
                    },
                    { "1.2.1.8",
                        {
                            "Remove the user even if it is logged on",
                            "Won't overwrite the keys if private key is not created",
                            "Work around the bug of removing multi-subsys cgroup in old version of CGroup tools",
                            "Fix the network usage monitoring issue",
                        }
                    },
                    { "1.2.1.9",
                        {
                            "Placeholder for linux extension bug fix",
                        }
                    },
                    { "1.2.1.10",
                        {
                            "Added logs for users left problem",
                        }
                    },
                    { "1.2.1.11",
                        {
                            "Treat the return value of the heart beat to adjust interval",
                        }
                    },
                    { "1.2.1.12",
                        {
                            "Fix the virtual method initialization race condition",
                        }
                    },
                    { "1.2.1.13",
                        {
                            "Fix the crashing issue of not properly initialized thread ID",
                            "Fix the trust failure issue caused by a wrongly specified node name",
                        }
                    },
                    { "1.2.1.14",
                        {
                            "Kill all processes associated with the user before deleting user",
                        }
                    },
                    { "1.2.1.15",
                        {
                            "Fix a crash issue due to the reporter thread race condition",
                            "Retry when the CGroup tools exit with code 82 and 96, CGroup tools bug",
                            "Change wait for trust period to 30 seconds",
                            "Keep all untrusted history",
                        }
                    },
                    { "1.2.1.16",
                        {
                            "Retry for permission issue of stdout and stderr",
                            "Refine the logic of keeping the state when trust failed",
                            "Fix a crash issue of reporter",
                        }
                    },
                    { "1.2.1.17",
                        {
                            "Keep the task folder in failed situation",
                            "Test stdout/stderr file access before and after the task run",
                            "Keep the trust related logs in separate folders per tasks",
                            "Enhance the log of wait for trust",
                        }
                    },
                    { "1.2.1.18",
                        {
                            "Skip deleting users",
                        }
                    },
                    { "1.2.1.19",
                        {
                            "Enhance logs for trouble shooting",
                        }
                    },
                    { "1.2.1.20",
                        {
                            "Enhance wait for trust logs for trouble shooting",
                        }
                    },
                    { "1.2.1.21",
                        {
                            "Capture the error code for popen error",
                            "Tune the wait for trust period",
                        }
                    },
                    { "1.2.1.22",
                        {
                            "Turned on ssh verbose log",
                        }
                    },
                    { "1.2.1.23",
                        {
                            "Prints ssh verbose log directly",
                        }
                    },
                    { "1.3.1.1",
                        {
                            "Add graceful preemption support",
                            "Prevent the common user with root name to run as root",
                        }
                    },
                    { "1.3.1.2",
                        {
                            "Kill the process immediately after the period expires",
                        }
                    },
                    { "1.3.1.3",
                        {
                            "Cleaning up zombie processes when startup",
                        }
                    },
                    { "1.3.1.4",
                        {
                            "Enable clusrun support",
                        }
                    },
                    { "1.3.1.5",
                        {
                            "Fix the stdout/stderr/stdin handling issues",
                        }
                    },
                    { "1.3.1.6",
                        {
                            "Fix the virtual method initialization race condition",
                            "Print meaningful error messages when node trust fails",
                        }
                    },
                    { "1.3.1.7",
                        {
                            "Exit immediately if the port cannot be opened",
                        }
                    },
                    { "1.3.1.8",
                        {
                            "Merging with 1.2",
                        }
                    },
                    { "1.3.1.9",
                        {
                            "Fix the exit code not captured issue",
                            "Terminate the task properly when end task called",
                        }
                    },
                    { "1.3.1.10",
                        {
                            "Avoid duplicated cleanup of zombie process",
                            "Fixed a crash due to merge of the code",
                            "Print an error message when node trust failed",
                        }
                    },
                    { "1.3.1.11",
                        {
                            "Added logs for trust process",
                        }
                    },
                    { "1.3.1.12",
                        {
                            "Work around a ssh command line issue",
                            "Removed an unnecessary error message",
                        }
                    },
                    { "1.3.1.13",
                        {
                            "Fix the network usage collection error on CentOS65",
                            "Fix the network usage data not precise issue",
                            "Avoid sending the EOF output back when not using stream output",
                        }
                    },
                    { "1.3.1.14",
                        {
                            "Clear the environment buffer when rerun",
                            "Inherit the path from the user specified",
                            "Change the node manager port",
                        }
                    },
                    { "1.3.1.15",
                        {
                            "Change the wait time for trust",
                            "Cancel the graceful period thread if the task completed by itself",
                        }
                    },
                    { "1.4.1.1",
                        {
                            "Fix a bug to remember the graceful period thread Id",
                            "Terminate the task forcefully if the graceful period is 0",
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
