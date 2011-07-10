using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace VANETs
{
    class VANETScheduler: Scheduler
    {
        public override void ProcessEvent(Node node, Event e)
        {
            VANETReader reader = (VANETReader)node;
            switch (e.Type)
            {
                case EventType.CHK_CERT:
                    reader.CheckCertificate((Certificate)e.Obj);
                    break;
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }
    }
}
