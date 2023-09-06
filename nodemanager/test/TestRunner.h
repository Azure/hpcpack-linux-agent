#ifndef TESTRUNNER_H
#define TESTRUNNER_H

#ifdef DEBUG

#include <map>
#include <string>
#include <functional>

namespace hpc
{
    namespace tests
    {
        class TestRunner
        {
            public:
                TestRunner();

                bool Run();

            protected:
            private:
                std::map<std::string, std::function<bool(void)>> tests;
        };
    }
}

#endif // DEBUG

#endif // TESTRUNNER_H
