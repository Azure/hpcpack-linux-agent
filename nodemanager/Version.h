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
                    { "1.4.2.1",
                        {
                            "Change the kill wait time to 1s",
                            "Only terminate the original process key when graceful period elapsed",
                            "Cleanup the whole CGroup when the main process exit",
                        }
                    },
                    { "1.4.3.1",
                        {
                            "Fix a security issue or running as root when the user has no privilege",
                        }
                    },
                    { "1.4.4.1",
                        {
                            "Fix the memory leak caused by deadlock",
                            "Work around the cgroup creation failure",
                            "Tuned the wait time of trust",
                        }
                    },
                    { "1.4.5.1",
                        {
                            "Fix the trust script sub process id capture issue",
                        }
                    },
                    { "1.4.5.1",
                        {
                            "Fix the trust script sub process id capture issue",
                        }
                    },
                    { "1.5.1.0",
                        {
                            "VM extension script fix",
                        }
                    },
                    { "1.6.1.0",
                        {
                            "Cleanup warnings from ShellCheck tool",
                        }
                    },
                    { "1.6.2.0",
                        {
                            "Adding a configuration file nodemanager.json",
                            "Add the authentication key support",
                            "Add the https support",
                        }
                    },
                    { "1.6.3.0",
                        {
                            "Metric configuration support (framework)",
                        }
                    },
                    { "1.6.4.0",
                        {
                            "Enabled customized CA",
                        }
                    },
                    { "1.6.5.0",
                        {
                            "Added debug mode",
                            "Added several metrics plugin",
                        }
                    },
                    { "1.6.6.0",
                        {
                            "Bind to any address when listening",
                        }
                    },
                    { "1.6.7.0",
                        {
                            "Fix network usage collection issue",
                        }
                    },
                    { "1.6.8.0",
                        {
                            "Change the Casablanca to use the construction time callback",
                            "Fix the environment PATH issue",
                        }
                    },
                    { "1.6.9.0",
                        {
                            "Remove the nginx dependency, support https natively",
                        }
                    },
                    { "1.6.10.0",
                        {
                            "Fix a reporting thread crash issue",
                        }
                    },
                    { "1.6.11.0",
                        {
                            "Fix a node error issue caused by rare case deadlock",
                            "Upgrade to latest spdlog library",
                        }
                    },
                    { "1.6.12.0",
                        {
                            "Added hosts file support",
                        }
                    },
                    { "1.6.13.0",
                        {
                            "Fix a deadlock caused by the input string stream reading",
                        }
                    },
                    { "1.6.14.0",
                        {
                            "Give the statistics an initial value to avoid overflow in scheduler database",
                        }
                    },
                    { "1.6.15.0",
                        {
                            "Fix a security issue which will disclose secrets",
                        }
                    },
                    { "1.6.16.0",
                        {
                            "Atomically change the configuration file",
                        }
                    },
                    { "1.6.17.0",
                        {
                            "Fix the host file manager bug",
                        }
                    },
                    { "1.6.18.0",
                        {
                            "Fix the cgroup root path issue",
                        }
                    },
                    { "1.6.19.0",
                        {
                            "Upgrade to use the latest cpprestsdk",
                            "Fix a memory leak issue in the cpprestsdk",
                        }
                    },
                    { "1.7.1.0",
                        {
                            "Added the execution filter support",
                            "Added unit test for execution filter",
                            "Improved the unit test framework",
                            "Fixed some problems in json format processing",
                        }
                    },
                    { "1.7.2.0",
                        {
                            "Fix the node unreachable error",
                            "Detect user's home directory instead of hard code",
                            "Skip the plugin if the file doesn't exist",
                            "Pass home directory to user's process",
                            "Added environment variable controlling bypass behavior of domain name on user name",
                        }
                    },
                    { "1.7.3.0",
                        {
                            "Fix two node manager crash issues",
                        }
                    },
                    { "1.7.4.0",
                        {
                            "Fix one node manager crash issue",
                        }
                    },
                    { "1.7.5.0",
                        {
                            "Fix one node manager crash issue",
                            "Fix the TaskStart filter",
                            "Fix a security issue",
                            "Fix a typo in the log",
                        }
                    },
                    { "1.7.6.0",
                        {
                            "Ignore the JobEndFilter exception",
                            "Cleanup the execution filter folder",
                        }
                    },
                    { "1.7.7.0",
                        {
                            "Retry the heartbeat when failed to minimize heartbeat lost",
                            "Isolate the filter execution from task execution",
                            "Refine the folder name of filter execution directory",
                        }
                    },
                    { "1.7.8.0",
                        {
                            "Report instance level GPU metrics",
                            "Register with GPU information",
                        }
                    },
                    { "1.7.9.0",
                        {
                            "Fix a metric packet count issue",
                        }
                    },
                    { "1.7.10.0",
                        {
                            "Fix a bug of total memory data in metrics",
                        }
                    },
                    { "1.7.11.0",
                        {
                            "Fix a crash issue when cancelling a job",
                            "Fix a process statistics issue when cancelling a job which results in the job stuck in cancelling state",
                        }
                    },
                    { "2.1.1.0",
                        {
                            "Hpc Pack 2016 head node support",
                            "Dynamically resolve the Uris of the head node services",
                        }
                    },
                    { "2.1.2.0",
                        {
                            "Fix the unknown node availability issue",
                        }
                    },
                    { "2.1.3.0",
                        {
                            "Fix the node error issue when scheduler failover",
                        }
                    },
                    { "2.1.4.0",
                        {
                            "Fix a job stuck in running issue",
                        }
                    },
                    { "2.1.5.0",
                        {
                            "Fix a resync failure issue, caused job stuck in running",
                        }
                    },
                    { "2.1.6.0",
                        {
                            "Fix the task process never start issue",
                        }
                    },
                    { "2.2.1.0",
                        {
                            "Added built-in proxy support",
                        }
                    },
                    { "2.2.2.0",
                        {
                            "Added task completion uri configuration",
                        }
                    },
                    { "2.3.1.0",
                        {
                            "Added docker support",
                            "Added support to peek the task output",
                            "Fixed the unit test failure",
                        }
                    },
                    { "2.3.2.0",
                        {
                            "Removed the requirement of public key in extended data of user credential",
                            "Removed unnecessary warning log of GPU monitors",
                            "Fixed the issue that user credential information may be left on disk when using execution filter",
                            "Fixed a bug that root user may fail in mutual trust in mpi docker task",
                            "Enabled the user to specfic the private key only",
                        }
                    },
                    { "2.3.3.0",
                        {
                            "Modified user mapping logic",
                        }
                    },
                    { "2.3.4.0",
                        {
                            "Fixed a bug that task would fail when cgroup is not enabled",
                            "Generate hostfile/machinefile for various mpi applications",
                        }
                    },
                    { "2.3.5.0",
                        {
                            "Upgraded the spdlog version",
                            "Fixed the build in the old build environment",
                        }
                    },
                    { "2.3.6.0",
                        {
                            "Revise a log message",
                            "Add Azure instance metadata to monitor",
                            "Fix a bug that nodemanager would delete user ssh keys",
                            "Reduce naming service request interval when failing to get response from headnode",
                        }
                    },
                    { "2.3.7.0",
                        {
                            "Enable environment variable CCP_SWITCH_USER to run task command by switching user rather than by sudo",
                            "Fix affinity bug",
                        }
                    },
                    { "2.4.0.0",
                        {
                            "Fix 'Cores In Use' not correct bug",
                            "Fix a bug that HPC Pack environment variables will be lost if CCP_SWITCH_USER is set on CentOS",
                            "Add environment variable CCP_DISABLE_CGROUP to enable running a task without cgroup",
                            "Change the owner of home directory, which is created by Linux nodemanager, to the user instead of leaving it root",
                            "Change the default working directory to home",
                            "Fix a bug that memory is limited to the first NUMA node when using cgroup",
                            "Fix code defect: get home dir by tilde expansion",
                            "Enable task statistics when task is running",
                            "Fix a bug that zombie task clean up would fail when nodemanager starts",
                            "Fix a bug that processes in task are not actually terminated after task canceling when cgroup is not enabled",
                            "Add properties 'CcpVersion' and 'CustomProperties' in node registration info",
                            "Support monitoring InfiniBand network usage",
                            "Fix node manager crash issue due to out-of-bound array writing when constructing monitoring packet with too many data values",
                            "Support monitoring multiple instances of network usage, which is set as default instead of monitoring total usage",
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
