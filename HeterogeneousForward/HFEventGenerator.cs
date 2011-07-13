using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;

namespace HeterogeneousForward
{
    class HFEventGenerator:EventGenerator
    {
        public override void GenerateSendEvents(bool append, bool reload, Node[] froms, Node[] tos, string cmd)
        {
            HFGlobal global = (HFGlobal)Global.getInstance();
            string line = null;
            StreamWriter sw = null;
            sw = new StreamWriter(global.eventsFileName, append);
            sw.WriteLine();

            double t = (global.endTime - global.startTime) / global.SendEventNum;
            double current = 0;
            for (int i = 0; i < global.SendEventNum; i++)
            {
                Node snode = froms[Utility.Rand(froms.Length)];
                Node dnode = snode;

                if (snode.type == NodeType.OBJECT && tos[0].type == NodeType.ORG)
                    dnode = global.orgs[((ObjectNode)snode).OrgId];
                else
                {
                    while (dnode == snode)
                        dnode = tos[Utility.Rand(tos.Length)];
                }

                double time = Utility.P_Rand(t);
                current += time;
                if (current >= global.endTime)//End
                    break;
                double end = Utility.U_Rand(current, global.endTime);

                int num = global.SendEventMinTagNum +
                        Utility.Rand(global.SendEventMaxTagNum - global.SendEventMinTagNum); //>=1                
                
                HashSet<int> set = new HashSet<int>();
                while(set.Count>=num)
                {
                    int m = Utility.Rand(global.tagNameNum);
                    if (!set.Contains(m))
                        set.Add(m);
                } 
                string tags = "";
                foreach (int m in set)
                {
                    tags += (m + "_"); 
                }
                tags = tags.Substring(0, tags.Length - 1);

                line = cmd + "\t" + snode + "\t" + dnode + "\t" + (int)current + "\t" + num
                    + tags;
                sw.WriteLine(line);
            }


            if (sw != null)
                sw.Close();

            if (reload == true)
                Event.LoadEvents();
        }
    }
}
