using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.Collections;


namespace TrustArch
{
    public enum IOTEventType
    {
        //Intermediate node forward data
        //Drop
        NotDropPacket = 0,//Normal event        
        NotDropPacketButNotReceivePacket,
        DropPacketDueToBandwith,
        DropPacketMaliciously,
        //Modify
        NotModifyPacket,
        NotModifyPacketButNetworkFaulty,
        ModifyPacketDueToNodeFaulty,
        ModifyPacketMaliciously,
        //Redirect or forge packet
        NotMakePacket,
        MakePacketMaliciously,
        
        //Intermediate node topology information
        BadTopologyMaliciously,
        BadTopologyDueToMove,
        BadTopologyDueToNetwork,

        //Terminal node
        NotDropCommand,
        NotDropCommandButNotDetected,
        DropCommandMaliciously,

        NotModifyCommand,
        ModifyCommandDueToNodeFaulty,
        ModifyCommandMaliciously,

        NotMakeCommand,
        NotMakeCommandButMove,
        NotMakeCommandButNetworkDelay,
        MakeCommandMaliciously,

        //Misc event
        BadDeclaredRegionInfo,
        CorrectDeclaredRegionInfo,
        COUNT
    }

    public enum IOTEventCategoryType
    {
        DropPacket,
        ModifyPacket,
        MakePacket,
        Topology,
        ModifyCommand,
        DropCommand,
        MakeCommand,
        RedirectTagData,
        ModifyTagData,
        DeclaredRegionInfo,
        COUNT
    }

    public enum IOTDropPacketEventType
    {
        //Intermediate node forward data
        NotDropPacket = 0,//Normal event        
        NotDropPacketButNotReceivePacket,
        DropPacketDueToBandwith,
        DropPacketMaliciously,
        END
    }

    public enum IOTModifyPacketEventType
    {
        //Intermediate node forward data
        NotModifyPacket = IOTDropPacketEventType.END,
        //Modify
        NotModifyPacketButNetworkFaulty,
        ModifyPacketDueToNodeFaulty,
        ModifyPacketMaliciously,
        END
    }


    public enum IOTMakePacketEventType
    {
        NotMakePacket = IOTModifyPacketEventType.END,
        //MakePacket
        MakePacketMaliciously,
        END
    }


    public enum IOTDropCommandEventType
    {
        NotDropCommand,
        NotDropCommandButNotDetected,
        DropCommandMaliciously,
        END
    }
    public enum IOTModifyCommandEventType
    {
        NotModifyCommand,
        ModifyCommandDueToNodeFaulty,
        ModifyCommandMaliciously,
        END
    }

    public enum IOTMakeCommandEventType
    {
        NotMakeCommand,
        NotMakeCommandButMove,
        NotMakeCommandButNetworkDelay,
        MakeCommandMaliciously,
        END
    }
    

    [Serializable]
    public class IOTEventTrustResult
    {
        public int node;
        public int reportNode;
        public string eventIdent;
        public DSClass ds;
        public int app;
        public IOTEventCategoryType category;
        public int totalEventCount;
        public int nodeReportCount;

        public static int Conv(IOTEventType type)
        {
            int t = (int)type;
            //....
            if (t > (int)IOTModifyPacketEventType.NotModifyPacket)
                return t - (int)IOTModifyPacketEventType.NotModifyPacket;
            else if(t>(int)IOTDropPacketEventType.NotDropPacket)
                return t - (int)IOTDropPacketEventType.NotDropPacket;
            return -1;
        }

