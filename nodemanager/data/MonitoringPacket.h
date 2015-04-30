#ifndef MONITORINGPACKET_H
#define MONITORINGPACKET_H

#include <boost/uuid/uuid.hpp>

#include "../utils/Logger.h"
#include "Umid.h"

using namespace hpc::utils;

namespace hpc
{
    namespace data
    {
        using namespace boost::uuids;

        template<int UmidCount>
        class MonitoringPacket
        {
            public:
                struct Guid
                {
                    uint32_t g3:8;
                    uint32_t g2:8;
                    uint32_t g1:8;
                    uint32_t g0:8;
                    uint16_t g5:8;
                    uint16_t g4:8;
                    uint16_t g7:8;
                    uint16_t g6:8;
                    uint8_t g8:8;
                    uint8_t g9:8;
                    uint8_t g10:8;
                    uint8_t g11:8;
                    uint8_t g12:8;
                    uint8_t g13:8;
                    uint8_t g14:8;
                    uint8_t g15:8;

                    void AssignFrom(const uuid& id)
                    {
                        Logger::Debug("UUID variant rfc {0}", id.variant() == id.variant_rfc_4122);

                        g0 = id.data[0]; g1 = id.data[1]; g2 = id.data[2]; g3 = id.data[3];
                        g4 = id.data[4]; g5 = id.data[5]; g6 = id.data[6]; g7 = id.data[7];
                        g8 = id.data[8]; g9 = id.data[9]; g10 = id.data[10]; g11 = id.data[11];
                        g12 = id.data[12]; g13 = id.data[13]; g14 = id.data[14]; g15 = id.data[15];
                    }
                };

                MonitoringPacket(int version) : Version(version)
                {
                }

                int Version;
                Guid Uuid;
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
