using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    class NodeDist : IComparable
    {
        public MobileNode node;
        double dist;
        bool accept;

        public NodeDist(MobileNode node, double dist, bool accept)
        {
            this.node = node;
            this.dist = dist;
            this.accept = accept;
        }

        public int CompareTo(object obj)
        {
            NodeDist d1 = (NodeDist)obj;
            if (this.accept && !d1.accept)
                return 1;
            else if (!this.accept && d1.accept)
                return -1;
            if (this.dist < d1.dist)
                return -1;
            else if (this.dist > d1.dist)
                return 1;
            else
                return 0;
        }
    }
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
                    ((SWReader)reader).CheckSmallWorldNeighbors(e.Obj);
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
                        int count = 0;
                        while (distance < mindist || distance >mindist+100 && count++ < 20)
                        {
                            int orgId = ((Reader)node).OrgId;
                            Organization org = (Organization)Node.getNode(orgId, NodeType.ORG);
                            HashSet<MobileNode> candidateDstNodes = new HashSet<MobileNode>();
                            List<MobileNode> allAllowNodes = new List<MobileNode>();
                            //找出[mindist-100, mindist+100]的节点
                            List<NodeDist> tmpDists = new List<NodeDist>();
                            foreach (MobileNode node1 in global.readers)
                            {
                                double dist = Utility.Distance((MobileNode)node, (MobileNode)node1);
                                if (dist > mindist && dist < mindist + 100 && node.Id != node1.Id)
                                {
                                    candidateDstNodes.Add(node1);
                                    tmpDists.Add(new NodeDist(node1, dist, ((HFReader)node1).IsAllowedTags(pkg.Tags)));
                                }
                            }
                            if (candidateDstNodes.Count == 0)
                            {
                                Console.WriteLine("[WARNING] reader{0} has no far enough nodes, retry", this);
                                node = global.readers[(int)Utility.U_Rand(global.readerNum)];
                                pkg.Src = node.Id;
                                pkg.Prev = node.Id;
                                distance = Utility.Distance((MobileNode)node, (MobileNode)Node.getNode(pkg.Dst, NodeType.READER));
                                Console.WriteLine("src is changed to {0}", pkg.Src);
                                continue;
                            }
                            NodeDist[] tmpDist1 = (NodeDist[])tmpDists.ToArray();
                            Array.Sort(tmpDist1);

                            //allAllowNodes中的元素顺序为：源节点(1)，候选终结点，中间节点
                            allAllowNodes.Add((MobileNode)node);
                            foreach (NodeDist d in tmpDist1)
                            {
                                allAllowNodes.Add(d.node);
                            }
                            foreach (HFReader node1 in global.readers)
                            {
                                if (!candidateDstNodes.Contains(node1) && node1.IsAllowedTags(pkg.Tags))
                                    allAllowNodes.Add(node1);
                            }
                            Dijkstra di = new Dijkstra(allAllowNodes);
                            int[] allAllowNodePathDist = di.GetAllShortedPaths(0);
                            MobileNode dstNode = null;
                            for (int candidateDstIndex = 1; candidateDstIndex < candidateDstNodes.Count + 1; candidateDstIndex++)
                            {
                                if (allAllowNodePathDist[candidateDstIndex] == Dijkstra.noPath)
                                    continue;
                                dstNode = allAllowNodes[candidateDstIndex];

                                for (int u = 0; u < allAllowNodes.Count; u++)
                                {
                                    if (di.shortestPaths[candidateDstIndex, u] == Dijkstra.noPath)
                                        break;
                                    int x = di.shortestPaths[candidateDstIndex, u];
                                    //如果不是最后一个节点，且该中间节点为其他终结点，则放弃，（取其他的节点即可）
                                    //if (x != dstNode.Id && candidateDstNodes.Contains(global.readers[x]))
                                    /*if(u>9)
                                    {
                                        Console.WriteLine();
                                        dstNode = null;
                                        break;
                                    }*/
                                    Console.Write(allAllowNodes[x] + "->");
                                }

                                if (dstNode != null)
                                {
                                    pkg.Dst = dstNode.Id;
                                    Console.WriteLine("\ndst is changed to {0}", pkg.Dst);
                                    break;
                                }
                            }
                            if (dstNode == null)
                            {
                                Console.WriteLine("No suitable nodes, retry.");
                                node = global.readers[(int)Utility.U_Rand(global.readerNum)];
                                pkg.Src = node.Id;
                                pkg.Prev = node.Id;
                                distance = Utility.Distance((MobileNode)node, (MobileNode)Node.getNode(pkg.Dst, NodeType.READER));
                                Console.WriteLine("\nsrc is changed to {0}", pkg.Src);
                            }
                            else
                                break;
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
