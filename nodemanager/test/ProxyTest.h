#ifndef PROXYTEST_H
#define PROXYTEST_H

#ifdef DEBUG

namespace hpc
{
    namespace tests
    {
        class ProxyTest
        {
            public:
                ProxyTest() { }

                static bool ProxyToLocal();

            protected:
            private:
        };
    }
}

#endif // DEBUG

#endif // EXECUTIONFILTERTEST_H
