#ifndef PROCESSSTATISTICS_H
#define PROCESSSTATISTICS_H

#include <vector>
#include <inttypes.h>

namespace hpc
{
    namespace data
    {
        struct ProcessStatistics
        {
            uint64_t UserTimeMs;
            uint64_t KernelTimeMs;
            uint64_t WorkingSetKb;
            std::vector<int> ProcessIds;

            int GetProcessCount() const { return this->ProcessIds.size(); }

            bool IsTerminated() const { return this->GetProcessCount() == 0; }
        };
    }
}

#endif // PROCESSSTATISTICS_H
