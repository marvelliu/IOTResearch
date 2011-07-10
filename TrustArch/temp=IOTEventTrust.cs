using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocForward;
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
        public string eventIdent;
        public DSClass ds;
        public int app;
        public IOTEventCategoryType category;
        public int totalCount;
        public int count;

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
                    return (int)IOTEventType.NotDropPacketButNotReceivePacket;
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

        public IOTEventTrustResult(int node, string pkgIdent, IOTEventCategoryType category, DSClass ds)
        {
            this.node = node;
            this.ds = ds;
            this.category = category;
            this.eventIdent = pkgIdent+"-"+(int)category;
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
        public int[] confirmedNums;
        public int totalNums;

        public IOTEventTrustCategoryResult(int node, string pkgIdent, int app, IOTEventCategoryType category, DSClass ds)
        {
            this.node = node;
            this.ds = ds;
            this.category = category;
            this.categoryIdent = pkgIdent + "-" + (int)category;
            this.app = app;
            this.confirmedNums = new int[(int)IOTEventType.COUNT];
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
            return (p1.pkg.Command.tag == p2.pkg.Dst && p1.pkg.PacketSeq == p2.pkg.PacketSeq);
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
                likehood = Math.Max(likehood, 1 - global.SmallValue - Math.Abs(p1.pkg.PacketSeq - p.pkg.PacketSeq) / 100);
            }
            return likehood;
        }

        static List<IOTEventTrustResult> DeduceDropPacketMaliciously(int selfId, List<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                double a1, a2, a3, a4, a5, a6, a7;
                IOTPhenomemon p = observedPhenomemons[i];

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst == p.nodeId)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;

                a1 = p.likehood; //likehood of receiving a packet at time p.start
                a2 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_PACKET, node, p.start, p.start + global.sendPacketTimeout, p.pkg);
                a3 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.NOT_SEND_PACKET, node, p.start, p.start + global.sendPacketTimeout, p.pkg);
                a4 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.BANDWIDTH_BUSY, selfId, p.start - global.checkPhenomemonTimeout, p.start + global.checkPhenomemonTimeout);
                //如果对带宽占用没有知识，则正反都设置为未知。
                if (Utility.DoubleEqual(a4, global.SmallValue))
                    a5 = global.SmallValue;
                else
                    a5 = 0.9 - a4;
                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, p.start + global.checkPhenomemonTimeout);
                if (Utility.DoubleEqual(a6, global.SmallValue))
                    a7 = global.SmallValue;
                else
                    a7 = 0.9 - a6;


                //A1 AND A2 AND A7 AND A11 -> B1 
                double b0 = DSClass.AND(a1, a2) * CF[(int)IOTEventType.NotDropPacket];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b1 = DSClass.AND(DSClass.AND(a1, a3), DSClass.OR(a4, a5)) * CF[(int)IOTEventType.NotDropPacketButNotReceivePacket];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b2 = DSClass.AND(DSClass.AND(a1, a3), a4) * CF[(int)IOTEventType.DropPacketDueToBandwith];
                //A1 AND A2 AND A7 AND A11 -> B1
                double b3 = DSClass.AND(DSClass.AND(DSClass.AND(a1, a3), a5), a7) * CF[(int)IOTEventType.DropPacketMaliciously];
                

                DSClass ds = new DSClass(pow(IOTDropPacketEventType.END));
                ds.SetM(pow(IOTDropPacketEventType.NotDropPacket), b0);
                ds.SetM(pow(IOTDropPacketEventType.NotDropPacketButNotReceivePacket), b1);
                ds.SetM(pow(IOTDropPacketEventType.DropPacketDueToBandwith), b2);
                ds.SetM(pow(IOTDropPacketEventType.DropPacketMaliciously), b3);
                ds.SetM(pow(IOTDropPacketEventType.END) - 1, 1 - b0 - b1 - b2 - b3);
                ds.Cal();
                //ds.Output();
                //此处，我们先过滤一些正常事件，否则事件太多了
                if (ds.b[pow(IOTEventType.NotDropPacket)] < global.NormalBelief
                    || ds.p[pow(IOTEventType.NotDropPacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "-" + p.pkg.Next + global.PacketSeq;
                    IOTEventTrustResult r = new IOTEventTrustResult(node, pkgIdent, IOTEventCategoryType.DropPacket, ds);
                    r.totalCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }
            }
            return list;
        }

        static List<IOTEventTrustResult> DeduceModifyPacketMaliciously(int selfId, List<IOTPhenomemon> observedPhenomemons, double currentTime)
        { 
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                double a1, a2, a3, a4, a6, a8_9;

                IOTPhenomemon p = observedPhenomemons[i];
                if(p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst == p.nodeId)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;

                a1 = p.likehood; //likehood of receiving a packet at time p.start
                a2 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_PACKET, node, p.start, p.start + global.sendPacketTimeout, p.pkg);                
                a3 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.NOT_SEND_PACKET, node, p.start, p.start + global.sendPacketTimeout, p.pkg);
                a4 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.BANDWIDTH_BUSY, selfId, p.start - global.checkPhenomemonTimeout, p.end + global.checkPhenomemonTimeout);
                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST, node, p.start - global.checkPhenomemonTimeout, p.start + global.checkPhenomemonTimeout);
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
                    || ds.p[pow(IOTModifyPacketEventType.NotModifyPacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "-" + p.pkg.Next + global.PacketSeq;
                    IOTEventTrustResult r = new IOTEventTrustResult(node, pkgIdent, IOTEventCategoryType.ModifyPacket, ds);
                    r.totalCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }

            }
            return list;
        }




        static List<IOTEventTrustResult> DeduceMakePacketMaliciously(int selfId, List<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                double a2, a24, a23, a22_10;

                IOTPhenomemon p = observedPhenomemons[i];
                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst == p.nodeId)
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
                    || ds.p[pow(IOTMakePacketEventType.NotMakePacket)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "-" + p.pkg.Next + global.PacketSeq;
                    IOTEventTrustResult r = new IOTEventTrustResult(node, pkgIdent, IOTEventCategoryType.MakePacket, ds);
                    r.totalCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }

            }
            return list;
        }







        static List<IOTEventTrustResult> DeduceDropCommandMaliciously(int selfId, List<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                double a15_16, a17_20, a18_20, a19, a6, a7;
                IOTPhenomemon p = observedPhenomemons[i];

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst != p.nodeId ||p.pkg.Type != PacketType.COMMAND)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;

                a15_16 = p.likehood; //likehood of receiving a packet at time p.start

                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST,
                    node, p.start - global.checkPhenomemonTimeout, p.start + global.checkPhenomemonTimeout);
                if (Utility.DoubleEqual(a6, global.SmallValue))
                    a7 = global.SmallValue;
                else
                    a7 = 0.9 - a6;

                a17_20 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.SEND_COMMAND,
                    node, p.start, p.start + global.sendPacketTimeout, p, ComparePhenomemonByExactTag);
                a18_20 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.NOT_SEND_COMMAND, 
                    node, p.start, p.start + global.sendPacketTimeout, p.pkg);
                a19 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.DIST_FAR,
                    node, p.start - global.checkPhenomemonTimeout, p.start + global.checkPhenomemonTimeout);


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
                    || ds.p[pow(IOTDropCommandEventType.NotDropCommand)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "-" + p.pkg.Next + global.PacketSeq;
                    IOTEventTrustResult r = new IOTEventTrustResult(node, pkgIdent, IOTEventCategoryType.DropCommand, ds);
                    r.totalCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }
            }
            return list;
        }

        static List<IOTEventTrustResult> DeduceModifyCommandMaliciously(int selfId, List<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                double a15_16, a17_20, a21, a6, a7, a17;
                IOTPhenomemon p = observedPhenomemons[i];

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.RECV_PACKET)
                    continue;
                else if (p.pkg.Dst != p.nodeId || p.pkg.Type != PacketType.COMMAND)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;

                a15_16 = p.likehood; //likehood of receiving a packet at time p.start

                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST,
                    node, p.start - global.checkPhenomemonTimeout, p.start + global.checkPhenomemonTimeout);
                if (Utility.DoubleEqual(a6, global.SmallValue))
                    a7 = global.SmallValue;
                else
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
                    || ds.p[pow(IOTModifyCommandEventType.NotModifyCommand)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "-" + p.pkg.Next + global.PacketSeq;
                    IOTEventTrustResult r = new IOTEventTrustResult(node, pkgIdent, IOTEventCategoryType.ModifyCommand, ds);
                    r.totalCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }
            }
            return list;
        }


        static List<IOTEventTrustResult> DeduceMakeCommandMaliciously(int selfId, List<IOTPhenomemon> observedPhenomemons, double currentTime)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();
            //每个节点处理该类事件的次数
            int[] eventCount = new int[global.readerNum];
            List<IOTEventTrustResult> list = new List<IOTEventTrustResult>();
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                double a20, a24, a5, a6, a7, a17, a23, a4;
                IOTPhenomemon p = observedPhenomemons[i];

                if (p.likehood <= global.SmallValue)
                    continue;
                if (p.type != IOTPhenomemonType.SEND_COMMAND)
                    continue;
                else if (p.pkg.Dst != p.nodeId || p.pkg.Type != PacketType.COMMAND)
                    continue;

                int node = p.nodeId;
                eventCount[node]++;
                
                a17 = p.likehood; //likehood of receiving a packet at time p.start

                a6 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.MOVE_FAST,
                    node, p.start - global.checkPhenomemonTimeout, p.start + global.checkPhenomemonTimeout);
                //如果对带宽占用没有知识，则正反都设置为未知。
                a4 = ConditionHappened(observedPhenomemons, IOTPhenomemonType.BANDWIDTH_BUSY, selfId, p.start - global.checkPhenomemonTimeout, p.start + global.checkPhenomemonTimeout);
                if (Utility.DoubleEqual(a4, global.SmallValue))
                    a5 = global.SmallValue;
                else
                    a5 = 0.9 - a4;
                if (Utility.DoubleEqual(a6, global.SmallValue))
                    a7 = global.SmallValue;
                else
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
                    || ds.p[pow(IOTMakeCommandEventType.NotMakeCommand)] < global.NormalPlausibility)//确实是攻击,恶意事件的信念大于正常事件，或恶意事件的信念大于某一个阈值
                {
                    string pkgIdent = p.pkg.Prev + "-" + p.pkg.Next + global.PacketSeq;
                    IOTEventTrustResult r = new IOTEventTrustResult(node, pkgIdent, IOTEventCategoryType.MakeCommand, ds);
                    r.totalCount = eventCount[node];
                    r.app = p.pkg.AppId;
                    list.Add(r);
                }
            }
            return list;
        }


        /*
         * 在普通节点一级，推导出各种事件，事件根据数据包划分，一个事件为一个类型。
         */
        public static List<IOTEventTrustResult> DeduceAllEventTrusts(int selfId, List<IOTPhenomemon> observedPhenomemons, double currentTime)
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


        static double ConditionHappened(List<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node)
        {
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                if (observedPhenomemons[i].nodeId == node && observedPhenomemons[i].type == type)
                    return observedPhenomemons[i].likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(List<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node, double starttime, double endtime)
        {
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                if (observedPhenomemons[i].nodeId == node && observedPhenomemons[i].type == type
                    && observedPhenomemons[i].start >= starttime && observedPhenomemons[i].end <= endtime)
                    return observedPhenomemons[i].likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(List<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node, double time)
        {
            for(int i=0;i<observedPhenomemons.Count;i++)
            {
                if (observedPhenomemons[i].nodeId == node && observedPhenomemons[i].type == type && Utility.DoubleEqual(observedPhenomemons[i].start, time))
                    return observedPhenomemons[i].likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(List<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node, double time, Packet pkg)
        {
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                if (observedPhenomemons[i].nodeId == node && observedPhenomemons[i].type == type
                    && Utility.DoubleEqual(observedPhenomemons[i].start, time) && Packet.IsSamePacket(observedPhenomemons[i].pkg, pkg))
                    return observedPhenomemons[i].likehood;
            }
            return global.SmallValue;
        }


        static double ConditionHappened(List<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node, double starttime, double endtime, Packet pkg)
        {
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                if (observedPhenomemons[i].nodeId == node && observedPhenomemons[i].type == type  
                    && observedPhenomemons[i].start>=starttime && observedPhenomemons[i].end<=endtime
                    && Packet.IsSamePacket(observedPhenomemons[i].pkg, pkg))
                    return observedPhenomemons[i].likehood;
            }
            return global.SmallValue;
        }
        
        static double ConditionHappened(List<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node,
            double starttime, double endtime, IOTPhenomemon p, List<IOTPhenomemon> list, ComparePhenomemon comparer)
        {
            double likehood = global.SmallValue;
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                if (observedPhenomemons[i].nodeId == node && observedPhenomemons[i].type == type
                    && observedPhenomemons[i].start >= starttime && observedPhenomemons[i].end <= endtime
                    && comparer(p, observedPhenomemons[i]))
                {
                    list.Add(observedPhenomemons[i]);
                    likehood = Math.Max(observedPhenomemons[i].likehood, likehood);
                }
            }
            return likehood;
        }


        static double ConditionHappened(List<IOTPhenomemon> observedPhenomemons, IOTPhenomemonType type, int node,
            double starttime, double endtime, IOTPhenomemon p, ComparePhenomemon comparer)
        {
            for (int i = 0; i < observedPhenomemons.Count; i++)
            {
                if (observedPhenomemons[i].nodeId == node && observedPhenomemons[i].type == type
                    && observedPhenomemons[i].start >= starttime && observedPhenomemons[i].end <= endtime
                    && comparer(p, observedPhenomemons[i]))
                {
                    return observedPhenomemons[i].likehood;
                }
            }
            return global.SmallValue;
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
