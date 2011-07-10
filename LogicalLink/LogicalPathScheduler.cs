using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LogicalPath
{
    class LogicalPathScheduler : Scheduler
    {
        public LogicalPathScheduler():base()
        {}

        new public void NextStep()
        {
            MoveMobileNodes();
            while (global.events.Count > 0 && global.events[0].Time <= currentTime)
            {
                Event e = global.events[0];
                LogicalPathReader node = (LogicalPathReader)e.Node;
                switch (e.Type)
                {
                    case EventType.SND_BCN:
                        node.SendBeacon(currentTime);
                        break;
                    case EventType.RECV:
                        node.Recv((Packet)e.Obj);
                        break;
                    case EventType.SND_DATA:
                        node.SendAODVData((Packet)e.Obj);
                        break;
                    case EventType.CHK_NB:
                        node.CheckNeighbors();
                        break;
                    case EventType.CHK_REV_PATH_CACHE:
                        node.ClearOldReverseCache();
                        break;
                    default:
                        break;
                }
                global.events.Remove(e);
            }
        }
        
    }
}
