#ifndef EXECUTIONFILTERTEST_H
#define EXECUTIONFILTERTEST_H

#ifdef DEBUG

namespace hpc
{
    namespace tests
    {
        class ExecutionFilterTest
        {
            public:
                ExecutionFilterTest() { }

                static bool JobStart();

            protected:
            private:
        };
    }
}

#endif // DEBUG

#endif // EXECUTIONFILTERTEST_H
