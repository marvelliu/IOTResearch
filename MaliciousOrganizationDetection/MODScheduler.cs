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
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }
    }
}
