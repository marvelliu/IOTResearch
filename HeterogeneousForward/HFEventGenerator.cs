using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;

namespace HeterogeneousForward
{
    class HFEventGenerator : EventGenerator
    {
        public void GenerateSendEvents(bool clear, bool reload, Node[] froms, Node[] tos, string cmd, double mindist, int tagNum, int generateMode)
        {
            HFGlobal global = (HFGlobal)Global.getInstance();
            string line = null;
            string tempfile = ".tmp";

            if (File.Exists(tempfile))
                File.Delete(tempfile);
            File.Copy(global.eventsFileName, tempfile);

            StreamReader sr = new StreamReader(tempfile);
            StreamWriter sw = new StreamWriter(global.eventsFileName, false);

            List<string> tempList = new List<string>();
            //先复制到文件
            while ((line = sr.ReadLine()) != null)
            {
                if (line.IndexOf(cmd) >= 0 && line[0] != '#')
                {
                    tempList.Add(line);
                    if (clear == true)
                        continue;
                }
                sw.WriteLine(line);
            }

            if (generateMode == 0)// overwrite sending
            {
                foreach (string c in tempList)
                {
                    string[] array = c.Split(new string[] { " ", "\t" }, StringSplitOptions.RemoveEmptyEntries);
                    Node snode = Node.getNode(array[1]);
                    Node dnode = Node.getNode(array[2]);
                    float starttime = float.Parse(array[3]);
                    float endtime = float.Parse(array[4]);
                    float interval = float.Parse(array[5]); ;

                    //这里需要找到与src同在一个机构的节点，tag一致
                    if (dnode == Node.BroadcastNode)
                    {
                        int orgId = ((Reader)snode).OrgId;
                        Organization org = (Organization)Node.getNode(orgId, NodeType.ORG);
                        List<Node> tmpNodes = new List<Node>();
                        foreach (Node node in org.Nodes)
                        {
                            if (Utility.Distance((MobileNode)snode, (MobileNode)node) > mindist)
                                tmpNodes.Add(node);
                        }
                        if (tmpNodes.Count == 0)
                        {
                            Console.WriteLine("[WARNING] reader{0} has no far enough nodes", snode);
                            sw.WriteLine(line);
                            continue;
                        }
                        int n = (int)Utility.U_Rand(tmpNodes.Count);
                        dnode = org.Nodes[n];
                    }


                    HashSet<int> tmpTags = new HashSet<int>();
                    while (tmpTags.Count < tagNum)
                    {
                        int t = (int)Utility.U_Rand(global.tagNameNum);
                        if (!tmpTags.Contains(t))
                            tmpTags.Add(t);
                    }

                    uint tags = 0;
                    foreach (int t in tmpTags)
                    {
                        tags = tags | (uint)1 << t;
                    }
                    line = cmd + "\t" + snode + "\t" + dnode + "\t" + starttime + "\t" + endtime + "\t" + interval + "\t" + tags;
                    sw.WriteLine(line);
                }
            }
            else //generate new sending 
            {
                //最长每个能发送多久
                double maxSendPeriod = (global.endTime - global.startTime) / global.SendEventNum;
                double current = 0;
                for (int i = 0; i < global.SendEventNum; i++)
                {
                    Node snode = froms[Utility.Rand(froms.Length)];
                    Node dnode = snode;

                    int orgId = ((Reader)snode).OrgId;
                    Organization org = (Organization)Node.getNode(orgId, NodeType.ORG);
                    List<Node> tmpNodes = new List<Node>();
                    foreach (Node node in org.Nodes)
                    {
                        if (Utility.Distance((MobileNode)snode, (MobileNode)node) > mindist)
                            tmpNodes.Add(node);
                    }
                    if (tmpNodes.Count == 0)
                    {
                        Console.WriteLine("[WARNING] reader{0} has no far enough nodes", snode);
                        continue;
                    }
                    int n = (int)Utility.U_Rand(tmpNodes.Count);
                    dnode = org.Nodes[n];


                    double sendPeriod = Utility.P_Rand(maxSendPeriod);
                    current += sendPeriod;
                    if (current >= global.endTime)//End
                        break;
                    double endtime = Utility.U_Rand(current, global.endTime);

                    int num = global.SendEventMinTagNum +
                            Utility.Rand(global.SendEventMaxTagNum - global.SendEventMinTagNum); //>=1                

                    HashSet<int> set = new HashSet<int>();
                    while (set.Count >= num)
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

                    line = cmd + "\t" + snode + "\t" + dnode + "\t" + current + "\t" + endtime + "\t" + maxSendPeriod + "\t"
                        + tags;
                    sw.WriteLine(line);
                }
            }


            if (sw != null)
                sw.Close();

            if (sr != null)
                sr.Close();

            if (reload == true)
                Event.LoadEvents();
        }
    }
}
