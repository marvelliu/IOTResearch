using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LocationPrivacy
{
    class PrivacyScheduler:Scheduler
    {
        new public static Scheduler ProduceScheduler()
        {
            return new PrivacyScheduler();
        }

        public override void ProcessEvent(Node node, Event e)
        {
            PrivacyReader reader = (PrivacyReader)node;
            switch (e.Type)
            {
                case EventType.K_ANONY:
                    reader.JoinAnonyGroup(e.Obj);
                    break;
                case EventType.CHK_SUBTREE:
                    reader.CheckSubTree(e.Obj);
                    break;
                    /*
                case EventType.CHK_NEWGROUP:
                    reader.CheckNewGroup(e.Obj);
                    break;
                     */ 
                case EventType.CHK_NATGROUP:
                    reader.CheckNativeGroupResponse(e.Obj);
                    break;
                case EventType.CHK_NATGROUP1:
                    reader.CheckNativeLargeGroupResponse(e.Obj);
                    break;
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }
    }
}
