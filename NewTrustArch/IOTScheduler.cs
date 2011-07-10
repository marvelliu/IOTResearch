using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace NewTrustArch
{
    class IOTScheduler:Scheduler
    {
        public override void ProcessEvent(Node node, Event e)
        {
            IOTReader reader = (IOTReader)node;
            switch (e.Type)
            {
                case EventType.CHK_RT_TIMEOUT:
                    reader.CheckRoutine();
                    break;
                case EventType.CHK_EVENT_TIMEOUT:
                    reader.CheckEvents();
                    break;
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }
    }
}
