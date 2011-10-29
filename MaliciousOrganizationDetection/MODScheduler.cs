using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace MaliciousOrganizationDetection
{
    class MODScheduler : Scheduler
    {
        new public static MODScheduler ProduceScheduler()
        {
            return new MODScheduler();
        }

        public override void ProcessEvent(Node node, Event e)
        {
            MODReader reader = (MODReader)node;
            switch (e.Type)
            {
                case EventType.CHK_RT_TIMEOUT:
                    reader.CheckRoutine();
                    break;
                case EventType.CHK_RECV_PKT:
                    reader.CheckReceivedPacket((MODPhenomemon)e.Obj);
                    break;
                case EventType.DEDUCE_EVENT:
                    reader.DeduceEventType((string)e.Obj);
                    break;
                case EventType.FWD_EVENT_REPORT:
                    reader.ForwardEventReport((string)e.Obj);
                    break;
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }
    }
}