        public static int GetStartTypeByCategory(IOTEventCategoryType category)
        {
            switch (category)
            {
                case IOTEventCategoryType.DropPacket:
                    return (int)IOTEventType.NotDropPacket;
                case IOTEventCategoryType.ModifyPacket:
                    return (int)IOTEventType.NotModifyPacket;
                case IOTEventCategoryType.MakePacket:
                    return (int)IOTEventType.NotMakePacket;
                case IOTEventCategoryType.DropCommand:
                    return (int)IOTEventType.NotDropCommand;
                case IOTEventCategoryType.ModifyCommand:
                    return (int)IOTEventType.NotModifyCommand;
                case IOTEventCategoryType.MakeCommand:
                    return (int)IOTEventType.NotMakeCommand;
                //TODO others
                default:
                    return -1;
                    
            }
        }

        public static int GetCountByCategory(IOTEventCategoryType category)
        {
            switch (category)
            {
                case IOTEventCategoryType.DropPacket:
                    return (int)IOTDropPacketEventType.END - (int)IOTDropPacketEventType.NotDropPacket;
                case IOTEventCategoryType.ModifyPacket:
                    return (int)IOTModifyPacketEventType.END - (int)IOTModifyPacketEventType.NotModifyPacket;
                case IOTEventCategoryType.MakePacket:
                    return (int)IOTMakePacketEventType.END - (int)IOTMakePacketEventType.NotMakePacket;
                case IOTEventCategoryType.DropCommand:
                    return (int)IOTDropCommandEventType.END - (int)IOTDropCommandEventType.NotDropCommand;
                case IOTEventCategoryType.ModifyCommand:
                    return (int)IOTModifyCommandEventType.END - (int)IOTModifyCommandEventType.NotModifyCommand;
                case IOTEventCategoryType.MakeCommand:
                    return (int)IOTMakeCommandEventType.END - (int)IOTMakeCommandEventType.NotMakeCommand;
                //TODO others
                default:
                    return -1;
            }
        }


        public static int GetOffsetByCategory(IOTEventCategoryType category, IOTEventType type)
        {
            switch (category)
            {
                case IOTEventCategoryType.DropPacket:
                    return (int)type - (int)IOTDropPacketEventType.NotDropPacket;
                case IOTEventCategoryType.ModifyPacket:
                    return (int)type - (int)IOTModifyPacketEventType.NotModifyPacket;
                case IOTEventCategoryType.MakePacket:
                    return (int)type - (int)IOTMakePacketEventType.NotMakePacket;
                case IOTEventCategoryType.DropCommand:
                    return (int)type - (int)IOTDropCommandEventType.NotDropCommand;
                case IOTEventCategoryType.ModifyCommand:
                    return (int)type - (int)IOTModifyCommandEventType.NotModifyCommand;
                case IOTEventCategoryType.MakeCommand:
                    return (int)type - (int)IOTMakeCommandEventType.NotMakeCommand;
                //TODO others
                default:
                    return -1;
            }
        }

        public static Dictionary<IOTEventType, double> confirmBeliefThrehold;

        public IOTEventTrustResult(int node, int reportNode, string pkgIdent, IOTEventCategoryType category, DSClass ds)
        {
            this.node = node;
            this.reportNode = reportNode;
            this.ds = ds;
            this.category = category;
            this.eventIdent = pkgIdent+"-"+category;
            //TODO app

            confirmBeliefThrehold = new Dictionary<IOTEventType, double>();
            confirmBeliefThrehold[IOTEventType.DropPacketMaliciously] = 0.3;
            confirmBeliefThrehold[IOTEventType.DropCommandMaliciously] = 0.3;
        }
    }


    [Serializable]
    class IOTEventTrustCategoryResult
    {
        public int node;
        public string categoryIdent;
        public DSClass ds;
        public int app;
        public IOTEventCategoryType category;
        public int[] confirmedEventNums;
        public int totalEventCount;

        public IOTEventTrustCategoryResult(int node, int app, IOTEventCategoryType category, DSClass ds)
        {
            this.node = node;
            this.ds = ds;
            this.category = category;
            this.categoryIdent = node + "-" + category;
            this.app = app;
            this.confirmedEventNums = new int[(int)IOTEventType.COUNT];
            //TODO app
        }
    }


