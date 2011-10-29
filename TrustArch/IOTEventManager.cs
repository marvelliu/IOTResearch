using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace TrustArch
{
    public class IOTEventManager : AdHocBaseApp.EventManager
    {
        public override void ParseEventArgs(string[] array)
        {

            IOTGlobal global = (IOTGlobal)Global.getInstance();
            if (array[0] == "SET_BAD_NODE")
            {
                Node node = Node.getNode(array[1]);
                if (node == null)
                    throw new Exception("Unknown bad node: " + array[1]);
                string type = array[2].Trim();
                if (type == "DROP_PACKET")
                    ((IOTReader)node).readerType = ReaderType.DROP_PACKET;
                else
                    throw new Exception("Unknown bad node type: " + array[2]);
            }
            else if (array[0] == "SET_TAG_ORG")
            {
                int obj = int.Parse(array[1]);
                int org = int.Parse(array[2]);
                global.objects[obj].OrgId = org;
            }
            else
            {
                base.ParseEventArgs(array);
            }
        }
    }
}
