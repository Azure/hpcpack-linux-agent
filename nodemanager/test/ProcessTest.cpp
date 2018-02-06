#include "ProcessTest.h"

#ifdef DEBUG

#include <cpprest/http_listener.h>
#include "../utils/JsonHelper.h"
#include "../core/Process.h"

using namespace hpc::tests;
using namespace hpc::core;
using namespace hpc::data;

using namespace web::http;
using namespace web;

ProcessTest::ProcessTest()
{
    //ctor
}

bool ProcessTest::ClusRun()
{
    const std::string listeningUri = "http://localhost:50002/api/test";
    web::http::experimental::listener::http_listener listener(listeningUri);
    bool result = true;
    bool started = false;
    int callbackCount = 0;
    bool callbacked = false;

    listener.support(
        methods::POST,
        [&result, &callbackCount](auto request)
        {
            auto j = request.extract_json().get();
            Logger::Debug("Received: {0}", j.serialize());

            auto content = JsonHelper<std::string>::Read("Content", j);

            bool eq = content == "30\n" || content == "31\n" || content == "";

            result = result && eq;
            callbackCount++;

            request.reply(status_codes::OK, "OK");
        });

    listener.open().wait();

    Logger::Info("Listener opened");

    std::shared_ptr<Process> p = std::make_shared<Process>(
        25, 28, 1, "Task", "echo 30; sleep 1; echo 31", listeningUri, "", "", "", "root", true,
        std::vector<uint64_t>(), std::map<std::string, std::string>(),
        [&result, &callbacked]
        (int exitCode, std::string&& message, const ProcessStatistics& stat)
        {
            if (exitCode != 0) result = false;
           // if (message.empty()) result = false;
            if (stat.KernelTimeMs < 0) result = false;
            if (stat.UserTimeMs < 0) result = false;
            if (stat.ProcessIds.size() > 0) result = false;
            if (stat.WorkingSetKb < 0) result = false;

            Logger::Info("exitCode {0}, message {1}, result {2}", exitCode, message, result);
            callbacked = true;
        });

    pthread_t threadId;

    p->Start(p).then([&result, &started, &threadId] (std::pair<pid_t, pthread_t> ids)
    {
        if (ids.first <= 0) result = false;
        Logger::Info("pid {0}, result {1}", ids.first, result);
        threadId = ids.second;
        started = true;
    }).wait();

    pthread_join(threadId, nullptr);

    if (!(callbacked && started)) result = false;

    Logger::Info("calbackCount {0}", callbackCount);

    result = result && callbackCount == 3;

    Logger::Info("result {0}", result);

    return result;
}

