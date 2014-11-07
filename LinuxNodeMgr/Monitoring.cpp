#include "Monitoring.h"

void * MonitoringThread(void* param){
	pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, NULL);
	pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, NULL);

	Monitoring * m = (Monitoring *)param;
	m->Run();

	return NULL;
}

Monitoring::Monitoring()
{
	pthread_mutex_init(&_lock, NULL);

	_cpuUsage = 0.0F;
	_availableMemory = 0.0F;
	_networkUsage = 0.0F;

	_cpuTotal = 0;
	_cpuIdle = 0;
	_network = 0;
}


Monitoring::~Monitoring()
{
	Stop();
}

void Monitoring::Start()
{
	int ret = pthread_create(&_threadId, NULL, MonitoringThread, (void *)this);
	if (ret != 0){
		cout << "Failed to start Monitoring thread!" << endl;
	}
}

void Monitoring::Stop()
{
	if (_threadId != 0){
		pthread_cancel(_threadId);
		pthread_join(_threadId, NULL);
	}
}

float Monitoring::GetCpuUsage()
{
	return _cpuUsage;
}

float Monitoring::GetAvailableMemory()
{
	return _availableMemory;
}

float Monitoring::GetNetworkUsage()
{
	return _networkUsage;
}

void Monitoring::Run()
{
	long int cputotal = 0, cpuidle = 0, memory = 0, network = 0;

	while (true){
		// get cpu usage
		cputotal = 0;
		cpuidle = 0;
		getCpuUsage(cputotal, cpuidle);
		_cpuUsage = (float)((cputotal - _cpuTotal) - (cpuidle - _cpuIdle)) / (cputotal - _cpuTotal) * 100.0F;
		_cpuTotal = cputotal;
		_cpuIdle = cpuidle;

		// get memory usage
		memory = 0;
		getAvailableMemory(memory);
		_availableMemory = (float)memory / 1024.0F;	// K -> M

		// get network speed
		network = 0;
		getNetworkUsage(network);
		_networkUsage = (float)(network - _network) / Monitoring::Interval;
		_network = network;

		// wait a little time
		// cout << "Monitoring : " << _cpuUsage << " " << _availableMemory << " " << _networkUsage << endl;
		sleep(Interval);
	}
}

void Monitoring::getCpuUsage(long int & cputotal, long int & cpuidle)
{
	char * ret = NULL;
	char buf[256];
	char cpu[5];
	long int user, nice, sys, idle, iowait, irq, softirq;

	FILE * fp = fopen("/proc/stat", "r");
	if (fp == NULL)
	{
		cputotal = 0;
		cpuidle = 0;
		return;
	}

	ret = fgets(buf, sizeof(buf), fp);
	if (ret) {
		sscanf(buf, "%s%ld%ld%ld%ld%ld%ld%ld", cpu, &user, &nice, &sys, &idle, &iowait, &irq, &softirq);
	}

	fclose(fp);

	cputotal = user + nice + sys + idle + iowait + irq + softirq;
	cpuidle = idle;

	return;
}

void Monitoring::getAvailableMemory(long int & memory)
{
	char * ret = NULL;
	char buf[256];
	char name[20];
	char unit[20];
	long int free;

	FILE * fp = fopen("/proc/meminfo", "r");
	if (fp == NULL)
	{
		memory = 0;
		return;
	}

	ret = fgets(buf, sizeof(buf), fp);
	ret = fgets(buf, sizeof(buf), fp);
	if (ret) {
		sscanf(buf, "%s%ld%s", name, &free, unit);
	}

	fclose(fp);

	memory = free;

	return;
}

void Monitoring::getNetworkUsage(long int & network)
{
	char * ret = NULL;
	char buf[256];
	char name[20];
	long int receive, send, tmp1, tmp2, tmp3, tmp4, tmp5, tmp6, tmp7, tmp8, tmp9, tmp10, tmp11, tmp12, tmp13, tmp14;

	FILE * fp = fopen("/proc/net/dev", "r");
	if (fp == NULL)
	{
		network = 0;
		return;
	}

	ret = fgets(buf, sizeof(buf), fp);
	ret = fgets(buf, sizeof(buf), fp);
	ret = fgets(buf, sizeof(buf), fp);
	if (ret) {
		sscanf(buf, "%s%ld%ld%ld%ld%ld%ld%ld%ld%ld%ld%ld%ld%ld%ld%ld%ld", name, &receive, &tmp1, &tmp2, &tmp3, &tmp4, &tmp5, &tmp6, &tmp7, &send, &tmp8, &tmp9, &tmp10, &tmp11, &tmp12, &tmp13, &tmp14);
	}

	fclose(fp);

	network = receive + send;

	return;
}

