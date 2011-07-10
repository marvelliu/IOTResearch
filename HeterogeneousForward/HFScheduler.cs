using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    class HFScheduler : Scheduler
    {
        new public static HFScheduler ProduceScheduler()
        {
            return new HFScheduler();
        }

        public override void ProcessEvent(Node node, Event e)
        {
            HFReader reader = (HFReader)node;
            switch (e.Type)
            {
                case EventType.CHK_SW_NB:
                    reader.CheckSmallWorldNeighbors(e.Obj);
                    break;
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }
    }
}