bool ProcessTest::SimpleEcho()
{
    std::string userName = "nm_testuser", password = "Pass1word";

    bool result = true;
    bool started = false;
    bool callbacked = false;

    std::string privateKey = "-----BEGIN RSA PRIVATE KEY-----\
MIIEpQIBAAKCAQEAxJKBABhnOsE9eneGHvsjdoXKooHUxpTHI1JVunAJkVmFy8JC\
qFt1pV98QCtKEHTC6kQ7tj1UT2N6nx1EY9BBHpZacnXmknpKdX4Nu0cNlSphLpru\
lscKPR3XVzkTwEF00OMiNJVknq8qXJF1T3lYx3rW5EnItn6C3nQm3gQPXP0ckYCF\
Jdtu/6SSgzV9kaapctLGPNp1Vjf9KeDQMrJXsQNHxnQcfiICp21NiUCiXosDqJrR\
AfzePdl0XwsNngouy8t0fPlNSngZvsx+kPGh/AKakKIYS0cO9W3FmdYNW8Xehzkc\
VzrtJhU8x21hXGfSC7V0ZeD7dMeTL3tQCVxCmwIDAQABAoIBAQCve8Jh3Wc6koxZ\
qh43xicwhdwSGyliZisoozYZDC/ebDb/Ydq0BYIPMiDwADVMX5AqJuPPmwyLGtm6\
9hu5p46aycrQ5+QA299g6DlF+PZtNbowKuvX+rRvPxagrTmupkCswjglDUEYUHPW\
05wQaNoSqtzwS9Y85M/b24FfLeyxK0n8zjKFErJaHdhVxI6cxw7RdVlSmM9UHmah\
wTkW8HkblbOArilAHi6SlRTNZG4gTGeDzPb7fYZo3hzJyLbcaNfJscUuqnAJ+6pT\
iY6NNp1E8PQgjvHe21yv3DRoVRM4egqQvNZgUbYAMUgr30T1UoxnUXwk2vqJMfg2\
Nzw0ESGRAoGBAPkfXjjGfc4HryqPkdx0kjXs0bXC3js2g4IXItK9YUFeZzf+476y\
OTMQg/8DUbqd5rLv7PITIAqpGs39pkfnyohPjOe2zZzeoyaXurYIPV98hhH880uH\
ZUhOxJYnlqHGxGT7p2PmmnAlmY4TSJrp12VnuiQVVVsXWOGPqHx4S4f9AoGBAMn/\
vuea7hsCgwIE25MJJ55FYCJodLkioQy6aGP4NgB89Azzg527WsQ6H5xhgVMKHWyu\
Q1snp+q8LyzD0i1veEvWb8EYifsMyTIPXOUTwZgzaTTCeJNHdc4gw1U22vd7OBYy\
nZCU7Tn8Pe6eIMNztnVduiv+2QHuiNPgN7M73/x3AoGBAOL0IcmFgy0EsR8MBq0Z\
ge4gnniBXCYDptEINNBaeVStJUnNKzwab6PGwwm6w2VI3thbXbi3lbRAlMve7fKK\
B2ghWNPsJOtppKbPCek2Hnt0HUwb7qX7Zlj2cX/99uvRAjChVsDbYA0VJAxcIwQG\
TxXx5pFi4g0HexCa6LrkeKMdAoGAcvRIACX7OwPC6nM5QgQDt95jRzGKu5EpdcTf\
g4TNtplliblLPYhRrzokoyoaHteyxxak3ktDFCLj9eW6xoCZRQ9Tqd/9JhGwrfxw\
MS19DtCzHoNNewM/135tqyD8m7pTwM4tPQqDtmwGErWKj7BaNZCRUlhFxwOoemsv\
R6DbZyECgYEAhjL2N3Pc+WW+8x2bbIBN3rJcMjBBIivB62AwgYZnA2D5wk5o0DKD\
eesGSKS5l22ZMXJNShgzPKmv3HpH22CSVpO0sNZ6R+iG8a3oq4QkU61MT1CfGoMI\
a8lxTKnZCsRXU1HexqZs+DSc+30tz50bNqLdido/l5B4EJnQP03ciO0=\
-----END RSA PRIVATE KEY-----";

    System::DeleteUser(userName);
    std::string publicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDEkoEAGGc6wT16d4Ye+yN2hcqigdTGlMcjUlW6cAmRWYXLwkKoW3WlX3xAK0oQdMLqRDu2PVRPY3qfHURj0EEellpydeaSekp1fg27Rw2VKmEumu6Wxwo9HddXORPAQXTQ4yI0lWSerypckXVPeVjHetbkSci2foLedCbeBA9c/RyRgIUl227/pJKDNX2Rpqly0sY82nVWN/0p4NAyslexA0fGdBx+IgKnbU2JQKJeiwOomtEB/N492XRfCw2eCi7Ly3R8+U1KeBm+zH6Q8aH8ApqQohhLRw71bcWZ1g1bxd6HORxXOu0mFTzHbWFcZ9ILtXRl4Pt0x5Mve1AJXEKb hpclabsa@longhaulLN5-033\n";

    int ret = System::CreateUser(userName, password);
    if (ret != 0) return false;

    std::string keyFileName;
    ret = System::AddSshKey(userName, privateKey, "id_rsa", "600", keyFileName);
    if (ret != 0) return false;
    ret = System::AddSshKey(userName, publicKey, "id_rsa.pub", "644", keyFileName);
    if (ret != 0) return false;
    ret = System::AddAuthorizedKey(userName, publicKey, "600", keyFileName);
    if (ret != 0) return false;

    std::shared_ptr<Process> p = std::make_shared<Process>(
        25, 28, 1, "Task", "echo 30", "", "", "", "", userName, true,
        std::vector<uint64_t>(), std::map<std::string, std::string>(),
        [&result, &callbacked]
        (int exitCode, std::string&& message, const ProcessStatistics& stat)
        {
            if (exitCode != 0) result = false;
            if (message.empty()) result = false;
            if (stat.KernelTimeMs < 0) result = false;
            if (stat.UserTimeMs < 0) result = false;
            if (stat.ProcessIds.size() > 0) result = false;
            if (stat.WorkingSetKb < 0) result = false;

            Logger::Info("exitCode {0}, message {1}, result {2}", exitCode, message, result);
            callbacked = true;
        });

    pthread_t threadId;

    p->Start(p).then([&result, &started, &threadId] (std::pair<pid_t, pthread_t> ids)
    {
        if (ids.first <= 0) result = false;
        Logger::Info("pid {0}, result {1}", ids.first, result);
        threadId = ids.second;
        started = true;
    }).wait();

    pthread_join(threadId, nullptr);

    if (!(callbacked && started)) result = false;

    Logger::Info("result {0}", result);
    ret = System::DeleteUser(userName);
    if (ret != 0)
    {
        result = false;

        Logger::Info("ret {0}, result {1}", ret, result);
    }

    return result;
}

