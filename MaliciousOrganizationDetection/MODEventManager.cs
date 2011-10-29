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
                    ((MODReader)node).readerType = ReaderType.DROP_PACKET;
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
                    ((MODOrganization)node).orgType = ReaderType.DROP_PACKET;
                else
                    throw new Exception("Unknown bad node type: " + array[2]);
            }
            else
            {
                base.ParseEventArgs(array);
            }
        }

    }
}