    class IOTEventTrust
    {
        static double[] CF = new double[(int)IOTEventType.COUNT]{
                0.9, //NotDropPacket
                0.5, //NotDropPacketButNotReceivePacket,
                0.7, //DropPacketDueToBandwith,
                0.7, //DropPacketMaliciously,
        
                0.9,//NotModifyPacket,
                0.9,//NotModifyPacketButNetworkFaulty,
                0.9,//ModifyPacketDueToNodeFaulty,
                0.9,//ModifyPacketMaliciously,
        
                0.9,//NotMakePacket,
                0.9,//MakePacketMaliciously,
        
                0.9,//BadTopologyMaliciously,
                0.9,//BadTopologyDueToMove,
                0.9,//BadTopologyDueToNetwork,

                0.9,//NotDropCommand,
                0.9,//NotDropCommandButNotDetected,
                0.9,//DropCommandMaliciously,

                0.9,//NotModifyCommand,
                0.9,//ModifyCommandDueToNodeFaulty,
                0.9,//ModifyCommandMaliciously,

                0.9,//NotMakeCommand,
                0.9,//NotMakeCommandButMove,
                0.9,//NotMakeCommandButNetworkDelay,
                0.9,//MakeCommandMaliciously,

                0.9,//BadDeclaredRegionInfo,
                0.9,//CorrectDeclaredRegionInfo,
        };

        static IOTGlobal global = (IOTGlobal)Global.getInstance();


        public delegate bool ComparePhenomemon(IOTPhenomemon p1, IOTPhenomemon p2);

        public static bool ComparePhenomemonByExactTag(IOTPhenomemon p1, IOTPhenomemon p2)
        {
            if (p1.pkg == null || p1.pkg.Type != PacketType.COMMAND)
                return false;
            //使用PacketSeq判断两个数据包是否为同一个
            return (p1.pkg.Command.tag == p2.pkg.Dst && p1.pkg.SrcSenderSeq == p2.pkg.SrcSenderSeq);
        }

        public static bool ComparePhenomemonBySimiliarTag(IOTPhenomemon p1, IOTPhenomemon p2)
        {
            if (p1.pkg == null || p1.pkg.Type != PacketType.COMMAND)
                return false;
            //使用PacketSeq判断两个数据包是否为同一个
            return (p1.pkg.Command.tag == p2.pkg.Dst);
        }

        public static double SimiliarCommand(IOTPhenomemon p, List<IOTPhenomemon> list)
        {
            double likehood = 0;
            foreach (IOTPhenomemon p1 in list)
            {
                likehood = Math.Max(likehood, 1 - global.SmallValue - Math.Abs(p1.pkg.SrcSenderSeq - p.pkg.SrcSenderSeq) / 100);
            }
            return likehood;
        }

