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
            uint64_t UserTimeMs = 0;
            uint64_t KernelTimeMs = 0;
            uint64_t WorkingSetKb = 0;
            std::vector<int> ProcessIds;

            int GetProcessCount() const { return this->ProcessIds.size(); }

            bool IsTerminated() const { return this->GetProcessCount() == 0; }
        };
    }
}

#endif // PROCESSSTATISTICS_H
