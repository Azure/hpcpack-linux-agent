#ifndef MONITORINGPACKET_H
#define MONITORINGPACKET_H

#include <boost/uuid/uuid.hpp>

#include "Umid.h"

namespace hpc
{
    namespace data
    {
        using namespace boost::uuids;

        template<int UmidCount>
        class MonitoringPacket
        {
            public:
                MonitoringPacket(int version) : Version(version)
                {
                }

                int Version;
                uuid Uuid;
                int Count;
                int TickCount;
                Umid Umids[UmidCount];
                float Values[UmidCount];

            protected:
            private:
        };
    }
}

#endif // MONITORINGPACKET_H
