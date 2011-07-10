using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocForward;

namespace TrustArch
{
    enum IOTPhenomemonType
    {
        RECV_PACKET,
        NOT_SEND_PACKET,
        SEND_PACKET,
        NOT_SEND_COMMAND,
        SEND_COMMAND,
        BANDWIDTH_BUSY,
        MOVE_FAST,
        SAME_PACKET_HEADER,
        SAME_PACKET_DATA,
        DIST_FAR,
    }

    class IOTPhenomemon
    {
        public IOTPhenomemonType type;
        public int nodeId;
        public double start;
        public double end;
        public double likehood;
        public Packet pkg;

        public IOTPhenomemon(IOTPhenomemonType type, int nodeId)
        {
            this.type = type;
            this.nodeId = nodeId;
            this.start = this.end = Scheduler.getInstance().CurrentTime;
        }

        public IOTPhenomemon(IOTPhenomemonType type, int nodeId, double time, Packet pkg)
        {
            this.type = type;
            this.nodeId = nodeId;
            this.start = this.end = time;
            this.pkg = pkg;
        }

        public IOTPhenomemon(IOTPhenomemonType type, int nodeId, double start, double end, Packet pkg)
        {
            this.type = type;
            this.nodeId = nodeId;
            this.start = start;
            this.end = end;
            this.pkg = pkg;
        }

        public static void ClearOutdatedPhenomemon(List<IOTPhenomemon> list)
        {
            double timeout = 10;
            List<IOTPhenomemon> temp = new List<IOTPhenomemon>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].start < Scheduler.getInstance().CurrentTime - timeout)
                    temp.Add(list[i]);
            }
            for (int i = 0; i < temp.Count; i++)
                list.Add(temp[i]);
        }
    }


}
