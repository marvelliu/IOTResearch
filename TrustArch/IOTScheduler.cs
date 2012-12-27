using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace TrustArch
{
    class IOTScheduler:Scheduler
    {

        new public static IOTScheduler ProduceScheduler()
        {
            return new IOTScheduler();
        }

        public override void ProcessEvent(Node node, Event e)
        {
            if (node.type == NodeType.READER)
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
                return;
            }
            else if (node.type == NodeType.ORG)
            {
                IOTOrganization org = (IOTOrganization)node;
                switch (e.Type)
                {
                    case EventType.CHK_RT_TIMEOUT:
                        org.CheckRoutine();
                        break;
                    default:
                        base.ProcessEvent(node, e);
                        break;
                }
            }
            else if (node.type == NodeType.OBJECT)
            {
                IOTObjectNode obj = (IOTObjectNode)node;
                switch (e.Type)
                {
                    default:
                        base.ProcessEvent(node, e);
                        break;
                }
            }
            else if (node.type == NodeType.TRUST_MANAGER)
            {
                IOTTrustManager tm = (IOTTrustManager)node;
                switch (e.Type)
                {
                    case EventType.CHK_RT_TIMEOUT:
                        tm.CheckRoutine();
                        break;
                    default:
                        base.ProcessEvent(node, e);
                        break;
                }
            }
            else
            {
                base.ProcessEvent(node, e);
            }
        }
    }
}
