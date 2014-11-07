#include "RemotingExecutor.h"
#include "RemotingCommunicator.h"
#include <arpa/inet.h>
#include <sys/socket.h>
#include <sys/ioctl.h>
#include <net/if.h>
#include <syslog.h>
#include <unistd.h>
#include <stdio.h>
#include <stdlib.h>
#include <signal.h>
#include <sys/types.h>
#include <sys/stat.h>

#define MAX_BUF_LENGTH 500

void KillZombie()
{
    FILE* fileInput = fopen("PreviousPId","r");
    if (fileInput != NULL)
    {
        /// Read previous pid.
        std::vector<int> pidList;
        pidList.clear();
        std::set<int> PIdDict;
        PIdDict.clear();
        UnionFindSet ppidDict;
        int PId;
        while (fscanf(fileInput,"%d", &PId) != EOF) PIdDict.insert(PId);

        /// Read the answer of "ps -ef" and kill the zombie process which is build by previous running.
        char* buf  = (char*)malloc(sizeof(char) * MAX_BUF_LENGTH);
        char* name = (char*)malloc(sizeof(char) * MAX_BUF_LENGTH);
        FILE* pipeIn;
        if ((pipeIn = popen("ps -ef", "r")) != NULL)
        {
            if (fgets(buf, MAX_BUF_LENGTH, pipeIn))
            {
                while (fgets(buf, MAX_BUF_LENGTH, pipeIn))
                {
                    int tempPpid, tempPid;
                    sscanf(buf,"%s%d%d", name, &tempPid, &tempPpid);
                    pidList.push_back(tempPid);
                    ppidDict.AddPair(tempPid, tempPpid);
                }
            }

            pclose(pipeIn);

            for (size_t i = 0; i < pidList.size(); i++)
            if (PIdDict.find(ppidDict.FindParent(pidList[i])) != PIdDict.end()) kill(pidList[i], SIGKILL);
        }
        fclose(fileInput);
    }

    /// Write the current pid to file "PreviousPId"
    FILE* fileOutput = fopen("PreviousPId", "w");
    fprintf(fileOutput, "%d", getpid());
    fclose(fileOutput);
}

void CleanUpAllChildren(int parentPid)
{
    char* buf  = (char*)malloc(sizeof(char)*500);
    char* name = (char*)malloc(sizeof(char)*500);
    FILE* pipeIn;
    std::vector<int> pidList;pidList.clear();
    UnionFindSet ppidDict;

    ///scan the process list and kill
    if ((pipeIn = popen("ps -ef","r")) != NULL)
    {
        if (fgets(buf,500,pipeIn))
        {
            while (fgets(buf,500,pipeIn))
            {
                int tempPpid, tempPid;
                sscanf(buf,"%s%d%d", name, &tempPid, &tempPpid);
                pidList.push_back(tempPid);
                ppidDict.AddPair(tempPid, tempPpid);
            }
        }

        pclose(pipeIn);
        for (size_t i = 0; i < pidList.size(); i++)
            if (ppidDict.FindParent(pidList[i]) == parentPid) kill(pidList[i], SIGKILL);
    }
}

int main()
{
/*
    pid_t pid;

    pid = fork();

    if (pid == -1)
    {
        return -1;
    }
    else if (pid != 0)
    {
        exit (EXIT_SUCCESS);
    }

    if (setsid() == -1)
    {
        return -1;
    }

    umask(0);

    if (chdir("/") < 0) { return -1; }

    close(STDIN_FILENO);
    close(STDOUT_FILENO);
    close(STDERR_FILENO);

    std::cout << "Test" << std::endl;

    openlog("nodemanager", LOG_PID, LOG_DAEMON);
    syslog(LOG_INFO, "starting");
*/
    int inet_sock;
    struct ifreq ifr;
    char ip[32];

    inet_sock = socket(AF_INET, SOCK_DGRAM, 0);
    strcpy(ifr.ifr_name, "eth0");
    ioctl(inet_sock, SIOCGIFADDR, &ifr);
    strcpy(ip, inet_ntoa(((struct sockaddr_in *)&ifr.ifr_addr)->sin_addr));

    std::string sourceAddress = (std::string)"http://" + (std::string)ip + (std::string)":50001";
    std::cout << sourceAddress << std::endl;
   // syslog(LOG_INFO, sourceAddress.c_str());

    KillZombie(); 
	JobTaskDb::Initialize();

    RemotingCommunicator rc(sourceAddress);
    rc.OpenListener();
  //  syslog(LOG_INFO, "Started");
    std::cout << "Start" << std::endl;

	while (true){
		sleep(100);
	}

    rc.CloseListener();

  //  closelog();

    return 0;
}

