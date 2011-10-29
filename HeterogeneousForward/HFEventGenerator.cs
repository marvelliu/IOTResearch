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

            sw.WriteLine();
            sw.WriteLine("#SND_DATA src dst begin end interval tag t1_t2_t3");
            if (generateMode == 1)// overwrite sending
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
                    if (dnode == BroadcastNode.Node)
                    {
                        int orgId = ((Reader)snode).OrgId;
                        Organization org = (Organization)Node.getNode(orgId, NodeType.ORG);
                        List<Node> tmpNodes = new List<Node>();
                        foreach (Node node in org.nodes)
                        {
                            if (Utility.Distance((MobileNode)snode, (MobileNode)node) > mindist)
                                tmpNodes.Add(node);
                        }
                        if (tmpNodes.Count == 0)
                        {
                            foreach (Node node in global.readers)
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
                        }
                        int n = (int)Utility.U_Rand(tmpNodes.Count);
                        dnode = org.nodes[n];
                    }


                    HashSet<int> tmpTags = new HashSet<int>();
                    while (tmpTags.Count < tagNum)
                    {
                        int t = (int)Utility.U_Rand(global.tagNameNum);
                        if (((HFReader)snode).IsAllowedTags((uint)1 << t) && !tmpTags.Contains(t))
                            tmpTags.Add(t);
                    }

                    //uint tags = 0;
                    string tags = "";
                    foreach (int t in tmpTags)
                    {
                        //tags = tags | (uint)1 << t;
                        tags += (t + "_");
                    }
                    tags = tags.Substring(0, tags.Length - 1);

                    line = cmd + "\t" + snode + "\t" + dnode + "\t" + starttime + "\t" + endtime + "\t" + interval + "\ttag\t" + tags;
                    sw.WriteLine(line);
                }
            }
            else //generate new sending 
            {
                double current = 0;
                double currentSendSeg = 0;
                double sendSeg = (global.endTime - global.startTime) / global.SendEventNum;

                //开始有warming，skip
                currentSendSeg += 2 * sendSeg;

                for (int i = 0; i < global.SendEventNum; i++)
                {
                    Node snode = null;

                    do
                    {
                        snode = froms[Utility.Rand(froms.Length)];
                    } while (((HFReader)snode).IsAllowedAllTags());
                    Node dnode = snode;

                    int orgId = ((Reader)snode).OrgId;
                    Organization org = (Organization)Node.getNode(orgId, NodeType.ORG);
                    List<Node> tmpNodes = new List<Node>();
                    foreach (Node node in org.nodes)
                    {
                        if (Utility.Distance((MobileNode)snode, (MobileNode)node) > mindist && snode.Id != node.Id)
                            tmpNodes.Add(node);
                    }
                    if (tmpNodes.Count == 0)
                    {
                        foreach (Node node in global.readers)
                        {
                            if (Utility.Distance((MobileNode)snode, (MobileNode)node) > mindist)
                                tmpNodes.Add(node);
                        }
                        if (tmpNodes.Count == 0)
                        {
                            Console.WriteLine("[WARNING] reader{0} has no far enough nodes", snode);
                            continue;
                        }
                    }
                    dnode = tmpNodes[(int)Utility.U_Rand(tmpNodes.Count)];


                    //持续发送时间
                    double sendDuration = 0;
                    //发送的结束时间
                    double endtime = 0;
                    //发送间隔
                    double sendInterval = 0;

                    current = currentSendSeg + Utility.U_Rand(0, sendSeg);

                    if (global.SendEventDuration > 0)
                    {
                        sendDuration = global.SendEventDuration;
                        endtime = current + sendDuration;
                        sendInterval = global.SendEventInterval;
                    }
                    else
                    {
                        sendDuration = Utility.P_Rand(sendSeg);
                        endtime = Utility.U_Rand(current, global.endTime);
                        sendInterval = global.SendEventInterval;
                        current += sendDuration;
                    }

                    if (current >= global.endTime)//End
                        break;

                    int num = global.SendEventMinTagNum +
                            Utility.Rand(global.SendEventMaxTagNum - global.SendEventMinTagNum); //>=1                

                    //set中是发送数据包的标签集合，最多有num个标签
                    HashSet<int> tmpTags = new HashSet<int>();
                    while (tmpTags.Count < num)
                    {
                        int t = Utility.Rand(global.tagNameNum);
                        if (((HFReader)snode).IsAllowedTags((uint)1 << t) && !tmpTags.Contains(t))
                            tmpTags.Add(t);
                    }
                    string tags = "";
                    foreach (int t in tmpTags)
                    {
                        tags += (t + "_");
                    }
                    tags = tags.Substring(0, tags.Length - 1);

                    
                    line = cmd + "\t" + snode + "\t" + dnode + "\t" + current + "\t" + endtime + "\t" + sendInterval + "\ttag\t"
                        + tags;
                    sw.WriteLine(line);
                    Console.WriteLine("{0}---{1}", line, Utility.Distance((MobileNode)snode, (MobileNode)dnode));

                    currentSendSeg += sendSeg;
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
