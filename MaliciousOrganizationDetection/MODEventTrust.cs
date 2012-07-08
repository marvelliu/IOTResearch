using System;
using System.Collections.Generic;
using AdHocBaseApp;


namespace MaliciousOrganizationDetection
{
    public enum MODEventType
    {
        //Intermediate node forward data
        //Drop
        NotDropPacket = 0,//Normal event        
        NotDropPacketButNotReceivePacket,
        DropPacketDueToBandwith,
        DropPacketMaliciously,
        COUNT
    }

    public enum MODEventCategoryType
    {
        DropPacket,
        COUNT
    }

    public enum MODDropPacketEventType
    {
        //Intermediate node forward data
        NotDropPacket = 0,//Normal event        
        NotDropPacketButNotReceivePacket,
        DropPacketDueToBandwith,
        DropPacketMaliciously,
        END
    }


    public enum GroupType
    {
        Normal = 0,
        Malicious,
    }

    public enum ReportSupportGroupType
    {
        Support = 0,
        Nonsupport,
    }

    public enum ActionToReport
    {
        Accept = 0,
        Reject,
    }

    [Serializable]
    public class MODEventTrustResult
    {
        public int nodeId;
        public int reportNodeId;
        public string eventIdent;
        public DSClass ds;
        public int app;
        public int totalEventCount;
        public int nodeReportCount;
        public double timeStamp;
        //总体对事件是否真实判断，如果大于0表示认为该事件是恶意.由于初始报告总是报告恶意事件，所以判断总体正常就是对源报告的不支持，反之亦然
        //默认是正常的，即不支持是恶意的
        public int supportDroppingMalicious = -1; 
        public double variance = 0; //其内部的方差
        public double myVarianceDist = 0;//与自己的方差
        public double totalVarianceDist = 0;//与整体报告的方差

        public static int Conv(MODEventType type)
        {
            int t = (int)type;
            //....
            if (t > (int)MODDropPacketEventType.NotDropPacket)
                return t - (int)MODDropPacketEventType.NotDropPacket;
            return -1;
        }

        public static int GetStartTypeByCategory(MODEventCategoryType category)
        {
            switch (category)
            {
                case MODEventCategoryType.DropPacket:
                    return (int)MODEventType.NotDropPacket;
                //TODO others
                default:
                    return -1;

            }
        }

        public static int GetCountByCategory(MODEventCategoryType category)
        {
            switch (category)
            {
                case MODEventCategoryType.DropPacket:
                    return (int)MODDropPacketEventType.END - (int)MODDropPacketEventType.NotDropPacket;
                //TODO others
                default:
                    return -1;
            }
        }


        public static int GetOffsetByCategory(MODEventCategoryType category, MODEventType type)
        {
            switch (category)
            {
                case MODEventCategoryType.DropPacket:
                    return (int)type - (int)MODDropPacketEventType.NotDropPacket;
                //TODO others
                default:
                    return -1;
            }
        }

        public static Dictionary<MODEventType, double> confirmBeliefThrehold;

        public MODEventTrustResult(int node, int reportNodeId, string pkgIdent, MODEventCategoryType category, DSClass ds)
        {
            this.nodeId = node;
            this.reportNodeId = Global.getInstance().readers[reportNodeId].Id;
            this.ds = ds;
            this.eventIdent = pkgIdent;
            //TODO app

            confirmBeliefThrehold = new Dictionary<MODEventType, double>();
            confirmBeliefThrehold[MODEventType.DropPacketMaliciously] = 0.3;
            this.timeStamp = Scheduler.getInstance().currentTime;
        }


        public MODEventTrustResult(int node, Node reportNode, string pkgIdent, MODEventCategoryType category, DSClass ds)
        {
            this.nodeId = node;
            this.reportNodeId = reportNode.Id;
            this.ds = ds;
            this.eventIdent = pkgIdent;
            //TODO app

            confirmBeliefThrehold = new Dictionary<MODEventType, double>();
            confirmBeliefThrehold[MODEventType.DropPacketMaliciously] = 0.3;
            this.timeStamp = Scheduler.getInstance().currentTime;
        }

        public static HashSet<string> DeducedPackets = new HashSet<string>();
    }

    [Serializable]
    class MODEventTrustCategoryResult
    {
        public int node;
        public string categoryIdent;
        public DSClass ds;
        public int app;
        public MODEventCategoryType category;
        public int[] confirmedEventNums;