bool ProcessTest::Affinity()
{
    std::string userName = "nm_testuser", password = "Pass1word";

    bool result = true;
    bool started = false;
    bool callbacked = false;

    std::string privateKey = "-----BEGIN RSA PRIVATE KEY-----\
MIIEpQIBAAKCAQEAxJKBABhnOsE9eneGHvsjdoXKooHUxpTHI1JVunAJkVmFy8JC\
qFt1pV98QCtKEHTC6kQ7tj1UT2N6nx1EY9BBHpZacnXmknpKdX4Nu0cNlSphLpru\
lscKPR3XVzkTwEF00OMiNJVknq8qXJF1T3lYx3rW5EnItn6C3nQm3gQPXP0ckYCF\
Jdtu/6SSgzV9kaapctLGPNp1Vjf9KeDQMrJXsQNHxnQcfiICp21NiUCiXosDqJrR\
AfzePdl0XwsNngouy8t0fPlNSngZvsx+kPGh/AKakKIYS0cO9W3FmdYNW8Xehzkc\
VzrtJhU8x21hXGfSC7V0ZeD7dMeTL3tQCVxCmwIDAQABAoIBAQCve8Jh3Wc6koxZ\
qh43xicwhdwSGyliZisoozYZDC/ebDb/Ydq0BYIPMiDwADVMX5AqJuPPmwyLGtm6\
9hu5p46aycrQ5+QA299g6DlF+PZtNbowKuvX+rRvPxagrTmupkCswjglDUEYUHPW\
05wQaNoSqtzwS9Y85M/b24FfLeyxK0n8zjKFErJaHdhVxI6cxw7RdVlSmM9UHmah\
wTkW8HkblbOArilAHi6SlRTNZG4gTGeDzPb7fYZo3hzJyLbcaNfJscUuqnAJ+6pT\
iY6NNp1E8PQgjvHe21yv3DRoVRM4egqQvNZgUbYAMUgr30T1UoxnUXwk2vqJMfg2\
Nzw0ESGRAoGBAPkfXjjGfc4HryqPkdx0kjXs0bXC3js2g4IXItK9YUFeZzf+476y\
OTMQg/8DUbqd5rLv7PITIAqpGs39pkfnyohPjOe2zZzeoyaXurYIPV98hhH880uH\
ZUhOxJYnlqHGxGT7p2PmmnAlmY4TSJrp12VnuiQVVVsXWOGPqHx4S4f9AoGBAMn/\
vuea7hsCgwIE25MJJ55FYCJodLkioQy6aGP4NgB89Azzg527WsQ6H5xhgVMKHWyu\
Q1snp+q8LyzD0i1veEvWb8EYifsMyTIPXOUTwZgzaTTCeJNHdc4gw1U22vd7OBYy\
nZCU7Tn8Pe6eIMNztnVduiv+2QHuiNPgN7M73/x3AoGBAOL0IcmFgy0EsR8MBq0Z\
ge4gnniBXCYDptEINNBaeVStJUnNKzwab6PGwwm6w2VI3thbXbi3lbRAlMve7fKK\
B2ghWNPsJOtppKbPCek2Hnt0HUwb7qX7Zlj2cX/99uvRAjChVsDbYA0VJAxcIwQG\
TxXx5pFi4g0HexCa6LrkeKMdAoGAcvRIACX7OwPC6nM5QgQDt95jRzGKu5EpdcTf\
g4TNtplliblLPYhRrzokoyoaHteyxxak3ktDFCLj9eW6xoCZRQ9Tqd/9JhGwrfxw\
MS19DtCzHoNNewM/135tqyD8m7pTwM4tPQqDtmwGErWKj7BaNZCRUlhFxwOoemsv\
R6DbZyECgYEAhjL2N3Pc+WW+8x2bbIBN3rJcMjBBIivB62AwgYZnA2D5wk5o0DKD\
eesGSKS5l22ZMXJNShgzPKmv3HpH22CSVpO0sNZ6R+iG8a3oq4QkU61MT1CfGoMI\
a8lxTKnZCsRXU1HexqZs+DSc+30tz50bNqLdido/l5B4EJnQP03ciO0=\
-----END RSA PRIVATE KEY-----";

    std::string publicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDEkoEAGGc6wT16d4Ye+yN2hcqigdTGlMcjUlW6cAmRWYXLwkKoW3WlX3xAK0oQdMLqRDu2PVRPY3qfHURj0EEellpydeaSekp1fg27Rw2VKmEumu6Wxwo9HddXORPAQXTQ4yI0lWSerypckXVPeVjHetbkSci2foLedCbeBA9c/RyRgIUl227/pJKDNX2Rpqly0sY82nVWN/0p4NAyslexA0fGdBx+IgKnbU2JQKJeiwOomtEB/N492XRfCw2eCi7Ly3R8+U1KeBm+zH6Q8aH8ApqQohhLRw71bcWZ1g1bxd6HORxXOu0mFTzHbWFcZ9ILtXRl4Pt0x5Mve1AJXEKb hpclabsa@longhaulLN5-033\n";
    System::DeleteUser(userName);

    int ret = System::CreateUser(userName, password);
    if (ret != 0) return false;

    std::string keyFileName;
    ret = System::AddSshKey(userName, privateKey, "id_rsa", "600", keyFileName);
    if (ret != 0) return false;
    ret = System::AddSshKey(userName, publicKey, "id_rsa.pub", "644", keyFileName);
    if (ret != 0) return false;
    ret = System::AddAuthorizedKey(userName, publicKey, "600", keyFileName);
    if (ret != 0) return false;

    std::shared_ptr<Process> p = std::make_shared<Process>(
        25, 28, 1, "Task", "echo 30", "", "", "", "", userName, true,
        std::vector<uint64_t>({ UINT64_C(4) }), std::map<std::string, std::string>(),
        [&result, &callbacked]
        (int exitCode, std::string&& message, const ProcessStatistics& stat)
        {
            if (exitCode != 0) result = false;
            if (message.empty()) result = false;
            if (stat.KernelTimeMs < 0) result = false;
            if (stat.UserTimeMs < 0) result = false;
            if (stat.ProcessIds.size() > 0) result = false;
            if (stat.WorkingSetKb < 0) result = false;

            Logger::Info("exitCode {0}, message {1}, result {2}", exitCode, message, result);
            callbacked = true;
        });

    pthread_t threadId;

    p->Start(p).then([&result, &started, &threadId] (std::pair<pid_t, pthread_t> ids)
    {
        if (ids.first <= 0) result = false;
        Logger::Info("pid {0}, result {1}", ids.first, result);
        threadId = ids.second;
        started = true;
    }).wait();

    pthread_join(threadId, nullptr);

    if (!(callbacked && started)) result = false;

    Logger::Info("result {0}", result);
    ret = System::DeleteUser(userName);
    if (ret != 0)
    {
        result = false;

        Logger::Info("ret {0}, result {1}", ret, result);
    }

    return result;
}

