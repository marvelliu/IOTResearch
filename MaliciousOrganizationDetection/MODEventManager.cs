using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace MaliciousOrganizationDetection
{
    class MODEventManager:EventManager
    {
        public override void ParseEventArgs(string[] array)
        {
            MODGlobal global = (MODGlobal)Global.getInstance();
            if (array[0] == "SET_BAD_NODE")
            {
                Node node = Node.getNode(array[1]);
                if (node == null)
                    throw new Exception("Unknown bad node: " + array[1]);
                string type = array[2].Trim();
                if (type == "DROP_PACKET")
                    ((MODReader)node).readerType = BehaviorType.DROP_PACKET;
                else
                    throw new Exception("Unknown bad node type: " + array[2]);
            }
            else if (array[0] == "SET_BAD_ORG")
            {
                Node node = Node.getNode(array[1]);
                if (node == null)
                    throw new Exception("Unknown bad node: " + array[1]);
                string type = array[2].Trim();
                if (type == "DROP_PACKET")
                    ((MODOrganization)node).orgType = BehaviorType.DROP_PACKET;
                else
                    throw new Exception("Unknown bad node type: " + array[2]);
            }
            else if (array[0] == "SET_MONI_NODE")
            {
                Node node = Node.getNode(array[1]);
                if (node == null)
                    throw new Exception("Unknown monitored node: " + array[1]);
                if(!global.monitoredNodes.Contains(node.Id))
                    global.monitoredNodes.Add(node.Id);

            }
            else
            {
                base.ParseEventArgs(array);
            }
        }

    }
}