        public MODEventTrustCategoryResult(int node, int app, MODEventCategoryType category, DSClass ds)
        {
            this.node = node;
            this.ds = ds;
            this.category = category;
            this.categoryIdent = node + "-" + category;
            this.app = app;
            this.confirmedEventNums = new int[(int)MODEventType.COUNT];
            //TODO app
        }
    }


    class MODEventTrust
    {
        static double[] CF = new double[(int)MODEventType.COUNT]{
                            0.9, //NotDropPacket
                            0.5, //NotDropPacketButNotReceivePacket,
                            0.7, //DropPacketDueToBandwith,
                            0.7, //DropPacketMaliciously,
        
                        };

        static MODGlobal global = (MODGlobal)Global.getInstance();


        public delegate bool ComparePhenomemon(MODPhenomemon p1, MODPhenomemon p2);

        public static bool ComparePhenomemonByExactTag(MODPhenomemon p1, MODPhenomemon p2)
        {
            if (p1.pkg == null || p1.pkg.Type != PacketType.COMMAND)
                return false;
            //使用PacketSeq判断两个数据包是否为同一个
            return (p1.pkg.Command.tag == p2.pkg.Dst && p1.pkg.SrcSenderSeq == p2.pkg.SrcSenderSeq);
        }

        public static bool ComparePhenomemonBySimiliarTag(MODPhenomemon p1, MODPhenomemon p2)
        {
            if (p1.pkg == null || p1.pkg.Type != PacketType.COMMAND)
                return false;
            //使用PacketSeq判断两个数据包是否为同一个
            return (p1.pkg.Command.tag == p2.pkg.Dst);
        }

        public static double SimiliarCommand(MODPhenomemon p, List<MODPhenomemon> list)
        {
            double likehood = 0;
            foreach (MODPhenomemon p1 in list)
            {
                likehood = Math.Max(likehood, 1 - global.SmallValue - Math.Abs(p1.pkg.SrcSenderSeq - p.pkg.SrcSenderSeq) / 100);
            }
            return likehood;
        }

