#include "TestRunner.h"

#ifdef DEBUG

#include "../utils/Logger.h"
#include "ProcessTest.h"
#include "ExecutionFilterTest.h"

using namespace hpc::tests;
using namespace hpc::utils;

TestRunner::TestRunner()
{
    this->tests["SimpleEcho"] = []() { return ProcessTest::SimpleEcho(); };
    this->tests["Affinity"] = []() { return ProcessTest::Affinity(); };
    this->tests["RemainingProcess"] = []() { return ProcessTest::RemainingProcess(); };
    this->tests["ClusRun"] = []() { return ProcessTest::ClusRun(); };
    this->tests["FilterJobStart"] = []() { return ExecutionFilterTest::JobStart(); };
}

bool TestRunner::Run()
{
    bool finalResult = true;
    Logger::Info("========================================================");
    Logger::Info("Start Testing, {0} cases in total.", this->tests.size());
    Logger::Info("========================================================");

    std::map<std::string, bool> results;

    for (auto& t : this->tests)
    {
        Logger::Info("");
        Logger::Info("");
        Logger::Info("Testing {0}", t.first);
        Logger::Info("--------------------------------------------------------");
        bool result = t.second();
        Logger::Info("--------------------------------------------------------");
        Logger::Info("Result {0}", result ? "Passed" : "Failed");
        Logger::Info("");
        Logger::Info("");

        results[t.first] = result;

        if (!result) finalResult = false;
    }

    Logger::Info("");
    Logger::Info("");
    Logger::Info("========================================================");
    Logger::Info("Testing result summary");
    Logger::Info("========================================================");
    int passed = 0;
    for (auto& t : results)
    {
        Logger::Info("Testing {0} : {1}", t.first, t.second ? "Passed" : "Failed");
        if (t.second)
        {
            passed++;
        }
    }

    Logger::Info("========================================================");
    Logger::Info("All passed {0}, Pass rate {1} / {2}.", finalResult, passed, this->tests.size());

    return finalResult;
}

#endif // DEBUG