bool ProcessTest::RemainingProcess()
{
    std::string userName = "nm_testuser", password = "Pass1word";

    bool result = true;
    bool started = false;
    bool callbacked = false;

    std::string privateKey = "-----BEGIN RSA PRIVATE KEY-----\
MIIEpQIBAAKCAQEAxJKBABhnOsE9eneGHvsjdoXKooHUxpTHI1JVunAJkVmFy8JC\
qFt1pV98QCtKEHTC6kQ7tj1UT2N6nx1EY9BBHpZacnXmknpKdX4Nu0cNlSphLpru\
lscKPR3XVzkTwEF00OMiNJVknq8qXJF1T3lYx3rW5EnItn6C3nQm3gQPXP0ckYCF\
Jdtu/6SSgzV9kaapctLGPNp1Vjf9KeDQMrJXsQNHxnQcfiICp21NiUCiXosDqJrR\
AfzePdl0XwsNngouy8t0fPlNSngZvsx+kPGh/AKakKIYS0cO9W3FmdYNW8Xehzkc\
VzrtJhU8x21hXGfSC7V0ZeD7dMeTL3tQCVxCmwIDAQABAoIBAQCve8Jh3Wc6koxZ\
qh43xicwhdwSGyliZisoozYZDC/ebDb/Ydq0BYIPMiDwADVMX5AqJuPPmwyLGtm6\
9hu5p46aycrQ5+QA299g6DlF+PZtNbowKuvX+rRvPxagrTmupkCswjglDUEYUHPW\
05wQaNoSqtzwS9Y85M/b24FfLeyxK0n8zjKFErJaHdhVxI6cxw7RdVlSmM9UHmah\
wTkW8HkblbOArilAHi6SlRTNZG4gTGeDzPb7fYZo3hzJyLbcaNfJscUuqnAJ+6pT\
iY6NNp1E8PQgjvHe21yv3DRoVRM4egqQvNZgUbYAMUgr30T1UoxnUXwk2vqJMfg2\
Nzw0ESGRAoGBAPkfXjjGfc4HryqPkdx0kjXs0bXC3js2g4IXItK9YUFeZzf+476y\
OTMQg/8DUbqd5rLv7PITIAqpGs39pkfnyohPjOe2zZzeoyaXurYIPV98hhH880uH\
ZUhOxJYnlqHGxGT7p2PmmnAlmY4TSJrp12VnuiQVVVsXWOGPqHx4S4f9AoGBAMn/\
vuea7hsCgwIE25MJJ55FYCJodLkioQy6aGP4NgB89Azzg527WsQ6H5xhgVMKHWyu\
Q1snp+q8LyzD0i1veEvWb8EYifsMyTIPXOUTwZgzaTTCeJNHdc4gw1U22vd7OBYy\
nZCU7Tn8Pe6eIMNztnVduiv+2QHuiNPgN7M73/x3AoGBAOL0IcmFgy0EsR8MBq0Z\
ge4gnniBXCYDptEINNBaeVStJUnNKzwab6PGwwm6w2VI3thbXbi3lbRAlMve7fKK\
B2ghWNPsJOtppKbPCek2Hnt0HUwb7qX7Zlj2cX/99uvRAjChVsDbYA0VJAxcIwQG\
TxXx5pFi4g0HexCa6LrkeKMdAoGAcvRIACX7OwPC6nM5QgQDt95jRzGKu5EpdcTf\
g4TNtplliblLPYhRrzokoyoaHteyxxak3ktDFCLj9eW6xoCZRQ9Tqd/9JhGwrfxw\
MS19DtCzHoNNewM/135tqyD8m7pTwM4tPQqDtmwGErWKj7BaNZCRUlhFxwOoemsv\
R6DbZyECgYEAhjL2N3Pc+WW+8x2bbIBN3rJcMjBBIivB62AwgYZnA2D5wk5o0DKD\
eesGSKS5l22ZMXJNShgzPKmv3HpH22CSVpO0sNZ6R+iG8a3oq4QkU61MT1CfGoMI\
a8lxTKnZCsRXU1HexqZs+DSc+30tz50bNqLdido/l5B4EJnQP03ciO0=\
-----END RSA PRIVATE KEY-----";

    std::string publicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDEkoEAGGc6wT16d4Ye+yN2hcqigdTGlMcjUlW6cAmRWYXLwkKoW3WlX3xAK0oQdMLqRDu2PVRPY3qfHURj0EEellpydeaSekp1fg27Rw2VKmEumu6Wxwo9HddXORPAQXTQ4yI0lWSerypckXVPeVjHetbkSci2foLedCbeBA9c/RyRgIUl227/pJKDNX2Rpqly0sY82nVWN/0p4NAyslexA0fGdBx+IgKnbU2JQKJeiwOomtEB/N492XRfCw2eCi7Ly3R8+U1KeBm+zH6Q8aH8ApqQohhLRw71bcWZ1g1bxd6HORxXOu0mFTzHbWFcZ9ILtXRl4Pt0x5Mve1AJXEKb hpclabsa@longhaulLN5-033\n";
    System::DeleteUser(userName);

    int ret = System::CreateUser(userName, password);
    if (ret != 0) return false;

    std::string keyFileName;
    ret = System::AddSshKey(userName, privateKey, "id_rsa", "600", keyFileName);
    if (ret != 0) return false;
    ret = System::AddSshKey(userName, publicKey, "id_rsa.pub", "644", keyFileName);
    if (ret != 0) return false;
    ret = System::AddAuthorizedKey(userName, publicKey, "600", keyFileName);
    if (ret != 0) return false;

    std::shared_ptr<Process> p = std::make_shared<Process>(
        25, 28, 1, "Task", "echo 30; sleep 5 &", "", "", "", "", userName, true,
        std::vector<uint64_t>({ UINT64_C(4) }), std::map<std::string, std::string>(),
        [&result, &callbacked]
        (int exitCode, std::string&& message, const ProcessStatistics& stat)
        {
            Logger::Info("Callback started result {0}", result);
            if (exitCode != 0)
            {
                Logger::Debug("exitCode");
                result = false;
            }
            if (message.empty())
            {
                Logger::Debug("message");
                result = false;
            }
            if (stat.KernelTimeMs < 0)
            {
                Logger::Debug("Kernel");
                result = false;
            }

            if (stat.UserTimeMs < 0)
            {
                Logger::Debug("User");
                result = false;
            }
            if (stat.ProcessIds.size() != 0)
            {
                Logger::Debug("ProcessIds");
                result = false;
            }
            if (stat.WorkingSetKb < 0)
            {
                Logger::Debug("WorkingSetKb");
                result = false;
            }

            Logger::Info(
                "KernelTime {0}, UserTime {1}, ProcessIds {2}, WorkingSet {3}",
                stat.KernelTimeMs,
                stat.UserTimeMs,
                stat.ProcessIds.size(),
                stat.WorkingSetKb);

            Logger::Info("exitCode {0}, message {1}, result {2}",
                exitCode, message, result);

            callbacked = true;
        });

    pthread_t threadId;

    p->Start(p).then([&result, &started, &threadId] (std::pair<pid_t, pthread_t> ids)
    {
        if (ids.first <= 0) result = false;
        Logger::Info("pid {0}, result {1}", ids.first, result);
        threadId = ids.second;
        started = true;
    }).wait();

    pthread_join(threadId, nullptr);

    if (!(callbacked && started)) result = false;

    Logger::Info("result {0}", result);
    ret = System::DeleteUser(userName);
    if (ret != 0)
    {
        result = false;

        Logger::Info("ret {0}, result {1}", ret, result);
    }

    return result;
}

#endif // DEBUG
