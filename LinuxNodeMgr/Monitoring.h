#ifndef MONITORING_H
#define MONITORING_H

#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <iostream>
#include <pthread.h>
#include <unistd.h>

using namespace std;

class Monitoring
{
public:
	static const int Interval = 2;

	Monitoring();
	~Monitoring();

	void Start();
	void Stop();
	void Run();

	float GetCpuUsage();
	float GetAvailableMemory();
	float GetNetworkUsage();

private:
	void getCpuUsage(long int & cputotal, long int & cpuidle);
	long int _cpuTotal;
	long int _cpuIdle;

	void getAvailableMemory(long int & memory);

	void getNetworkUsage(long int & network);
	long int _network;

	pthread_t _threadId;
	pthread_mutex_t _lock;

	float _cpuUsage;
	float _availableMemory;
	float _networkUsage;
};

#endif	// MONITORING_H