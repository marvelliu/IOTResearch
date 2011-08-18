using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    class HFScheduler : Scheduler
    {
        new public static HFScheduler ProduceScheduler()
        {
            return new HFScheduler();
        }

        public override void ProcessEvent(Node node, Event e)
        {
            HFReader reader = (HFReader)node;
            switch (e.Type)
            {
                case EventType.CHK_SW_NB:
                    reader.CheckSmallWorldNeighbors(e.Obj);
                    break;
                case EventType.SND_DATA:
                    Packet pkg = (Packet)e.Obj;
                    HFGlobal global = (HFGlobal)Global.getInstance();
                    global.currentSendingTags = pkg.Tags;
                    if (pkg.inited == false && pkg.Src == node.Id && pkg.Type == PacketType.DATA)
                    {
                        double distance = Utility.Distance((MobileNode)node, (MobileNode)Node.getNode(pkg.Dst, NodeType.READER));

                        double mindist = global.minSrcDstDist;
                        //为了控制在mindist左右，设置了距离上限
                        if (distance < mindist || distance >mindist+100)
                        {
                            int orgId = ((Reader)node).OrgId;
                            Organization org = (Organization)Node.getNode(orgId, NodeType.ORG);
                            List<Node> tmpNodes = new List<Node>();
                            //找出[mindist, mindist+100]的节点
                            foreach (Node node1 in org.nodes)
                            {
                                double dist = Utility.Distance((MobileNode)node, (MobileNode)node1);
                                if (dist > mindist  && dist < mindist+100 && node.Id != node1.Id)
                                    tmpNodes.Add(node1);
                            }

                            if (tmpNodes.Count > 0)
                                pkg.Dst = tmpNodes[tmpNodes.Count / 2].Id;
                            else
                            {
                                double maxd = 0;
                                int maxnode = -1;
                                double mind = 9999;
                                int minnode = -1;
                                foreach (Node node1 in global.readers)
                                {
                                    double dist = Utility.Distance((MobileNode)node, (MobileNode)node1);
                                    if (dist > mindist)
                                    {
                                        tmpNodes.Add(node1);
                                        if (dist < mind)
                                        {
                                            mind = dist;
                                            minnode = node1.Id;
                                        }
                                    }
                                    else
                                    {
                                        if (dist > maxd)
                                        {
                                            maxd = dist;
                                            maxnode = node1.Id;
                                        }
                                    }
                                }
                                if (tmpNodes.Count == 0)
                                {
                                    Console.WriteLine("[WARNING] reader{0} has no far enough nodes", this);
                                    pkg.Dst = maxnode;
                                }
                                else//大于mindist的最小节点
                                    pkg.Dst = minnode;
                                    //pkg.Dst = tmpNodes[tmpNodes.Count/2].Id;
                            }
                            Console.WriteLine("dst is changed to {0}", pkg.Dst);
                        } 
                        distance = Utility.Distance((MobileNode)node, (MobileNode)Node.getNode(pkg.Dst, NodeType.READER));
                        Console.WriteLine("{0:F4} [SND_DATA] READER{1} sends data to READER{2} with tags {3}, distance: {4}", currentTime, pkg.Src, pkg.Dst, Convert.ToString(pkg.Tags, 2), distance);
                    }
                    node.SendData(pkg);
                    break;
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }
    }
}