        public static MODPhenomemon GetPhenomemon(Packet pkg, int selfId, HashSet<MODPhenomemon> observedPhenomemons)
        {
            MODGlobal global = (MODGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<MODEventTrustResult> list = new List<MODEventTrustResult>();
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.pkg != pkg)
                    continue;
                else if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != MODPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst == p.nodeId)
                    continue;
                else if (p.pkg.Type != PacketType.DATA && p.pkg.Type != PacketType.COMMAND)
                    continue;
                return p;
            }
            return null;
        }


        public static MODPhenomemon GetPhenomemon(string pkgIdent, int selfId, HashSet<MODPhenomemon> observedPhenomemons)
        {

            MODGlobal global = (MODGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<MODEventTrustResult> list = new List<MODEventTrustResult>();
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.type != MODPhenomemonType.RECV_PACKET)
                    continue;
                string pkgIdent1 = GetPacketIdent(p.pkg);

                if (pkgIdent != pkgIdent1)
                    continue;
                else if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != MODPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst == p.nodeId)
                    continue;
                else if (p.pkg.Type != PacketType.DATA && p.pkg.Type != PacketType.COMMAND)
                    continue;
                return p;
            }
            return null;
        }


        public static MODEventTrustResult DeduceDropPacketMaliciouslyByPacket(int selfId, 
            HashSet<MODPhenomemon> observedPhenomemons, double currentTime, MODPhenomemon p)
        {
            double a1, a2, a3, a4, a5, a6, a7, a19, a27;
            if (Global.getInstance().debug)
                Console.WriteLine("READER{0} Check DeduceDropPacketMaliciously", selfId);
            MODGlobal global = (MODGlobal)Global.getInstance();
            MODReader selfNode = (MODReader)global.readers[selfId];

            int node = p.nodeId;

            a1 = p.likehood; //likehood of receiving a packet at time p.start
            a2 = ConditionHappened(observedPhenomemons, MODPhenomemonType.SEND_PACKET, node, p.start, p.start + global.sendPacketTimeout, p.pkg);
            a3 = ConditionHappened(observedPhenomemons, MODPhenomemonType.NOT_SEND_PACKET, node, p.start, Scheduler.getInstance().currentTime, p.pkg);
            a4 = ConditionHappened(observedPhenomemons, MODPhenomemonType.BANDWIDTH_BUSY, selfId, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
            //如果对带宽占用没有知识，则正反都设置为未知。
            a5 = 0.9 - a4;
            a6 = Utility.Average(new double[]{
                    ConditionHappened(observedPhenomemons, MODPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime),
                    ConditionHappened(observedPhenomemons, MODPhenomemonType.MOVE_FAST, p.pkg.Prev, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime),
                    ConditionHappened(observedPhenomemons, MODPhenomemonType.MOVE_FAST, selfId, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime)
                });
            a7 = 0.9 - a6;
            //一个是观测节点和被观测节点的距离，看是否后者发送的消息能否被前者收到
            //另一个是，看源节点发送的数据是否能被被观测节点收到
            //a19 = Math.Max(FarDistanceLikehood(selfId, node, p.pkg.DstType==NodeType.OBJECT),
            //    FarDistanceLikehood(selfId, p.pkg.Prev, p.pkg.PrevType == NodeType.OBJECT));


            if (p.pkg.DstType == NodeType.OBJECT && selfNode.NearbyObjectCache.ContainsKey(p.pkg.Dst))
                a19 = FarDistanceLikehood(selfId, node, true);
            else
                a19 = FarDistanceLikehood(selfId, node, false);
            a27 = 0.9 - a19;


            //A1 AND A2 AND A7 AND A11 -> B1 
            double b0 = DSClass.AND(a1, a2) * CF[(int)MODEventType.NotDropPacket];
            //A1 AND A2 AND A7 AND A11 -> B1
            double b1 = DSClass.AND(DSClass.AND(a1, a3), DSClass.OR(DSClass.OR(a4, a6), a19)) * CF[(int)MODEventType.NotDropPacketButNotReceivePacket];
            //A1 AND A2 AND A7 AND A11 -> B1
            double b2 = DSClass.AND(DSClass.AND(a1, a3), a4) * CF[(int)MODEventType.DropPacketDueToBandwith];
            //A1 AND A2 AND A7 AND A11 -> B1
            double b3 = DSClass.AND(DSClass.AND(DSClass.AND(DSClass.AND(a1, a3), a5), a7), a27) * CF[(int)MODEventType.DropPacketMaliciously];

            /*
            if (global.debug)
            {
                Console.WriteLine("{0}->{1}:{2}", selfId, node, p.pkg.SrcSenderSeq);
                Console.WriteLine("a1:" + a1);
                Console.WriteLine("a2:" + a2);
                Console.WriteLine("a3:" + a3);
                Console.WriteLine("a4:" + a4);
                Console.WriteLine("a5:" + a5);
                Console.WriteLine("a6:" + a6);
                Console.WriteLine("a7:" + a7);
                Console.WriteLine("a19:" + a19);
                Console.WriteLine("a27:" + a27);
                Console.WriteLine("b0:" + b0);
                Console.WriteLine("b1:" + b1);
                Console.WriteLine("b2:" + b2);
                Console.WriteLine("b3:" + b3);
            }*/

            DSClass ds = new DSClass(pow(MODDropPacketEventType.END));
            ds.SetM(pow(MODDropPacketEventType.NotDropPacket), b0);
            ds.SetM(pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
            ds.SetM(pow(MODDropPacketEventType.DropPacketDueToBandwith), b2);
            ds.SetM(pow(MODDropPacketEventType.DropPacketMaliciously), b3);
            ds.SetM(pow(MODDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
            ds.Cal();
            //ds.Output();
            string pkgIdent = GetPacketIdent(p.pkg);
            MODEventTrustResult r = new MODEventTrustResult(node, global.readers[selfId], pkgIdent, MODEventCategoryType.DropPacket, ds);

            //此处，我们先过滤一些正常事件，否则事件太多了
            if (ds.b[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)] < global.NormalBelief
                && ds.p[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
            {
                Console.WriteLine("{0:F4} reader{1} think reader{2} is not normal.", currentTime, selfId, p.nodeId);
                r.supportDroppingMalicious = 1;
                /*
                if (global.debug)
                    r.ds.Output();
                 * */
            }
            else
                r.supportDroppingMalicious = -1;
            return r;
        }

        static List<MODEventTrustResult> DeduceDropPacketMaliciously(int selfId, HashSet<MODPhenomemon> observedPhenomemons, double currentTime)
        {
            if (Global.getInstance().debug)
                Console.WriteLine("READER{0} Check DeduceDropPacketMaliciously", selfId);
            MODGlobal global = (MODGlobal)Global.getInstance();
            MODReader selfNode = (MODReader)global.readers[selfId];
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<MODEventTrustResult> list = new List<MODEventTrustResult>();
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                double a1, a2, a3, a4, a5, a6, a7, a19, a27;

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != MODPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst == p.nodeId)
                    continue;
                else if (p.pkg.Type != PacketType.DATA && p.pkg.Type != PacketType.COMMAND)
                    continue;
                else if (selfId == p.nodeId)//自己不检查自己的可信度
                    continue;
                else if (global.readers[p.nodeId].IsGateway)
                    continue;
                else if (currentTime - p.start < global.checkPhenomemonTimeout)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;

                a1 = p.likehood; //likehood of receiving a packet at time p.start
                a2 = ConditionHappened(observedPhenomemons, MODPhenomemonType.SEND_PACKET, node, p.start, p.start + global.sendPacketTimeout, p.pkg);
                a3 = ConditionHappened(observedPhenomemons, MODPhenomemonType.NOT_SEND_PACKET, node, p.start, Scheduler.getInstance().currentTime, p.pkg);
                a4 = ConditionHappened(observedPhenomemons, MODPhenomemonType.BANDWIDTH_BUSY, selfId, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
                //如果对带宽占用没有知识，则正反都设置为未知。
                a5 = 0.9 - a4;
                a6 = Utility.Max(new double[]{
                    ConditionHappened(observedPhenomemons, MODPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime),
                    ConditionHappened(observedPhenomemons, MODPhenomemonType.MOVE_FAST, p.pkg.Prev, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime),
                    ConditionHappened(observedPhenomemons, MODPhenomemonType.MOVE_FAST, selfId, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime)
                });
                a7 = 0.9 - a6;
                //一个是观测节点和被观测节点的距离，看是否后者发送的消息能否被前者收到
                //另一个是，看源节点发送的数据是否能被被观测节点收到
                //a19 = Math.Max(FarDistanceLikehood(selfId, node, p.pkg.DstType==NodeType.OBJECT),
                //    FarDistanceLikehood(selfId, p.pkg.Prev, p.pkg.PrevType == NodeType.OBJECT));
                if (p.pkg.DstType == NodeType.OBJECT && selfNode.NearbyObjectCache.ContainsKey(p.pkg.Dst))
                    a19 = FarDistanceLikehood(selfId, node, true);
                else
                    a19 = FarDistanceLikehood(selfId, node, false);
                a27 = 0.9 - a19;


                //A1 AND A2 AND A7 AND A11 -> B1 
                double b0 = DSClass.AND(a1, a2) * CF[(int)MODEventType.NotDropPacket];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b1 = DSClass.AND(DSClass.AND(a1, a3), DSClass.OR(DSClass.OR(a4, a6), a19)) * CF[(int)MODEventType.NotDropPacketButNotReceivePacket];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b2 = DSClass.AND(DSClass.AND(a1, a3), a4) * CF[(int)MODEventType.DropPacketDueToBandwith];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b3 = DSClass.AND(DSClass.AND(DSClass.AND(DSClass.AND(a1, a3), a5), a7), a27) * CF[(int)MODEventType.DropPacketMaliciously];

                if (global.debug)
                {
                    Console.WriteLine("{0}->{1}:{2}", selfId, node, p.pkg.SrcSenderSeq);
                    Console.WriteLine("a1:" + a1);
                    Console.WriteLine("a2:" + a2);
                    Console.WriteLine("a3:" + a3);
                    Console.WriteLine("a4:" + a4);
                    Console.WriteLine("a5:" + a5);
                    Console.WriteLine("a6:" + a6);
                    Console.WriteLine("a7:" + a7);
                    Console.WriteLine("a19:" + a19);
                    Console.WriteLine("a27:" + a27);
                    Console.WriteLine("b0:" + b0);
                    Console.WriteLine("b1:" + b1);
                    Console.WriteLine("b2:" + b2);
                    Console.WriteLine("b3:" + b3);
                }

                DSClass ds = new DSClass(pow(MODDropPacketEventType.END));
                ds.SetM(pow(MODDropPacketEventType.NotDropPacket), b0);
                ds.SetM(pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
                ds.SetM(pow(MODDropPacketEventType.DropPacketDueToBandwith), b2);
                ds.SetM(pow(MODDropPacketEventType.DropPacketMaliciously), b3);
                ds.SetM(pow(MODDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
                ds.Cal();
                //ds.Output();
                //此处，我们先过滤一些正常事件，否则事件太多了
                if (ds.b[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)] < global.NormalBelief
                    && ds.p[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = GetPacketIdent(p.pkg);
                    MODEventTrustResult r = new MODEventTrustResult(node, global.readers[selfId], pkgIdent, MODEventCategoryType.DropPacket, ds);
                    r.totalEventCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                    Console.WriteLine("{0:F4} reader{1} think reader{2} is not normal.", currentTime, selfId, p.nodeId);
                    if (global.debug)
                    {
                        r.ds.Output();
                    }
                }
            }
            return list;
        }



        /*
         * 在普通节点一级，推导出各种事件，事件根据数据包划分，一个事件为一个类型。
         */
        public static List<MODEventTrustResult> DeduceAllEventTrusts(int selfId, HashSet<MODPhenomemon> observedPhenomemons, double currentTime)
        {
            List<MODEventTrustResult> list = new List<MODEventTrustResult>();
            list.AddRange(DeduceDropPacketMaliciously(selfId, observedPhenomemons, currentTime));
            //调整每个事件的因子,如果有的因子为0，则调为0.001，防止d-s的冲突
            for (int i = 0; i < list.Count; i++)
            {
                MODEventTrustResult result = list[i];
                int node = result.nodeId;
                result.ds.Normalize();
            }
            return list;
        }


        static double ConditionHappened(HashSet<MODPhenomemon> observedPhenomemons, MODPhenomemonType type, int node)
        {
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type)
                    return p.likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(HashSet<MODPhenomemon> observedPhenomemons, MODPhenomemonType type, int node, double starttime, double endtime)
        {
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type
                    && p.start >= starttime && p.end <= endtime)
                    return p.likehood;
            }
            if (type == MODPhenomemonType.DIST_FAR)//如果计算节点距离的可能性，且不在邻居中，则说明该节点很远，可能性为很大
                return 0.8;
            else
                return global.SmallValue;
        }


        static double ConditionHappened(HashSet<MODPhenomemon> observedPhenomemons, MODPhenomemonType type, int node, double time)
        {
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type && Utility.DoubleEqual(p.start, time))
                    return p.likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(HashSet<MODPhenomemon> observedPhenomemons, MODPhenomemonType type, int node, double time, Packet pkg)
        {
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type
                    && Utility.DoubleEqual(p.start, time) && Packet.IsSamePacket(p.pkg, pkg))
                    return p.likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(HashSet<MODPhenomemon> observedPhenomemons, MODPhenomemonType type, int node, double starttime, double endtime, Packet pkg)
        {
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type
                    && p.start >= starttime && p.end <= endtime
                    && Packet.IsSamePacket(p.pkg, pkg))
                    return p.likehood;
            }
            return global.SmallValue;
        }

        static double ConditionHappened(HashSet<MODPhenomemon> observedPhenomemons, MODPhenomemonType type, int node,
            double starttime, double endtime, MODPhenomemon p1, List<MODPhenomemon> list, ComparePhenomemon comparer)
        {
            double likehood = global.SmallValue;
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type
                    && p.start >= starttime && p.end <= endtime
                    && comparer(p1, p))
                {
                    list.Add(p);
                    likehood = Math.Max(p.likehood, likehood);
                }
            }
            return likehood;
        }


        static double ConditionHappened(HashSet<MODPhenomemon> observedPhenomemons, MODPhenomemonType type, int node,
            double starttime, double endtime, MODPhenomemon p1, ComparePhenomemon comparer)
        {
            foreach (MODPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type
                    && p.start >= starttime && p.end <= endtime
                    && comparer(p1, p))
                {
                    return p.likehood;
                }
            }
            return global.SmallValue;
        }

        static double FarDistanceLikehood(int n1, int n2, bool distIsTag)
        {
            double dist = Utility.Distance(global.readers[n1], global.readers[n2]);
            double likehood = 0;

            if (distIsTag)
            {
                if (dist > global.objectMaxDist)
                    return 0.6;
            }
            else
            {
                if (dist > global.nodeMaxDist)
                    return 0.6;
            }

            //likehood = 0.6/global.nodeMaxDist * dist + 0.2;
            likehood = 0.3;

            return likehood;
        }

        //基准速度，考察该速度下可能判断失误
        static public double baseSpeed = 10;

        //如果本节点是其邻居，那么判断没有收到所有数据包的情况
        public static MODEventTrustResult NotObservedEventTrustResult(int suspectedNode, int reportNode, string pkgIdent,
            MODEventCategoryType category, double[] speeds, bool[] isNeighbors)
        {
            //可能存在其他误差，设一个较小的值
            double b0 = 0.002, b1 = 0.002, b2 = 0.002, b3 = 0.002;

            if (!isNeighbors[0] && !isNeighbors[1])
            {
                b0 = b1 = b2 = b3 = 0.1;
            }
            else if (isNeighbors[0] && !isNeighbors[1])//prev是nb，sus不是nb
            {
                b0 = b1 = b2 = b3 = 0.1;
                if (speeds[0] > 4) //与速度有关
                {
                    b1 = Math.Min(0.3 * speeds[0] / baseSpeed, 0.6);
                    b3 = Math.Min(0.3 * speeds[0] / baseSpeed, 0.6);
                }
            }
            else if (!isNeighbors[0] && isNeighbors[1])//prev不是nb，sus是nb
            {
                b0 = b1 = b2 = b3 = 0.1;
                if (speeds[0] + speeds[2] > 4) //与速度有关
                {
                    b1 = Math.Min(0.3 * (speeds[0] + speeds[2]) / baseSpeed, 0.6);
                    b3 = Math.Min(0.3 * (speeds[0] + speeds[2]) / baseSpeed, 0.6);
                }
            }
            else//均为邻居
            {
                b0 = b1 = b2 = b3 = 0.1;
                if (speeds[0] + speeds[1] + speeds[2] > 4)
                { //与速度有关
                    b1 = Math.Min(0.3 * (speeds[0] + speeds[1] + speeds[2]) / baseSpeed, 0.6);
                    b3 = Math.Min(0.3 * (speeds[0] + speeds[1] + speeds[2]) / baseSpeed, 0.6);
                }
            }

            double total = b0 + b1 + b2 + b3 + 0.3;            
            b0 = b0 / total;
            b1 = b1 / total;
            b2 = b2 / total;
            b3 = b3 / total;


            DSClass ds = new DSClass(pow(MODDropPacketEventType.END));
            ds.SetM(pow(MODDropPacketEventType.NotDropPacket), b0);
            ds.SetM(pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
            ds.SetM(pow(MODDropPacketEventType.DropPacketDueToBandwith), b2);
            ds.SetM(pow(MODDropPacketEventType.DropPacketMaliciously), b3);
            ds.SetM(pow(MODDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
            ds.Cal();

            MODEventTrustResult r = new MODEventTrustResult(suspectedNode, reportNode, pkgIdent, category, ds);
            if (ds.b[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)] < global.NormalBelief
                && ds.p[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
            {
                r.supportDroppingMalicious = 1;
            }
            else
                r.supportDroppingMalicious = -1;
            return r;
        }

        public static double getDist(DSClass ds)
        {
            double x1 = ds.b[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)+
                pow(MODEventType.DropPacketDueToBandwith)];
            double x2 = ds.b[pow(MODEventType.DropPacketMaliciously)];
            return Math.Abs(x1 - x2);
        }


        public static MODEventTrustResult ForgeMaliciousEventTrustResult(int node, int reportNode, string pkgIdent, MODEventCategoryType category)
        {
            double b0 = 0.1;
            double b1 = 0.1;
            double b2 = 0.1;
            double b3 = 0.7;

            DSClass ds = new DSClass(pow(MODDropPacketEventType.END));
            ds.SetM(pow(MODDropPacketEventType.NotDropPacket), b0);
            ds.SetM(pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
            ds.SetM(pow(MODDropPacketEventType.DropPacketDueToBandwith), b2);
            ds.SetM(pow(MODDropPacketEventType.DropPacketMaliciously), b3);
            ds.SetM(pow(MODDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
            ds.Cal();

            MODEventTrustResult result = new MODEventTrustResult(node, reportNode, pkgIdent, category, ds);
            result.supportDroppingMalicious = 1;
            return result;
        }

        public static MODEventTrustResult ForgeNormalEventTrustResult(int node, int reportNode, string pkgIdent, MODEventCategoryType category)
        {
            double b0 = 0.7;
            double b1 = 0.1;
            double b2 = 0.1;
            double b3 = 0.1;

            DSClass ds = new DSClass(pow(MODDropPacketEventType.END));
            ds.SetM(pow(MODDropPacketEventType.NotDropPacket), b0);
            ds.SetM(pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
            ds.SetM(pow(MODDropPacketEventType.DropPacketDueToBandwith), b2);
            ds.SetM(pow(MODDropPacketEventType.DropPacketMaliciously), b3);
            ds.SetM(pow(MODDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
            ds.Cal();

            MODEventTrustResult result = new MODEventTrustResult(node, reportNode, pkgIdent, category, ds);
            result.supportDroppingMalicious = -1;
            return result;
        }

        public static MODEventTrustResult MergeMaliciousEventTrustResult(int node, Node reportNode, List<MODEventTrustResult> reports, 
            string pkgIdent, MODEventCategoryType category)
        {
            double b0 = 0.0;
            double b1 = 0.0;
            double b2 = 0.0;
            double b3 = 0.0;

            //各个b的方差
            double x0 = 0, x1 = 0, x2 = 0, x3 = 0;

            DSClass ds = new DSClass(pow(MODDropPacketEventType.END));


            //TODO，确认下面是否是正确的
            for (int i = 0; i < reports.Count; i++)
            {
                b0 += reports[i].ds.b[pow(MODDropPacketEventType.NotDropPacket)];
                b1 += reports[i].ds.b[pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket)];
                b2 += reports[i].ds.b[pow(MODDropPacketEventType.DropPacketDueToBandwith)];
                b3 += reports[i].ds.b[pow(MODDropPacketEventType.DropPacketMaliciously)];
            }
            b0 = b0 / reports.Count;
            b1 = b1 / reports.Count;
            b2 = b2 / reports.Count;
            b3 = b3 / reports.Count;

            ds.SetM(pow(MODDropPacketEventType.NotDropPacket), b0);
            ds.SetM(pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
            ds.SetM(pow(MODDropPacketEventType.DropPacketDueToBandwith), b2);
            ds.SetM(pow(MODDropPacketEventType.DropPacketMaliciously), b3);
            ds.SetM(pow(MODDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
            ds.Cal();

            //计算方差，即一致度
            //TODO，确认下面是否是正确的
            for (int i = 0; i < reports.Count; i++)
            {
                x0 += Math.Pow(b0 - reports[i].ds.b[pow(MODDropPacketEventType.NotDropPacket)], 2);
                x1 += Math.Pow(b1 - reports[i].ds.b[pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket)], 2);
                x2 += Math.Pow(b2 - reports[i].ds.b[pow(MODDropPacketEventType.DropPacketDueToBandwith)], 2);
                x3 += Math.Pow(b3 - reports[i].ds.b[pow(MODDropPacketEventType.DropPacketMaliciously)], 2);
            }

            MODEventTrustResult result = new MODEventTrustResult(node, reportNode, pkgIdent, category, ds);
            result.variance = x0 + x1 + x2 + x3;

            if (result.ds.b[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)] < global.NormalBelief
                && result.ds.p[pow(MODEventType.NotDropPacket) + pow(MODEventType.NotDropPacketButNotReceivePacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                result.supportDroppingMalicious = 1;
            else
                result.supportDroppingMalicious = -1;
            return result;
        }

        //与自己预测的相关度
        public static double CalculateRelativeMaliciousEventTrustResult(List<MODEventTrustResult> reports)
        {
            double abx1 = 0, abx2 = 0;
            for (int i = 0; i < reports.Count; i++)
            {
                abx1 += reports[i].ds.b[pow(MODDropPacketEventType.NotDropPacket) + pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket)
                + pow(MODDropPacketEventType.DropPacketDueToBandwith)];
                abx2 += reports[i].ds.b[pow(MODDropPacketEventType.DropPacketMaliciously)];
            }
            abx1 /=  reports.Count;
            abx2 /=  reports.Count;



            //各个b的方差
            //正常的方差和恶意的方差：
            double x1 = 0, x2 = 0;

            for (int i = 0; i < reports.Count; i++)
            {
                double bx1 = reports[i].ds.b[pow(MODDropPacketEventType.NotDropPacket) + pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket)
                   + pow(MODDropPacketEventType.DropPacketDueToBandwith)];
                double bx2 = reports[i].ds.b[pow(MODDropPacketEventType.DropPacketMaliciously)];
                x1 += Math.Pow(abx1 - bx1, 2);
                x2 += Math.Pow(abx1 - bx2, 2);
            }

            return x1 + x2;
        }


        //与自己预测的相关度
        public static double CalculateRelativeMaliciousEventTrustResult(MODEventTrustResult r, MODEventTrustResult myreport)
        {
            double rb = r.ds.b[pow(MODDropPacketEventType.NotDropPacket)+pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket)
                +pow(MODDropPacketEventType.DropPacketDueToBandwith)];
            double mb = myreport.ds.b[pow(MODDropPacketEventType.NotDropPacket)+pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket)
                +pow(MODDropPacketEventType.DropPacketDueToBandwith)];

            double b1 = (rb + mb) / 2;

            double b3 = r.ds.b[pow(MODDropPacketEventType.DropPacketMaliciously)] + myreport.ds.b[pow(MODDropPacketEventType.DropPacketMaliciously)];
            b3 = b3 / 2;


            //各个b的方差
            //正常的方差和恶意的方差：
            double x1 = Math.Pow(b1 - rb, 2) + Math.Pow(b1 - mb, 2);
            double x2 = Math.Pow(b3 - r.ds.b[pow(MODDropPacketEventType.DropPacketMaliciously)], 2) + Math.Pow(b3 - myreport.ds.b[pow(MODDropPacketEventType.DropPacketMaliciously)], 2);
            /*
            DSClass ds = new DSClass(pow(MODDropPacketEventType.END));
            
            ds.SetM(pow(MODDropPacketEventType.NotDropPacket), b0);
            ds.SetM(pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
            ds.SetM(pow(MODDropPacketEventType.DropPacketDueToBandwith), b2);
            ds.SetM(pow(MODDropPacketEventType.DropPacketMaliciously), b3);
            ds.SetM(pow(MODDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
            ds.Cal();*/

            //x0 = Math.Pow(b0 - r.ds.b[pow(MODDropPacketEventType.NotDropPacket)], 2) + Math.Pow(b0 - myreport.ds.b[pow(MODDropPacketEventType.NotDropPacket)], 2);
            //x1 = Math.Pow(b1 - r.ds.b[pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket)], 2) + Math.Pow(b1 - myreport.ds.b[pow(MODDropPacketEventType.NotDropPacketButNotReceivePacket)], 2);
            //x2 = Math.Pow(b2 - r.ds.b[pow(MODDropPacketEventType.DropPacketDueToBandwith)], 2) + Math.Pow(b2 - myreport.ds.b[pow(MODDropPacketEventType.DropPacketDueToBandwith)], 2);
            

            
            //return x0 + x1 + x2 + x3;
            return x1 + x2;
        }

        //恶意节点的收益有两部分，一部分是信誉值的变化(+0.2,-0.3)，另一部分是恶意行为所带来的收益(0.5,0)
        public static double A1Fun(bool Malicious, bool ISupportM, bool isCompositeReportSupportM, bool Accept)
        {
            double reputation = 0, gain = 0;
            if (ISupportM == isCompositeReportSupportM ^ Accept)
                reputation = -0.3;
            else
                reputation = 0.2;

            if (ISupportM == true)
                gain = 0.3;
            else
                gain = 0.0;

            double  result = reputation+gain;
            return result;

        }

        //两个结果的差别
        public static double Distance(MODEventTrustResult r1, MODEventTrustResult r2)
        {
            return Math.Pow(r1.ds.b[0] - r2.ds.b[0], 2)
                + Math.Pow(r1.ds.b[1] - r2.ds.b[1], 2)
                + Math.Pow(r1.ds.b[2] - r2.ds.b[2], 2)
                + Math.Pow(r1.ds.b[3] - r2.ds.b[3], 2);
        }

        static int pow(MODDropPacketEventType t)
        {
            return DSClass.pow((int)t - (int)MODDropPacketEventType.NotDropPacket);
        }

        public static int pow(MODEventType type)
        {
            return DSClass.pow((int)type);
        }

        static double Inverse(double v)
        {
            if (Utility.DoubleEqual(v, global.SmallValue))
                return global.SmallValue;
            else
                return 0.9 - v;
        }

        public static string GetPacketIdent(Packet pkg)
        {
            return pkg.Prev + "->" + pkg.Next + "(" + pkg.PrevSenderSeq + ")";
        }


    }
}

