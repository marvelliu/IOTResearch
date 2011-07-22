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
                case EventType.SND_DATA:
                    Packet pkg = (Packet)e.Obj;
                    HFGlobal global = (HFGlobal)Global.getInstance();
                    global.currentSendingTags = pkg.Tags;
                    Console.WriteLine("{0:F4}\t[SND_DATA]\treader{1} sends data to reader{2} with tags {3}", currentTime, pkg.Src, pkg.Dst, Convert.ToString(pkg.Tags,2));
                    node.SendData(pkg);
                    break;
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }
    }
}
