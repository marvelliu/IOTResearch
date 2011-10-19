using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;

namespace MaliciousOrganizationDetection
{


    public class MODReader : Reader
    {
        
        new public static MODReader ProduceReader(int id, int org)
        {
            return new MODReader(id, org);
        }

        public MODReader(int id, int org)
            : base(id, org)
        {            
            //Event.AddEvent(new Event( global.startTime + global.checkSWHubCandidateInterval, EventType.CHK_SW_NB, this, null));
        }



        public override void ProcessPacket(Packet pkg)
        {
            //I send the packet myself, ignore
            if (pkg.Prev == Id && pkg.PrevType == type)
            {
                return;
            }

            //如果不存在邻居中，则添加.
            //如果存在，则更新时间
            //if (pkg.Beacon == null && !this.Neighbors.ContainsKey(pkg.Prev) && pkg.PrevType == NodeType.READER)
            if (pkg.Beacon == null && pkg.PrevType == NodeType.READER)
                RecvBeacon(pkg);

            switch (pkg.Type)
            {
                //Readers
                case PacketType.BEACON:
                    RecvBeacon(pkg);
                    break;
                //Some codes are hided in the base class.
                default:
                    base.ProcessPacket(pkg);
                    return;
            }
        }

    }
}
