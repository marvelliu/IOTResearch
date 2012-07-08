using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LocationPrivacy
{
    public class GroupArgs
    {
        public int k;
        public int l;
        public int random;
        public GroupArgs(int k, int l, int random)
        {
            this.k = k;
            this.l = l;
            this.random = random;
        }
    }

    class PrivacyEventManager:EventManager
    {
        public override void ParseEventArgs(string[] array)
        {

            PrivacyGlobal global = (PrivacyGlobal)Global.getInstance();
            if (array[0] == "ANONY")
            {
                Node node = Node.getNode(array[1]);
                if (node == null)
                    throw new Exception("Unknown query node: " + array[1]);
                int k = int.Parse(array[2].Trim());
                int h = int.Parse(array[3].Trim());
                float time = float.Parse(array[4].Trim());
                int random = int.Parse(array[5].Trim());
                if (k > 0 && time >= 0)
                    Event.AddEvent(new Event(time, EventType.K_ANONY, node, new GroupArgs(k, h, random)));
                else
                    throw new Exception("Bad k value or time: " + array[2] + " " + array[3]);
            }
            else
            {
                base.ParseEventArgs(array);
            }
        }
    }
}