        static List<IOTEventTrustResult> DeduceDropPacketMaliciously(int selfId, HashSet<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            if(Global.getInstance().debug)
                Console.WriteLine("READER{0} Check DeduceDropPacketMaliciously", selfId);
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            IOTReader selfNode = (IOTReader)global.readers[selfId];
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                double a1, a2, a3, a4, a5, a6, a7, a19, a27;

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
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
                a2 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_PACKET, node, p.start, p.start + global.sendPacketTimeout, p.pkg);
                a3 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.NOT_SEND_PACKET, node, p.start, Scheduler.getInstance().currentTime, p.pkg);
                a4 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.BANDWIDTH_BUSY, selfId, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
                //如果对带宽占用没有知识，则正反都设置为未知。
                a5 = 0.9 - a4;
                a6 = Utility.Max(new double[]{
                    ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime),
                    ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, p.pkg.Prev, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime),
                    ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, selfId, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime)
                });
                a7 = 0.9 - a6;
                //一个是观测节点和被观测节点的距离，看是否后者发送的消息能否被前者收到
                //另一个是，看源节点发送的数据是否能被被观测节点收到
                //a19 = Math.Max(FarDistanceLikehood(selfId, node, p.pkg.DstType==NodeType.OBJECT),
                //    FarDistanceLikehood(selfId, p.pkg.Prev, p.pkg.PrevType == NodeType.OBJECT));
                if(p.pkg.DstType == NodeType.OBJECT && selfNode.NearbyObjectCache.ContainsKey(p.pkg.Dst))
                    a19 = FarDistanceLikehood(selfId, node, false);
                else
                    a19 = FarDistanceLikehood(selfId, node, true);
                a27 = 0.9 - a19;


                //A1 AND A2 AND A7 AND A11 -> B1 
                double b0 = DSClass.AND(a1, a2) * CF[(int)IOTEventType.NotDropPacket];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b1 = DSClass.AND(DSClass.AND(a1, a3), DSClass.OR(DSClass.OR(a4, a6), a19)) * CF[(int)IOTEventType.NotDropPacketButNotReceivePacket];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b2 = DSClass.AND(DSClass.AND(a1, a3), a4) * CF[(int)IOTEventType.DropPacketDueToBandwith];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b3 = DSClass.AND(DSClass.AND(DSClass.AND(DSClass.AND(a1, a3), a5), a7), a27) * CF[(int)IOTEventType.DropPacketMaliciously];

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

                DSClass ds = new DSClass(pow(IOTDropPacketEventType.END));
                ds.SetM(pow(IOTDropPacketEventType.NotDropPacket), b0);
                ds.SetM(pow(IOTDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
                ds.SetM(pow(IOTDropPacketEventType.DropPacketDueToBandwith), b2);
                ds.SetM(pow(IOTDropPacketEventType.DropPacketMaliciously), b3);
                ds.SetM(pow(IOTDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
                ds.Cal();
                //ds.Output();
                //此处，我们先过滤一些正常事件，否则事件太多了
                if (ds.b[pow(IOTEventType.NotDropPacket) + pow(IOTEventType.NotDropPacketButNotReceivePacket)] < global.NormalBelief
                    && ds.p[pow(IOTEventType.NotDropPacket) + pow(IOTEventType.NotDropPacketButNotReceivePacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "->" + p.pkg.Next + "[" + p.pkg.PrevSenderSeq + "]";
                    IOTEventTrustResult r = new IOTEventTrustResult(node, selfId, pkgIdent, IOTEventCategoryType.DropPacket, ds);
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

        static List<IOTEventTrustResult> DeduceModifyPacketMaliciously(int selfId, HashSet<IOTPhenomemon> observedPhenomemons, double currentTime)
        { 
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                double a1, a2, a3, a4, a6, a8_9;

                if(p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst == p.nodeId)
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
                a2 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_PACKET, node, p.start, p.start + global.sendPacketTimeout, p.pkg);
                a3 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.NOT_SEND_PACKET, node, p.start, Scheduler.getInstance().currentTime, p.pkg);
                a4 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.BANDWIDTH_BUSY, selfId, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
                a8_9 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SAME_PACKET_HEADER, selfId, p.start, p.start + global.sendPacketTimeout, p.pkg);


                double b4 = DSClass.AND(a1, a2) * CF[(int)IOTEventType.NotModifyPacket];
                double b5 = DSClass.AND(DSClass.AND(DSClass.AND(a1, a3), a8_9), a4) * CF[(int)IOTEventType.NotModifyPacketButNetworkFaulty];
                double b6 = DSClass.AND(DSClass.AND(a1, a3), a8_9) * CF[(int)IOTEventType.ModifyPacketDueToNodeFaulty];
                double b7 = DSClass.AND(DSClass.AND(a1, a3), a8_9) * CF[(int)IOTEventType.ModifyPacketMaliciously];



                DSClass ds = new DSClass(pow(IOTModifyPacketEventType.END));
                ds.SetM(pow(IOTModifyPacketEventType.NotModifyPacket), b4);
                ds.SetM(pow(IOTModifyPacketEventType.NotModifyPacketButNetworkFaulty), b5);
                ds.SetM(pow(IOTModifyPacketEventType.ModifyPacketDueToNodeFaulty), b6);
                ds.SetM(pow(IOTModifyPacketEventType.ModifyPacketMaliciously), b7);
                ds.SetM(pow(IOTModifyPacketEventType.END) - 1, 1 - b4 - b5 - b6 - b7);
                ds.Cal();
                //ds.Output();
                //此处，我们先过滤一些正常事件，否则事件太多了
                if (ds.b[pow(IOTModifyPacketEventType.NotModifyPacket)] < global.NormalBelief
                    && ds.p[pow(IOTModifyPacketEventType.NotModifyPacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "->" + p.pkg.Next + "[" + p.pkg.PrevSenderSeq + "]";
                    IOTEventTrustResult r = new IOTEventTrustResult(node, selfId, pkgIdent, IOTEventCategoryType.ModifyPacket, ds);
                    r.totalEventCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }

            }
            return list;
        }




        static List<IOTEventTrustResult> DeduceMakePacketMaliciously(int selfId, HashSet<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            foreach(IOTPhenomemon p in observedPhenomemons)
            {
                double a2, a24, a23, a22_10;

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst == p.nodeId)
                    continue;
                else if (selfId == p.nodeId)//自己不检查自己的可信度
                    continue;
                else if (global.readers[p.nodeId].IsGateway)
                    continue;
                else if (currentTime - p.start < global.checkPhenomemonTimeout)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;

                a2 = p.likehood; //likehood of receiving a packet at time p.start
                a24 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.RECV_PACKET, selfId, p.start - global.checkPhenomemonTimeout, p.start);
                a23 = Inverse(a24);
                a22_10 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SAME_PACKET_DATA, selfId, p.start, p.start + global.sendPacketTimeout, p.pkg);


                double b8 = DSClass.AND(a2, a24) * CF[(int)IOTEventType.NotMakePacket];
                double b9 =DSClass.AND(DSClass.OR(a23, a22_10), a2) * CF[(int)IOTEventType.MakePacketMaliciously];



                DSClass ds = new DSClass(pow(IOTMakePacketEventType.END));
                ds.SetM(pow(IOTMakePacketEventType.NotMakePacket), b8);
                ds.SetM(pow(IOTMakePacketEventType.MakePacketMaliciously), b9);
                ds.SetM(pow(IOTMakePacketEventType.END) - 1, 1 - b8 - b9);
                ds.Cal();
                //ds.Output();
                //此处，我们先过滤一些正常事件，否则事件太多了
                if (ds.b[pow(IOTMakePacketEventType.NotMakePacket)] < global.NormalBelief
                    && ds.p[pow(IOTMakePacketEventType.NotMakePacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "->" + p.pkg.Next + "[" + p.pkg.PrevSenderSeq + "]";
                    IOTEventTrustResult r = new IOTEventTrustResult(node, selfId, pkgIdent, IOTEventCategoryType.MakePacket, ds);
                    r.totalEventCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }

            }
            return list;
        }







        static List<IOTEventTrustResult> DeduceDropCommandMaliciously(int selfId, HashSet<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                double a15_16, a17_20, a18_20, a19, a6, a7;

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst != p.nodeId ||p.pkg.Type != PacketType.COMMAND)
                    continue;
                else if (selfId == p.nodeId)//自己不检查自己的可信度
                    continue;
                else if (currentTime - p.start < global.checkPhenomemonTimeout)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;

                a15_16 = p.likehood; //likehood of receiving a packet at time p.start

                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
                a7 = 0.9 - a6;

                a17_20 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_COMMAND, node, p.start, p.start + global.sendPacketTimeout, p, ComparePhenomemonByExactTag);
                a18_20 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.NOT_SEND_COMMAND, node, p.start, Scheduler.getInstance().currentTime, p.pkg);
                //第三个参数是“数据包的目的地是否是标签”，由于命令的目的肯定是标签，所以永远为true
                a19 = FarDistanceLikehood(selfId, node, true);


                double b14 = DSClass.AND(a15_16, a17_20) * CF[(int)IOTEventType.NotDropCommand];
                double b15 = DSClass.AND(DSClass.AND(a15_16, a18_20), DSClass.OR(a6, a19)) * CF[(int)IOTEventType.NotDropCommandButNotDetected];
                double b16 = DSClass.AND(DSClass.AND(a15_16, a18_20), a7) * CF[(int)IOTEventType.DropCommandMaliciously];


                DSClass ds = new DSClass(pow(IOTDropCommandEventType.END));
                ds.SetM(pow(IOTDropCommandEventType.NotDropCommand), b14);
                ds.SetM(pow(IOTDropCommandEventType.NotDropCommandButNotDetected), b15);
                ds.SetM(pow(IOTDropCommandEventType.DropCommandMaliciously), b16);
                ds.SetM(pow(IOTDropCommandEventType.END) - 1, 1 - b14 - b15 - b16);
                ds.Cal();
                //ds.Output();
                //此处，我们先过滤一些正常事件，否则事件太多了
                if (ds.b[pow(IOTDropCommandEventType.NotDropCommand)] < global.NormalBelief
                    && ds.p[pow(IOTDropCommandEventType.NotDropCommand)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "->" + p.pkg.Next + "[" + p.pkg.PrevSenderSeq + "]";
                    IOTEventTrustResult r = new IOTEventTrustResult(node, selfId, pkgIdent, IOTEventCategoryType.DropCommand, ds);
                    r.totalEventCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }
            }
            return list;
        }

        static List<IOTEventTrustResult> DeduceModifyCommandMaliciously(int selfId, HashSet<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                double a15_16, a17_20, a21, a6, a7, a17;

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst != p.nodeId || p.pkg.Type != PacketType.COMMAND)
                    continue;
                else if (selfId == p.nodeId)//自己不检查自己的可信度
                    continue;
                else if (currentTime - p.start < global.checkPhenomemonTimeout)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;

                a15_16 = p.likehood; //likehood of receiving a packet at time p.start

                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
                a7 = 0.9 - a6;

                a17_20 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_COMMAND,
                    node, p.start, p.start + global.sendPacketTimeout, p, ComparePhenomemonByExactTag);

                List<IOTPhenomemon> similiarList = new List<IOTPhenomemon>();
                a17 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_COMMAND,
                    node, p.start, p.start + global.sendPacketTimeout, p, similiarList, ComparePhenomemonBySimiliarTag);
                a21 = SimiliarCommand(p, similiarList);


                double b17 = DSClass.AND(a15_16, a17_20) * CF[(int)IOTEventType.NotModifyCommand];
                double b18 = DSClass.AND(DSClass.AND(a15_16, a17), a21) * CF[(int)IOTEventType.ModifyCommandDueToNodeFaulty];
                double b19 = DSClass.AND(DSClass.AND(a15_16, a17), a21) * CF[(int)IOTEventType.ModifyCommandMaliciously];


                DSClass ds = new DSClass(pow(IOTModifyCommandEventType.END));
                ds.SetM(pow(IOTModifyCommandEventType.NotModifyCommand), b17);
                ds.SetM(pow(IOTModifyCommandEventType.ModifyCommandDueToNodeFaulty), b18);
                ds.SetM(pow(IOTModifyCommandEventType.ModifyCommandDueToNodeFaulty), b19);
                ds.SetM(pow(IOTModifyCommandEventType.END) - 1, 1 - b17 - b18 - b19);
                ds.Cal();
                //ds.Output();
                //此处，我们先过滤一些正常事件，否则事件太多了
                if (ds.b[pow(IOTModifyCommandEventType.NotModifyCommand)] < global.NormalBelief
                    && ds.p[pow(IOTModifyCommandEventType.NotModifyCommand)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "->" + p.pkg.Next + "[" + p.pkg.PrevSenderSeq + "]";
                    IOTEventTrustResult r = new IOTEventTrustResult(node, selfId, pkgIdent, IOTEventCategoryType.ModifyCommand, ds);
                    r.totalEventCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);                    
                }
            }
            return list;
        }


        static List<IOTEventTrustResult> DeduceMakeCommandMaliciously(int selfId, HashSet<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                double a20, a24, a5, a6, a7, a17, a23, a4;

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.SEND_COMMAND)
                    continue;
                else if (p.pkg.Dst != p.nodeId || p.pkg.Type != PacketType.COMMAND)
                    continue;
                else if (selfId == p.nodeId)//自己不检查自己的可信度
                    continue;
                else if (currentTime - p.start < global.checkPhenomemonTimeout)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;
                
                a17 = p.likehood; //likehood of receiving a packet at time p.start

                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
                //如果对带宽占用没有知识，则正反都设置为未知。
                a4 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.BANDWIDTH_BUSY, selfId, p.start - global.checkPhenomemonTimeout, Scheduler.getInstance().currentTime);
                a5 = 0.9 - a4;
                a7 = 0.9 - a6;

                List<IOTPhenomemon> similiarList = new List<IOTPhenomemon>();
                a24 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_COMMAND,
                    node, p.start - global.sendPacketTimeout, p.start, p, similiarList, ComparePhenomemonBySimiliarTag);
                a20 = SimiliarCommand(p, similiarList);


                if (Utility.DoubleEqual(a20, global.SmallValue) || Utility.DoubleEqual(a24, global.SmallValue))
                    a23 = 0.9 - global.SmallValue;
                else
                    a23 = Math.Max(a20, a24);
           

                double b20 = DSClass.AND(DSClass.AND(a17, a20), a24) * CF[(int)IOTEventType.NotMakeCommand];
                double b21 = DSClass.AND(DSClass.AND(DSClass.AND(17, a20), a23), a6) * CF[(int)IOTEventType.NotMakeCommandButMove];
                double b22 = DSClass.AND(DSClass.AND(DSClass.AND(17, a20), a23), a4) * CF[(int)IOTEventType.NotMakeCommandButNetworkDelay];
                double b23 = DSClass.AND(DSClass.AND(DSClass.AND(DSClass.AND(17, a20), a23), a5), a7) * CF[(int)IOTEventType.MakeCommandMaliciously];


                DSClass ds = new DSClass(pow(IOTMakeCommandEventType.END));
                ds.SetM(pow(IOTMakeCommandEventType.NotMakeCommand), b20);
                ds.SetM(pow(IOTMakeCommandEventType.NotMakeCommandButNetworkDelay), b21);
                ds.SetM(pow(IOTMakeCommandEventType.NotMakeCommandButNetworkDelay), b22);
                ds.SetM(pow(IOTMakeCommandEventType.MakeCommandMaliciously), b23);
                ds.SetM(pow(IOTMakeCommandEventType.END) - 1, 1 - b21 - b22 - b23);
                ds.Cal();
                //ds.Output();
                //此处，我们先过滤一些正常事件，否则事件太多了
                if (ds.b[pow(IOTMakeCommandEventType.NotMakeCommand)] < global.NormalBelief
                    && ds.p[pow(IOTMakeCommandEventType.NotMakeCommand)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "->" + p.pkg.Next + "[" + p.pkg.PrevSenderSeq + "]";
                    IOTEventTrustResult r = new IOTEventTrustResult(node, selfId, pkgIdent, IOTEventCategoryType.MakeCommand, ds);
                    r.totalEventCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }
            }
            return list;
        }


        /*
         * 在普通节点一级，推导出各种事件，事件根据数据包划分，一个事件为一个类型。
         */
        public static List<IOTEventTrustResult> DeduceAllEventTrusts(int selfId, HashSet<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            list.AddRange(DeduceDropPacketMaliciously(selfId, observedPhenomemons, currentTime));
            list.AddRange(DeduceModifyPacketMaliciously(selfId, observedPhenomemons, currentTime));
            list.AddRange(DeduceMakePacketMaliciously(selfId, observedPhenomemons, currentTime));
            list.AddRange(DeduceDropCommandMaliciously(selfId, observedPhenomemons, currentTime));
            list.AddRange(DeduceModifyCommandMaliciously(selfId, observedPhenomemons, currentTime));
            list.AddRange(DeduceMakeCommandMaliciously(selfId, observedPhenomemons, currentTime));
            //调整每个事件的因子,如果有的因子为0，则调为0.001，防止d-s的冲突
            for (int i = 0; i < list.Count; i++)
            {
                IOTEventTrustResult result = list[i];
                int node = result.node;
                result.ds.Normalize();
            }
            return list;
        }


        static double ConditionHappened(HashSet<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node)
        {
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type)
                    return p.likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(HashSet<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node, double starttime, double endtime)
        {
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type
                    && p.start >= starttime && p.end <= endtime)
                    return p.likehood;
            }
            if (type == IOTPhenomemonType.DIST_FAR)//如果计算节点距离的可能性，且不在邻居中，则说明该节点很远，可能性为很大
                return 0.8;
            else
                return global.SmallValue;
        }


        static double ConditionHappened(HashSet<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node, double time)
        {
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type && Utility.DoubleEqual(p.start, time))
                    return p.likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(HashSet<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node, double time, Packet pkg)
        {
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type
                    && Utility.DoubleEqual(p.start, time) && Packet.IsSamePacket(p.pkg, pkg))
                    return p.likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(HashSet<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node, double starttime, double endtime, Packet pkg)
        {
            foreach (IOTPhenomemon p in observedPhenomemons)
            {
                if (p.nodeId == node && p.type == type
                    && p.start >= starttime && p.end <= endtime
                    && Packet.IsSamePacket(p.pkg, pkg))
                    return p.likehood;
            }
            return global.SmallValue;
        }

        static double ConditionHappened(HashSet<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node,
            double starttime, double endtime, IOTPhenomemon p1, List<IOTPhenomemon> list, ComparePhenomemon comparer)
        {
            double likehood = global.SmallValue;
            foreach (IOTPhenomemon p in observedPhenomemons)
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


        static double ConditionHappened(HashSet<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node,
            double starttime, double endtime, IOTPhenomemon p1, ComparePhenomemon comparer)
        {
            foreach (IOTPhenomemon p in observedPhenomemons)
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
        
        

        static int pow(IOTDropPacketEventType t)
        {
            return DSClass.pow((int)t- (int)IOTDropPacketEventType.NotDropPacket);
        }

        public static int pow(IOTModifyPacketEventType t)
        {
            return DSClass.pow((int)t - (int)IOTModifyPacketEventType.NotModifyPacket); 
        }


        public static int pow(IOTMakePacketEventType t)
        {
            return DSClass.pow((int)t - (int)IOTMakePacketEventType.NotMakePacket);
        }

        public static int pow(IOTDropCommandEventType t)
        {
            return DSClass.pow((int)t - (int)IOTDropCommandEventType.NotDropCommand);
        }

        static int pow(IOTModifyCommandEventType t)
        {
            return DSClass.pow((int)t - (int)IOTModifyCommandEventType.NotModifyCommand);
        }

        static int pow(IOTMakeCommandEventType t)
        {
            return DSClass.pow((int)t - (int)IOTMakeCommandEventType.NotMakeCommand);
        }

        public static int pow(IOTEventType type)
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


    }
}
