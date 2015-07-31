#ifndef UMID_H
#define UMID_H

#include <inttypes.h>

namespace hpc
{
    namespace data
    {
        class Umid
        {
            public:
                Umid() = default;
                Umid(const Umid& umid) = default;

                Umid(uint16_t metricId, uint16_t instanceId)
                    : MetricId(metricId), InstanceId(instanceId)
                {
                }

                uint16_t MetricId;
                uint16_t InstanceId;

            protected:
            private:
        };
    }
}

#endif // UMID_H
