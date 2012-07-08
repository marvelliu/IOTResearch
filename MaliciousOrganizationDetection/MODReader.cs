using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using AdHocBaseApp;

namespace MaliciousOrganizationDetection
{
    public class DirectTrustEntity
    {
        public double value;
        public double time;
    }

    public class Deduce2Result
    {
        public bool IsTotalReportSupport;
        public bool IsAccept;
    }
    public enum BehaviorType
    {
        NORMAL,
        DROP_PACKET,
        //这里简化为只检查是否抛弃数据包
    }
    public class IteratorType //每次博弈的计数
    {
        public bool DropEvent;
        public bool NormalNode;
        public bool ReportSupportM;

        public IteratorType(bool DropEvent, bool NormalNode, bool ReportSupportM)
        {
            this.DropEvent = DropEvent;
            this.NormalNode = NormalNode;
            this.ReportSupportM = ReportSupportM;
        }
        private IteratorType()
        { }
    }

    public class MODReader : Reader
    {

        private MODGlobal global;
        public BehaviorType readerType;

        private int totalReceivedPackets;

        private Dictionary<int, MODPhenomemon> neighborSpeedPhenomemons;
        //普通节点观察到的现象
        private HashSet<MODPhenomemon> observedPhenomemons;

        private Dictionary<string, Dictionary<Node, MODEventTrustResult>> receivedEventReports;
        private HashSet<string> deducedEventReports;
        private HashSet<string> toDeducedEventReports;


        private Dictionary<Node, double> pNormal;
        //private Dictionary<Node, double> pSupportMByNormal;
        //private Dictionary<Node, double> pNonsupportMByMalicious;

        private Dictionary<Node, Dictionary<Node, double>> pNBNormal;
        //private Dictionary<Node, Dictionary<Node, double>> pNBSupportByNormal;
        //private Dictionary<Node, Dictionary<Node, double>> pNBNonsupportByMalicious;

        //包括了节点和机构的可疑属性
        private Dictionary<Node, List<double>> nodeSuspectCount;
        private Dictionary<Node, double> nodeTrustWeights;
        private Dictionary<Node, double> nodeTrustWeightsLastUpdate;
        private List<DirectTrustEntity>[] orgDirectTrustWeights;
        private List<IteratorType> nodeIteractions;//博弈次数，越多则经验越多
        private Dictionary<Node, List<double>> nodeHistoryMyVariance;
        private Dictionary<Node, List<double>> nodeHistoryTotalVariance;

        private Dictionary<Node, Dictionary<Node, List<double>>> NBNodeSuspectCount;
        private Dictionary<Node, Dictionary<Node, double>> NBNodeTrustWeights;
        private Dictionary<Node, Dictionary<Node, double>> NBNodeTrustWeightsLastUpdate;
        private Dictionary<Node, List<IteratorType>> NBNodeIteractions;//博弈次数，越多则经验越多
        private Dictionary<Node, Dictionary<Node, List<double>>> NBNodeHistoryMyVariance;
        private Dictionary<Node, Dictionary<Node, List<double>>> NBNodeHistoryTotalVariance;

        new public static MODReader ProduceReader(int id, int org)
        {
            return new MODReader(id, org);
        }

        public MODReader(int id, int org)
            : base(id, org)
        {
            this.global = (MODGlobal)Global.getInstance();
            this.readerType = BehaviorType.NORMAL;
            this.observedPhenomemons = new HashSet<MODPhenomemon>();
            this.neighborSpeedPhenomemons = new Dictionary<int, MODPhenomemon>();
            this.receivedEventReports = new Dictionary<string, Dictionary<Node, MODEventTrustResult>>();
            this.deducedEventReports = new HashSet<string>();
            this.toDeducedEventReports = new HashSet<string>();

            this.pNormal = new Dictionary<Node, double>();
            //this.pSupportMByNormal = new Dictionary<Node, double>();
            //this.pNonsupportMByMalicious = new Dictionary<Node, double>();

            this.pNBNormal = new Dictionary<Node, Dictionary<Node, double>>();
            //this.pNBSupportByNormal = new Dictionary<Node, Dictionary<Node, double>>();
            //this.pNBNonsupportByMalicious = new Dictionary<Node, Dictionary<Node, double>>();

            this.nodeSuspectCount = new Dictionary<Node, List<double>>() ;
            this.nodeTrustWeights = new Dictionary<Node, double>();
            this.nodeTrustWeightsLastUpdate = new Dictionary<Node, double>();
            this.orgDirectTrustWeights = new List<DirectTrustEntity>[global.orgNum];
            for (int i = 0; i < this.orgDirectTrustWeights.Length;i++ )
                this.orgDirectTrustWeights[i] = new List<DirectTrustEntity>();
            this.NBNodeIteractions = new Dictionary<Node,List<IteratorType>>();
            this.nodeIteractions = new List<IteratorType>();
            this.nodeHistoryMyVariance = new Dictionary<Node, List<double>>();
            this.nodeHistoryTotalVariance = new Dictionary<Node, List<double>>();

            this.NBNodeSuspectCount = new Dictionary<Node, Dictionary<Node, List<double>>>();
            this.NBNodeTrustWeights = new Dictionary<Node, Dictionary<Node, double>>();
            this.NBNodeTrustWeightsLastUpdate = new Dictionary<Node, Dictionary<Node, double>>();
            this.NBNodeHistoryMyVariance = new Dictionary<Node, Dictionary<Node, List<double>>>();
            this.NBNodeHistoryTotalVariance = new Dictionary<Node, Dictionary<Node, List<double>>>();





            MODPhenomemon p = new MODPhenomemon(MODPhenomemonType.MOVE_FAST, id);
            this.neighborSpeedPhenomemons.Add(id, p);
            this.observedPhenomemons.Add(p);

            CheckRoutine();

            //Event.AddEvent(new Event( global.startTime + global.checkSWHubCandidateInterval, EventType.CHK_SW_NB, this, null));
        }



        void ClearOutdatedPhenomemons()
        {
            float timeThrehold = 2 * global.checkPhenomemonTimeout;

            List<MODPhenomemon> temp = new List<MODPhenomemon>();
            foreach (MODPhenomemon p in this.observedPhenomemons)
            {
                if (scheduler.currentTime - p.start > timeThrehold)
                {
                    temp.Add(p);
                    if (p.type == MODPhenomemonType.MOVE_FAST)
                        this.neighborSpeedPhenomemons.Remove(p.nodeId);
                }
            }
            foreach (MODPhenomemon p in temp)
            {
                this.observedPhenomemons.Remove(p);
            }
            //Console.WriteLine("Clear outdated phenomemon done.");
        }


        void CheckNodeSpeeds()
        {
            foreach (KeyValuePair<int, Neighbor> k in this.Neighbors)
            {
                int node = k.Key;
                Neighbor nb = k.Value;

                //节点无法测出邻居的距离，但是可以根据信号强弱估计出，为简化，此处直接给出两点距离。
                double speed = global.readers[node].GetCurrentSpeed();
                if (!this.neighborSpeedPhenomemons.ContainsKey(node))
                {
                    MODPhenomemon p = new MODPhenomemon(MODPhenomemonType.MOVE_FAST, node);
                    p.start = 0;
                    this.neighborSpeedPhenomemons.Add(node, p);
                    this.observedPhenomemons.Add(p);
                }
                this.neighborSpeedPhenomemons[node].start = this.neighborSpeedPhenomemons[node].end;
                this.neighborSpeedPhenomemons[node].end = scheduler.currentTime;
                this.neighborSpeedPhenomemons[node].likehood = Math.Min(speed / global.nodeSpeedThreahold + global.SmallValue, 0.9);
            }
            this.neighborSpeedPhenomemons[Id].start = this.neighborSpeedPhenomemons[Id].end;
            this.neighborSpeedPhenomemons[Id].end = scheduler.currentTime;
            double s = GetCurrentSpeed();
            this.neighborSpeedPhenomemons[Id].likehood = Math.Min(s / global.nodeSpeedThreahold + global.SmallValue, 0.9);
        }

        //仅供调试
        public override bool SendData(Packet pkg)
        {

            //TODO
            if (this.Id == 1 && pkg.Src == this.Id)
            {
                Console.Write("NB nodes: ");
                List<Reader> list = GetAllNearReaders(global.nodeMaxDist, true);
                foreach (Reader r in list)
                    Console.Write("{0}\t", r.Id);
                Console.WriteLine();
            }

            return base.SendData(pkg);
        }

        void CheckTimeoutPhenomemons()
        {
            double sendTimeout = global.sendPacketTimeout;
            List<MODPhenomemon> temp1 = new List<MODPhenomemon>();
            List<MODPhenomemon> temp2 = new List<MODPhenomemon>();
            foreach (MODPhenomemon p in this.observedPhenomemons)
            {
                if (p.type == MODPhenomemonType.RECV_PACKET && scheduler.currentTime - p.start > sendTimeout)
                {
                    MODPhenomemon foundSend = null, foundNotSend = null;
                    foreach (MODPhenomemon p1 in this.observedPhenomemons)
                    {
                        if (p1.pkg == null)
                            continue;
                        //找到该节点对该数据包的操作
                        if (Packet.IsSamePacket(p1.pkg, p.pkg) &&
                            p1.nodeId == p.nodeId)
                        {
                            if (p1.type == MODPhenomemonType.SEND_PACKET)
                            {
                                foundSend = p1;
                                continue;
                            }
                            else if (p1.type == MODPhenomemonType.NOT_SEND_PACKET)
                            {
                                p1.end = scheduler.currentTime;
                                foundNotSend = p1;
                                continue;
                            }
                            else
                                continue;
                        }
                    }

                    if (foundSend != null && foundNotSend != null)
                    {
                        temp2.Add(foundNotSend);
                    }
                    else if (foundSend == null && foundNotSend == null)
                    {
                        MODPhenomemon p2 = new MODPhenomemon(MODPhenomemonType.NOT_SEND_PACKET, p.nodeId, p.start, scheduler.currentTime, p.pkg);
                        p2.likehood = global.checkTimeoutPhenomemonLikehood;
                        temp1.Add(p2);
                    }
                }
            }
            foreach (MODPhenomemon p in temp1)
            {
                this.observedPhenomemons.Add(p);
            }
            foreach (MODPhenomemon p in temp2)
            {
                this.observedPhenomemons.Remove(p);
            }
        }


        //普通节点检查现象
        public void CheckRoutine()
        {
            //if (global.debug)
            //    Console.WriteLine("[Debug] {0}{1} check routing.", type, Id);
            CheckNodeSpeeds();
            //Console.WriteLine("Reader{0} ClearOutdatedPhenomemons.", id);
            ClearOutdatedPhenomemons();

            //Console.WriteLine("Reader{0} CheckTimeoutPhenomemons.", id);
            CheckTimeoutPhenomemons();

            //.......................

            float time = scheduler.currentTime + global.checkPhenomemonTimeout;
            Event.AddEvent(new Event(time, EventType.CHK_RT_TIMEOUT, this, null));
            //Console.WriteLine("Reader{0} check routing done.", id);
        }

        private MODEventTrustResult GetEventTrustResult(string pkgIdent, MODPhenomemon p, List<MODEventTrustResult> results,
            int suspectedNodeId, int reportNodeId)
        {
            MODEventTrustResult realr = null;
            if (p == null)//没观察到接收数据包，则不确定
            {
                string[] x = pkgIdent.Split(new char[]{'-','>'});
                int prevId = int.Parse(x[0]);
                Reader suspectedNode = global.readers[suspectedNodeId];
                Reader prevNode = global.readers[prevId];
                double[] speeds = new double[3];
                if (this.Speed != null)
                    speeds[0] = this.Speed[0];
                if (suspectedNode.Speed != null)
                    speeds[1] = suspectedNode.Speed[0];
                if (prevNode.Speed != null)
                    speeds[2] = prevNode.Speed[0];

                bool[] isNeighbors = new bool[2]{this.Neighbors.ContainsKey(prevId), this.Neighbors.ContainsKey(suspectedNodeId)};

                realr = MODEventTrust.NotObservedEventTrustResult(suspectedNodeId, this.Id,
                            pkgIdent, MODEventCategoryType.DropPacket, speeds, isNeighbors);
            }
            else
            {
                realr = MODEventTrust.DeduceDropPacketMaliciouslyByPacket(this.Id, this.observedPhenomemons, scheduler.currentTime, p);
            }
            return GetEventTrustResult(pkgIdent, p, results, suspectedNodeId, reportNodeId, realr);
        }

        public int GetOneNormalNodeFromReports(Dictionary<Node, MODEventTrustResult> localcachedresults)
        {
            foreach (KeyValuePair<Node, MODEventTrustResult> k in localcachedresults)
            {
                if (!((MODReader)k.Key).IsMalicious())
                    return k.Key.Id;
            }
            return -1;
        }


        private void AddNeighborReports(Node minNode, MODEventTrustResult realr, int suspectedNodeId, string pkgIdent, Dictionary<Node, MODEventTrustResult> reports)
        {
            {
                MODReader nbNode = (MODReader)minNode;
                //如果已经保存，则继续
                if (!reports.ContainsKey(nbNode))
                {
                    MODEventTrustResult r = null;
                    //恶意节点
                    if (nbNode.IsMalicious() && realr.supportDroppingMalicious <= 0)
                    {
                        //如果事件是正常的，那么伪造恶意事件
                        r = MODEventTrust.ForgeMaliciousEventTrustResult(suspectedNodeId, nbNode.Id, pkgIdent, MODEventCategoryType.DropPacket);
                        r.supportDroppingMalicious = 1;
                    }
                    else if (nbNode.IsMalicious() && realr.supportDroppingMalicious > 0)
                    {
                        //如果事件是恶意的，那么伪造正常事件
                        r = MODEventTrust.ForgeNormalEventTrustResult(suspectedNodeId, nbNode.Id, pkgIdent, MODEventCategoryType.DropPacket);
                        r.supportDroppingMalicious = -1;
                    }
                    else
                    {
                        r = new MODEventTrustResult(suspectedNodeId, nbNode.Id, pkgIdent, MODEventCategoryType.DropPacket, realr.ds);
                        r.supportDroppingMalicious = realr.supportDroppingMalicious;
                    }
                    reports.Add(nbNode, r);
                }
 
            }
            foreach (int nbId in this.Neighbors.Keys)
            {
                if (nbId == minNode.Id)
                    continue;
                MODReader nbNode = (MODReader)global.readers[nbId];
                //如果已经保存，则继续
                if (!reports.ContainsKey(nbNode))
                {
                    if (reports.Count >= global.MaxReportCount)
                        break;
                    MODEventTrustResult r = null;
                    //恶意节点
                    if (nbNode.IsMalicious() && realr.supportDroppingMalicious <= 0)
                    {
                        //如果事件是正常的，那么伪造恶意事件
                        r = MODEventTrust.ForgeMaliciousEventTrustResult(suspectedNodeId, nbNode.Id, pkgIdent, MODEventCategoryType.DropPacket);
                        r.supportDroppingMalicious = 1;
                    }
                    else if (nbNode.IsMalicious() && realr.supportDroppingMalicious > 0)
                    {
                        //如果事件是恶意的，那么伪造正常事件
                        r = MODEventTrust.ForgeNormalEventTrustResult(suspectedNodeId, nbNode.Id, pkgIdent, MODEventCategoryType.DropPacket);
                        r.supportDroppingMalicious = -1;
                    }
                    else
                    {
                        r = new MODEventTrustResult(suspectedNodeId, nbId, pkgIdent, MODEventCategoryType.DropPacket, realr.ds);
                        r.supportDroppingMalicious = realr.supportDroppingMalicious;
                    }
                    reports.Add(nbNode, r);
                }
            }
        }

        private MODEventTrustResult GetEventTrustResult(string pkgIdent, MODPhenomemon p, List<MODEventTrustResult> results, 
            int suspectedNodeId, int reportNodeId, MODEventTrustResult realr)
        {
            //0为正常行为，1为伪造异常报告，2为伪造正常报告
            bool forgeReport = false;
            
            MODEventTrustResult result = null;


            if (!this.IsMalicious())
                return realr;
            if (global.debug)
                Console.WriteLine("Reader{0} GetEventTrustResult", this.Id);
                        

            bool isAccept = false;
            bool isSupportM = false;
            bool isCompositeReportSupport = false;
            //如果是恶意节点，则会考察检测节点可能的动作
            if (this.IsMalicious())
            {
                if (global.Step1DeduceMethod == DeduceMethod.Native)//原始的话，恶意节点与真实情况相反
                {
                    forgeReport = true;
                    string sSupport = "";
                    if (global.DropData == false)
                    {
                        if (realr.supportDroppingMalicious < 0)
                            sSupport = "supporting";
                        else
                            sSupport = "nonsupporting";
                    }
                    else// global.DropData == true
                    {
                        if (realr.supportDroppingMalicious > 0)
                            sSupport = "nonsupporting";
                        else
                            sSupport = "supporting";
                    }

                    Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4} {5}{6} is {7} by {8}. [{9}:{10}]\t[{11}:{12}]\t[{13}:{14}]-${15}$:{16}",
                        scheduler.currentTime, "DEDUCTION1-1", this.type, this.Id, sSupport, NodeType.READER, this.Id, "accept", this.Id,
                        0, 0, 1, 1, 1, 1, pkgIdent, "None");
                }
                else if (global.Step1DeduceMethod == DeduceMethod.Game || global.Step1DeduceMethod == DeduceMethod.OrgGame
                    || global.Step1DeduceMethod == DeduceMethod.CoOrgGame)
                {
                    //如果是博弈论，则判断检测节点的观点
                    //此处仅以其周围邻居为参考，而非报告节点的邻居，这是由于ad-hoc的局限性所致的
                    Dictionary<Node, MODEventTrustResult> localcachedresults = new Dictionary<Node, MODEventTrustResult>();
                    foreach (MODEventTrustResult r in results)
                        localcachedresults.Add(Reader.GetReader(r.reportNodeId), r);

                    ReduceReports(localcachedresults);
                    //初始化邻居的结构，且找到最久的邻居
                    int minNbId = GetLongestNormalNeighbor();
                    if (minNbId < 0)//没有正常节点
                        minNbId = GetOneNormalNodeFromReports(localcachedresults);
                    
                    if (minNbId < 0)//还是没有正常节点
                        forgeReport = true;
                    else
                    {
                        Reader minNbNode = global.readers[minNbId];


                        MODEventTrustResult mr = null;
                        if (this.IsMalicious() && realr.supportDroppingMalicious <= 0)
                        {
                            //如果事件是正常的，那么伪造恶意事件
                            mr = MODEventTrust.ForgeMaliciousEventTrustResult(suspectedNodeId, this.Id, pkgIdent, MODEventCategoryType.DropPacket);
                            mr.supportDroppingMalicious = 1;
                        }
                        else if (this.IsMalicious() && realr.supportDroppingMalicious > 0)
                        {
                            //如果事件是恶意的，那么伪造正常事件
                            mr = MODEventTrust.ForgeNormalEventTrustResult(suspectedNodeId, this.Id, pkgIdent, MODEventCategoryType.DropPacket);
                            mr.supportDroppingMalicious = -1;
                        }
                        else
                        {
                            mr = new MODEventTrustResult(suspectedNodeId, this.Id, pkgIdent, MODEventCategoryType.DropPacket, realr.ds);
                            mr.supportDroppingMalicious = realr.supportDroppingMalicious;
                        }
                        localcachedresults.Add(this, mr);


                        AddNeighborReports(minNbNode, realr, suspectedNodeId, pkgIdent, localcachedresults);

                        Node[] reportNodes = localcachedresults.Keys.ToArray();

                        if (!this.pNBNormal.ContainsKey(minNbNode))
                            this.pNBNormal.Add(minNbNode, new Dictionary<Node, double>());
                        if (!this.NBNodeIteractions.ContainsKey(minNbNode))
                            this.NBNodeIteractions.Add(minNbNode, new List<IteratorType>());

                        if (!this.pNBNormal[minNbNode].ContainsKey(this))
                            this.pNBNormal[minNbNode].Add(this, global.pInitNormal);

                        foreach (int nbId in this.Neighbors.Keys)
                        {
                            MODReader nbNode = (MODReader)global.readers[nbId];
                            if (!this.pNBNormal[minNbNode].ContainsKey(nbNode))
                                this.pNBNormal[minNbNode].Add(nbNode, global.pInitNormal);
                        }
                        //有一些节点不是我的邻居，暂且也用nbNode表示吧
                        foreach (Node nbNode in reportNodes)
                        {
                            if (!this.pNBNormal[minNbNode].ContainsKey(nbNode))
                                this.pNBNormal[minNbNode].Add(nbNode, global.pInitNormal);
                        }

                        if (!this.NBNodeTrustWeights.ContainsKey(minNbNode))
                        {
                            this.NBNodeTrustWeights.Add(minNbNode, new Dictionary<Node, double>());
                            this.NBNodeTrustWeightsLastUpdate.Add(minNbNode, new Dictionary<Node, double>());
                            this.NBNodeSuspectCount.Add(minNbNode, new Dictionary<Node, List<double>>());
                            this.NBNodeHistoryMyVariance.Add(minNbNode, new Dictionary<Node, List<double>>());
                            this.NBNodeHistoryTotalVariance.Add(minNbNode, new Dictionary<Node, List<double>>());
                        }

                        Dictionary<Node, double> pNormal = new Dictionary<Node, double>();

                        if (global.Step1DeduceMethod == DeduceMethod.OrgGame || global.Step1DeduceMethod == DeduceMethod.CoOrgGame)
                        {
                            List<DirectTrustEntity>[] orgDirectTrustWeight = new List<DirectTrustEntity>[global.orgNum];
                            for (int i = 0; i < global.orgNum; i++)
                            {
                                orgDirectTrustWeight[i] = new List<DirectTrustEntity>();
                            }
                            Dictionary<Node, MODEventTrustResult> suspectedReportNodes =
                            AdjustNodeTrust(this.NBNodeTrustWeights[minNbNode], this.NBNodeTrustWeightsLastUpdate[minNbNode],
                                this.NBNodeSuspectCount[minNbNode], orgDirectTrustWeight,
                                this.NBNodeHistoryMyVariance[minNbNode], this.NBNodeHistoryTotalVariance[minNbNode],
                                localcachedresults, pkgIdent, minNbNode, suspectedNodeId);

                        }
                        //这个邻居节点对该节点邻居的印象
                        foreach (Node reportNode in reportNodes)
                        {
                            Organization org = global.orgs[((Reader)reportNode).OrgId];
                            if (this.NBNodeTrustWeights[minNbNode].ContainsKey(org))
                                pNormal.Add(reportNode, this.pNBNormal[minNbNode][reportNode] * this.NBNodeTrustWeights[minNbNode][org]);
                            else
                                pNormal.Add(reportNode, this.pNBNormal[minNbNode][reportNode]);
                        }


                        //计算报告节点的live时间，可估算在supportM的条件下，normal的概率
                        if (!this.NBNodeIteractions.ContainsKey(minNbNode))
                            this.NBNodeIteractions.Add(minNbNode, new List<IteratorType>());
                        double pDrop = global.pInitDrop;
                        //SetDropBySupport(ref pDropBySupportM, ref pDropByNonsupportM, this.NBNodeIteractions[minNbNode]);

                        if (global.debug)
                            Console.WriteLine("deducing reports:{0}", reportNodes.Length);
                        //然后模拟计算

                        Deduce2Result d2r = DeduceA2(reportNodes, false, suspectedNodeId, localcachedresults, minNbNode.Id,
                            pDrop, pNormal, this.NBNodeTrustWeights[minNbNode], this.NBNodeTrustWeightsLastUpdate[minNbNode],
                            this.NBNodeIteractions[minNbNode]);
                        isAccept = d2r.IsAccept;
                        isCompositeReportSupport = d2r.IsTotalReportSupport;
                        //此时，真实事件是否正常已知，自己的性质，检测节点的最佳行为已知，但是由于整体报告的性质可能出现变化，
                        //设有共有n个节点，m个正常节点，那么如果少于p-m/2个恶意节点改变自己的报告，则整体报告维持不变，否则整体报告改变。
                        isSupportM = DeduceA1(realr.supportDroppingMalicious <= 0, isCompositeReportSupport, isAccept);

                        if (realr.supportDroppingMalicious > 0 && !isSupportM)
                            forgeReport = true;
                        else if (realr.supportDroppingMalicious <= 0 && isSupportM)
                            forgeReport = true;
                        else
                            forgeReport = false;
                    }
                }
                else
                {
                    forgeReport = true;
                }

            }

            //恶意节点
            if(forgeReport == false)
            {
                Console.WriteLine("READER{0} not forge a report", this.Id);
                return realr;
            }
            else if(forgeReport == true)
            {
                if (realr.supportDroppingMalicious > 0)//真实事件是恶意的
                {
                    Console.WriteLine("READER{0} forge a normal report", this.Id);
                    result = MODEventTrust.ForgeNormalEventTrustResult(suspectedNodeId, this.Id,
                        pkgIdent, MODEventCategoryType.DropPacket);
                }
                else//真实事件是正常的
                {
                    Console.WriteLine("READER{0} forge a malicious report", this.Id);
                    result = MODEventTrust.ForgeMaliciousEventTrustResult(suspectedNodeId, this.Id,
                        pkgIdent, MODEventCategoryType.DropPacket);
                }
                return result;
            }
            return null;
        }

        /*
        void SetDropBySupport(ref double pDropBySupportM, ref double pDropByNonsupportM, Dictionary<Node, List<IteratorType>> iterations)
        {
            double pInitDropBySupportM = global.pInitDropBySupportM;
            double pInitDropByNonsupportM = global.pInitDropByNonsupportM;

            double x1 = 0, x2 = 0;
            foreach (KeyValuePair<Node, List<IteratorType>> k in iterations)
            {
                int count = k.Value.Count;
                x1 += (global.pMaxDropBySupportM - pInitDropBySupportM) *
                    (1 - Math.Pow(Math.E, (0 - count) * global.pDropBySupportMFactor)) + pInitDropBySupportM;
                x2 += (pInitDropByNonsupportM - global.pMinDropByNonsupportM) *
                    Math.Pow(Math.E, (0 - count) * global.pDropBySupportMFactor) + global.pMinDropByNonsupportM;
            }
            pDropBySupportM = x1 / iterations.Count;
            pDropByNonsupportM = x2 / iterations.Count;
        }*/


        //TODO
        void CalculateBelief(ref double pSupportMByDropAndGroupNormal, ref double pSupportMByFwrdAndGroupNormal,
                ref double pSupportMByDropAndGroupMalicious, ref double pSupportMByFwrdAndGroupMalicious,
                ref double pNonsupportMByDropAndGroupNormal, ref double pNonsupportMByFwrdAndGroupNormal,
                ref double pNonsupportMByDropAndGroupMalicious, ref double pNonsupportMByFwrdAndGroupMalicious, List<IteratorType> its)
        {
            int SupportMAndDropAndGroupNormal = 3, 
                SupportMAndFwrdAndGroupNormal = 1, 
                SupportMAndDropAndGroupMalicious = 1,
                SupportMAndFwrdAndGroupMalicious = 3, 
                NonsupportMAndDropAndGroupNormal = 1, 
                NonsupportMAndFwrdAndGroupNormal = 3, 
                NonsupportMAndDropAndGroupMalicious = 3,
                NonsupportMAndFwrdAndGroupMalicious = 1;
            int DropAndGroupNormal = 4, FwrdAndGroupNormal = 4, FwrdAndGroupMalicious = 4, DropAndGroupMalicious = 4;



            foreach (IteratorType it in its)
            {
                if (it.DropEvent && it.NormalNode)
                    DropAndGroupNormal++;
                if (it.DropEvent && !it.NormalNode)
                    DropAndGroupMalicious++;
                if (!it.DropEvent && it.NormalNode)
                    FwrdAndGroupNormal++;
                if (!it.DropEvent && !it.NormalNode)
                    FwrdAndGroupMalicious++;

                if (it.DropEvent && it.NormalNode && it.ReportSupportM)
                    SupportMAndDropAndGroupNormal++;
                if (it.DropEvent && it.NormalNode && !it.ReportSupportM)
                    NonsupportMAndDropAndGroupNormal++;
                if (it.DropEvent && !it.NormalNode && it.ReportSupportM)
                    SupportMAndDropAndGroupMalicious++;
                if (it.DropEvent && !it.NormalNode && !it.ReportSupportM)
                    NonsupportMAndDropAndGroupMalicious++;
                if (!it.DropEvent && it.NormalNode && it.ReportSupportM)
                    SupportMAndFwrdAndGroupNormal++;
                if (!it.DropEvent && it.NormalNode && !it.ReportSupportM)
                    NonsupportMAndFwrdAndGroupNormal++;
                if (!it.DropEvent && !it.NormalNode && it.ReportSupportM)
                    SupportMAndFwrdAndGroupMalicious++;
                if (!it.DropEvent && !it.NormalNode && !it.ReportSupportM)
                    NonsupportMAndFwrdAndGroupMalicious++;

            }

            pSupportMByDropAndGroupNormal = (double)SupportMAndDropAndGroupNormal/DropAndGroupNormal;
            pSupportMByFwrdAndGroupNormal = (double)SupportMAndFwrdAndGroupNormal / FwrdAndGroupNormal;
            pSupportMByDropAndGroupMalicious = (double)SupportMAndDropAndGroupMalicious / DropAndGroupMalicious;
            pSupportMByFwrdAndGroupMalicious = (double)SupportMAndFwrdAndGroupMalicious / FwrdAndGroupMalicious;
            pNonsupportMByDropAndGroupNormal = (double)NonsupportMAndDropAndGroupNormal / DropAndGroupNormal;
            pNonsupportMByFwrdAndGroupNormal = (double)NonsupportMAndFwrdAndGroupNormal / FwrdAndGroupNormal;
            pNonsupportMByDropAndGroupMalicious = (double)NonsupportMAndDropAndGroupMalicious / DropAndGroupMalicious;
            pNonsupportMByFwrdAndGroupMalicious = (double)NonsupportMAndFwrdAndGroupMalicious / FwrdAndGroupMalicious;

        }


        //将接收到的数据包添加到观察到的现象中
        public void AddReceivePacketPhenomemon(Packet pkg)
        {
            MODPhenomemon p;
            this.totalReceivedPackets++;
            //忽略广播包(从实际来看，发送广播包的一般是节点本身的行为，不需要考虑其对数据包的恶意操作)
            if (pkg.Next == BroadcastNode.Node.Id)
                return;


            //如果不是我需要监控的节点，返回(在真实丢包的场景中，需要监控所有节点，否则只需要监控特定的节点即可)
            if (!global.monitoredNodes.Contains(pkg.Prev) && !global.monitoredNodes.Contains(pkg.Next) && global.DropData == false)
                return;

            if (!global.monitoredNodes.Contains(pkg.Next))
                global.monitoredNodes.Add(pkg.Next);


            //记录发送现象
            if (pkg.Next != BroadcastNode.Node.Id)
            {
                p = new MODPhenomemon(MODPhenomemonType.SEND_PACKET, pkg.Prev, scheduler.currentTime, pkg);
                p.likehood = global.sendLikehood;
                this.observedPhenomemons.Add(p);
                if (global.debug)
                    Console.WriteLine("[Debug] reader{0} add a RECV phenomemon of reader{1}", Id, pkg.Next);

            }

            //记录接收现象
            if (pkg.Next != pkg.Dst)
            {
                p = new MODPhenomemon(MODPhenomemonType.RECV_PACKET, pkg.Next, scheduler.currentTime, pkg);
                p.likehood = global.recvLikehood;
                this.observedPhenomemons.Add(p);
                //Console.WriteLine("[Debug] reader{0} add a SEND phenomemon of reader{1}", id, pkg.Prev);

                //延迟检查事件
                Event.AddEvent(new Event(Scheduler.getInstance().currentTime + global.checkReceivedPacketTimeout, EventType.CHK_RECV_PKT, this, p));
            }

            //数据包到达目的地，忽略
        }


        //恶意观察节点推断采取的行为
        //isAccept是接受事件是恶意的
        public bool DeduceA1(bool normalEvent, bool isCompositeReportSupportM, bool isAccept)
        {
            bool isSupportM = false;
            double aSupportM = 0, aNonsupportM = 0;

            aSupportM = MODEventTrust.A1Fun(normalEvent, true, isCompositeReportSupportM, isAccept);
            aNonsupportM = MODEventTrust.A1Fun(normalEvent, false, isCompositeReportSupportM, isAccept);

            isSupportM = (aSupportM > aNonsupportM);
            return isSupportM;
        }

        public Dictionary<Node, MODEventTrustResult> AdjustNodeTrust(Dictionary<Node, double> localNodeTrustWeights, 
            Dictionary<Node, double> localNodeTrustWeightsLastUpdate, Dictionary<Node, List<double>> localNodeSuspectCount,
            List<DirectTrustEntity>[] localOrgDirectTrustWeights, 
            Dictionary<Node, List<double>> localNodeHistoryMyVariance, Dictionary<Node, List<double>> localNodeHistoryTotalVariance,
            Dictionary<Node, MODEventTrustResult> cachedresults, string pkgIdent, Reader deducingNode, int suspectNodeId)
        {
            Node[] reportNodes = cachedresults.Keys.ToArray();
            MODEventTrustResult myReport = cachedresults[deducingNode];
            Dictionary<int, HashSet<int>> orgMapping = new Dictionary<int, HashSet<int>>();
            Dictionary<Node, MODEventTrustResult> suspectedReportOrgs = new Dictionary<Node, MODEventTrustResult>();
            Dictionary<Node, MODEventTrustResult> finalSuspectedReportNodes = new Dictionary<Node, MODEventTrustResult>();

            bool isNB = deducingNode.Neighbors.ContainsKey(suspectNodeId);

            foreach (Node reportNode in reportNodes)
            {
                Reader reportReader = ((Reader)reportNode);
                if (!orgMapping.ContainsKey(reportReader.OrgId))
                {
                    orgMapping.Add(reportReader.OrgId, new HashSet<int>());
                }
                if (!orgMapping[reportReader.OrgId].Contains(reportReader.Id))
                    orgMapping[reportReader.OrgId].Add(reportReader.Id);
            }

            Dictionary<Node, MODEventTrustResult> orgReports = new Dictionary<Node, MODEventTrustResult>();
            //计算每个机构的一致性
            foreach (KeyValuePair<int, HashSet<int>> k in orgMapping)
            {
                int orgId = k.Key;
                Organization org = global.orgs[orgId];
                HashSet<int> nodeIds = k.Value;

                List<MODEventTrustResult> nodeResults = new List<MODEventTrustResult>();
                foreach (int nodeId in nodeIds)
                {
                    nodeResults.Add(cachedresults[global.readers[nodeId]]);
                }

                MODEventTrustResult orgReport = MODEventTrust.MergeMaliciousEventTrustResult(suspectNodeId, org,
                    nodeResults, pkgIdent, MODEventCategoryType.DropPacket);
                MODEventTrust.CalculateRelativeMaliciousEventTrustResult(orgReport, myReport);
                orgReports.Add(org, orgReport);
            }

            MODEventTrustResult totalOrgReport = MODEventTrust.MergeMaliciousEventTrustResult(suspectNodeId, MODOrganization.totalOrg,
                orgReports.Values.ToList(), pkgIdent, MODEventCategoryType.DropPacket);


            //初始化机构信任值
            for (int i = 0; i < global.orgNum; i++)
            {
                Organization org = global.orgs[i];
                List<DirectTrustEntity> temp = new List<DirectTrustEntity>();
                double total = 0;
                foreach (DirectTrustEntity e in localOrgDirectTrustWeights[org.Id])
                {
                    if (e.time < scheduler.currentTime - 0.1f)
                    {
                        temp.Add(e);
                        total += e.value;
                    }
                }
                if (!localNodeTrustWeights.ContainsKey(org))
                {
                    if (localOrgDirectTrustWeights[org.Id].Count>0)
                        localNodeTrustWeights.Add(org, total / localOrgDirectTrustWeights[org.Id].Count);
                    else
                        localNodeTrustWeights.Add(org, 1f);
                    localNodeTrustWeightsLastUpdate.Add(org, scheduler.currentTime);
                }
                else
                {
                    //如果是机构的话，则将其信誉提高一些，作为遗忘因子
                    //7s恢复0.1
                    localNodeTrustWeights[org] = Math.Min(1.0f,
                        localNodeTrustWeights[org] + 0.1 * (scheduler.currentTime - localNodeTrustWeightsLastUpdate[org]) / 6);
                    if (localOrgDirectTrustWeights[org.Id].Count > 0)
                    {
                        localNodeTrustWeights[org] = 0.3 * localNodeTrustWeights[org] + 
                            0.7 * total / localOrgDirectTrustWeights[org.Id].Count;
                    }
                    localNodeTrustWeightsLastUpdate[org] = scheduler.currentTime;
                }
                foreach (DirectTrustEntity e in temp)
                {
                    localOrgDirectTrustWeights[org.Id].Remove(e);
                }
            }

            double variance = MODEventTrust.CalculateRelativeMaliciousEventTrustResult(orgReports.Values.ToList());
            //if (totalOrgReport.variance > global.MaxTotalOrgVariance)
            if (variance > global.MaxTotalOrgVariance)
            {
                    //找到所有与自己差别较大的机构
                foreach (KeyValuePair<int, HashSet<int>> k in orgMapping)
                {
                    int orgId = k.Key;
                    Organization org = global.orgs[orgId];

                    //是否存在同机构的报告节点
                    bool hasPeer = orgReports.ContainsKey(global.orgs[deducingNode.OrgId]);
                    MODEventTrustResult refReport = null;


                    if (isNB || hasPeer)
                    {
                        if (isNB)
                            refReport = myReport;
                        else if (hasPeer)
                            refReport = orgReports[global.orgs[deducingNode.OrgId]];

                        double supportMe = 0, nonsupportMe = 0;
                        foreach (int nodeId in orgMapping[org.Id])
                        {
                            Node node = global.readers[nodeId];
                            double myVarianceDist = MODEventTrust.CalculateRelativeMaliciousEventTrustResult(cachedresults[node], refReport);
                            if (myVarianceDist > global.MaxReportDistance)
                                nonsupportMe++;
                            else
                                supportMe++;
                        }

                        if ((supportMe / (supportMe + nonsupportMe) < 0.5f && nonsupportMe>1) || nonsupportMe > 3)
                        {
                            suspectedReportOrgs.Add(org, orgReports[org]);
                            finalSuspectedReportNodes.Add(org, orgReports[org]);
                        }
                        //double dist = MODEventTrust.Distance(myReport, orgReports[org]);
                    }
                    //非邻居
                    else
                    {
                        //orgReports[org].myVarianceDist = MODEventTrust.CalculateRelativeMaliciousEventTrustResult(orgReports[org], myReport);                    
                        orgReports[org].totalVarianceDist = MODEventTrust.CalculateRelativeMaliciousEventTrustResult(orgReports[org], totalOrgReport);
                        if (orgReports[org].totalVarianceDist > global.MaxReportDistance)
                        {
                            suspectedReportOrgs.Add(org, orgReports[org]);
                            finalSuspectedReportNodes.Add(org, orgReports[org]);
                        }
                    }
                }

                //找到所有可疑的报告节点
                foreach (KeyValuePair<int, HashSet<int>> k in orgMapping)
                {
                    int orgId = k.Key;
                    Organization org = global.orgs[orgId];
                    if (suspectedReportOrgs.ContainsKey(org))
                        continue;
                    foreach (int nodeId in orgMapping[orgId])
                    {
                        Node node = global.readers[nodeId];

                        cachedresults[node].myVarianceDist = MODEventTrust.CalculateRelativeMaliciousEventTrustResult(cachedresults[node], myReport);
                        cachedresults[node].totalVarianceDist = MODEventTrust.CalculateRelativeMaliciousEventTrustResult(cachedresults[node], totalOrgReport);
                        if (isNB && cachedresults[node].myVarianceDist > global.MaxReportDistance)
                        {
                            //if (MODEventTrust.Distance(myReport, cachedresults[node]) > global.MaxReportDistance)
                            finalSuspectedReportNodes.Add(node, cachedresults[node]);
                            continue;
                        }
                        else if (cachedresults[node].totalVarianceDist > global.MaxReportDistance)
                        {
                            finalSuspectedReportNodes.Add(node, cachedresults[node]);
                        }
                        
                    }
                }
                                
                //所有节点添加历史相关度
                foreach (KeyValuePair<Node, MODEventTrustResult> k in cachedresults)
                {
                    Node node = k.Key;
                    if (!localNodeHistoryMyVariance.ContainsKey(node))
                        localNodeHistoryMyVariance.Add(node, new List<double>());
                    if(isNB)
                        localNodeHistoryMyVariance[node].Add(k.Value.myVarianceDist);
                    if (!localNodeHistoryTotalVariance.ContainsKey(node))
                        localNodeHistoryTotalVariance.Add(node, new List<double>());
                    localNodeHistoryMyVariance[node].Add(k.Value.totalVarianceDist);
                }
                foreach (KeyValuePair<Node, MODEventTrustResult> k in suspectedReportOrgs)
                {
                    Node node = k.Key;
                    if (!localNodeHistoryMyVariance.ContainsKey(node))
                        localNodeHistoryMyVariance.Add(node, new List<double>());
                    if (isNB)
                        localNodeHistoryMyVariance[node].Add(k.Value.myVarianceDist);
                    if (!localNodeHistoryTotalVariance.ContainsKey(node))
                        localNodeHistoryTotalVariance.Add(node, new List<double>());
                    localNodeHistoryMyVariance[node].Add(k.Value.totalVarianceDist);
                }


                //计算将可疑机构或节点的信任值
                foreach (KeyValuePair<Node, MODEventTrustResult> k in finalSuspectedReportNodes)
                {
                    Node node = k.Key;
                    double myVarianceDist = k.Value.myVarianceDist;


                    double freq = 1;
                    if (!localNodeSuspectCount.ContainsKey(node))
                        localNodeSuspectCount.Add(node, new List<double>());
                    
                    //初始化节点信任值，机构信任值已经初始化完毕
                    if (!localNodeTrustWeights.ContainsKey(node) && node.type == NodeType.READER)
                    {
                        localNodeTrustWeights.Add(node, 1f);
                        localNodeTrustWeightsLastUpdate.Add(node, scheduler.currentTime);
                    }

                    if (node.type == NodeType.READER)//考察节点短期的行为，则置1
                        localNodeTrustWeights[node] = 1f;

                    localNodeSuspectCount[node].Add(scheduler.currentTime);
                    while (localNodeSuspectCount[node].Count>0)//删除首部过时的恶意报告
                    {
                        if (localNodeSuspectCount[node][0] < scheduler.currentTime - global.maxCountPeriod)
                            localNodeSuspectCount[node].RemoveAt(0);
                        else
                            break;
                    }
                    freq = localNodeSuspectCount[node].Count;

                    //这里惩罚节点有三个条件，一个是与自己的相关度（惩罚小的），另一个是被怀疑的频率（惩罚大的），还有一个是历史的相关度变化（惩罚变化频繁的）
                    
                    //考察历史相关度偏移程度
                    double historyVarianceStandardDeviation = 1;
                    if (isNB && localNodeHistoryMyVariance[node].Count > 2)//曲线至少有三个点
                    {
                        //使历史记录数量维持在MaxHistoryCount之内
                        if (localNodeHistoryMyVariance[node].Count > global.MaxHistoryCount)
                            localNodeHistoryMyVariance[node].RemoveRange(0, localNodeHistoryMyVariance[node].Count - global.MaxHistoryCount);

                        double[] arcs = new double[localNodeHistoryMyVariance[node].Count];
                        for (int i = 0; i < localNodeHistoryMyVariance[node].Count; i++)
                            arcs[i] = 0.1f * i;

                        LinearRegression reg = new LinearRegression();
                        reg.BuildLSMCurve(arcs, localNodeHistoryMyVariance[node], 1, false);
                        //考察reg.CoefficientsStandardError
                        historyVarianceStandardDeviation = reg.StandardDeviation;
                    }
                    else if (localNodeHistoryTotalVariance[node].Count > 2)//曲线至少有三个点
                    {
                        //使历史记录数量维持在MaxHistoryCount之内
                        if (localNodeHistoryTotalVariance[node].Count > global.MaxHistoryCount)
                            localNodeHistoryTotalVariance[node].RemoveRange(0, localNodeHistoryTotalVariance[node].Count - global.MaxHistoryCount);

                        double[] arcs = new double[localNodeHistoryTotalVariance[node].Count];
                        for (int i = 0; i < localNodeHistoryTotalVariance[node].Count; i++)
                            arcs[i] = 0.1f * i;

                        LinearRegression reg = new LinearRegression();
                        reg.BuildLSMCurve(arcs, localNodeHistoryTotalVariance[node], 1, false);
                        //考察reg.CoefficientsStandardError
                        historyVarianceStandardDeviation = reg.StandardDeviation; 
                    }
                    double vv = isNB? Math.Pow(global.VarianceBase, myVarianceDist):1;

                    localNodeTrustWeights[node] *= global.AdjustFactor /
                        ((Math.Log(freq + 1, global.SuspectedCountBase) + 1) * vv
                        * (Math.Log(historyVarianceStandardDeviation, global.HistoryVSDBase) + 1));
                    if (localNodeTrustWeights[node] > 1)
                        localNodeTrustWeights[node] = 0.99f;
                    else if(localNodeTrustWeights[node] <=0)
                        localNodeTrustWeights[node] = 0.01f;

                }
            }
            return finalSuspectedReportNodes;
        }

        void PrintReports(Dictionary<Node, MODEventTrustResult> reports)
        {
            Console.Write("{0:F4} [{1}] READER{2}\t", scheduler.currentTime, "REPORTS", this.Id);
            foreach (KeyValuePair<Node, MODEventTrustResult> k in reports)
            {
                Console.Write("{0}:{1}\t", k.Key, k.Value.supportDroppingMalicious);
            }
            Console.WriteLine();
        }


        private void ReduceReports(Dictionary<Node, MODEventTrustResult> results)
        {
            //如果超过最大值，则正常和异常节点各去掉一个
            while (results.Count > global.MaxReportCount)
            {
                Node n1 = null, n2 = null;
                //先找两方都不确定的
                bool f1 = false, f2 = false;
                foreach (KeyValuePair<Node, MODEventTrustResult> k in results)
                {
                    if(MODEventTrust.getDist(k.Value.ds) > 0.20)
                        continue;
                    if (!f1 && n1 == null && k.Value.supportDroppingMalicious > 0 && k.Key != this)
                        n1 = k.Key;
                    else if (!f2 && n2 == null && k.Value.supportDroppingMalicious <= 0 && k.Key != this)
                        n2 = k.Key;
                }
                foreach (KeyValuePair<Node, MODEventTrustResult> k in results)
                {
                    if (!f1 && n1 == null && k.Value.supportDroppingMalicious > 0 && k.Key != this)
                        n1 = k.Key;
                    else if (!f2 && n2 == null && k.Value.supportDroppingMalicious <= 0 && k.Key != this)
                        n2 = k.Key;
                }

                if (n1 != null)
                    results.Remove(n1);
                if (n2 != null)
                    results.Remove(n2);
            }
        }

        //对最终邻居的结果进行分析
        public void DeduceEventType(string pkgIdent)
        {
            //清除过期的现象
            List<string> outdatedReports = new List<string>();
            foreach (KeyValuePair<string, Dictionary<Node, MODEventTrustResult>> k in this.receivedEventReports)
            {
                double timeStamp = k.Value.First().Value.timeStamp;
                if (scheduler.currentTime - timeStamp > 8)
                    outdatedReports.Add(k.Key);
            }
            foreach (string o in outdatedReports)
            {
                this.receivedEventReports.Remove(o);
            }

            if(MODEventTrustResult.DeducedPackets.Contains(pkgIdent))
                return;
            //Console.WriteLine("Reader{0} Deduces Event Type for {1}", this.Id, pkgIdent);
            
            Dictionary<Node, MODEventTrustResult> cachedresults = null;
            if (this.receivedEventReports.ContainsKey(pkgIdent))
                cachedresults = this.receivedEventReports[pkgIdent];
            else
                throw new Exception("no such an ident in receivedEventReports");

            int suspectedNodeId = cachedresults.First().Value.nodeId;
            if (!cachedresults.ContainsKey(this))
            {
                MODEventTrustResult myresult = GetEventTrustResult(pkgIdent, null, cachedresults.Values.ToList(), suspectedNodeId, this.Id); ;
                cachedresults.Add(this, myresult);
            }


            ReduceReports(cachedresults);
            PrintReports(cachedresults);


            if (global.Step2DeduceMethod == DeduceMethod.Native)
            {
                int supportMCount = 0, nonsupportMCount = 0;
                foreach (KeyValuePair<Node, MODEventTrustResult> r in cachedresults)
                {
                    //如果该节点对正常还是恶意均不确定，那还是算了
                    if(MODEventTrust.getDist(r.Value.ds)< global.ReportMinDist)
                        continue;
                    if (r.Value.supportDroppingMalicious > 0)
                        supportMCount++;
                    else
                        nonsupportMCount++;

                    //Console.Write("{0}:{1}\t",r.Key, r.Value.normal);
                }


                string sAccept = "", sResult = "", sSupport = "";
                bool isAccept = false, isReportSupportM = false;
                if (supportMCount > nonsupportMCount)
                {
                    isReportSupportM = true;
                    sSupport = "supporting";
                }
                else
                {
                    isReportSupportM = false;
                    sSupport = "nonsupporting";
                }
                isAccept = true;
                sAccept = "accept";

                bool duduceIsNormal = (isReportSupportM ^ isAccept);

                //这里由于真实事件都是正常的，所以直接用true代替,ps:DropData代表正常场景下丢包
                sResult = (duduceIsNormal != global.DropData) ? "Succ" : "Fail";

                Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4} {5}{6} is {7} by {8}. [{9}:{10}]\t[{11}:{12}]\t[{13}:{14}]-${15}$:{16}",
                    scheduler.currentTime, "DEDUCTION1-2", this.type, this.Id, sSupport, NodeType.READER, suspectedNodeId, sAccept, this.Id,
                    0, 0, 1, 1, supportMCount, nonsupportMCount, pkgIdent, sResult);

            }
            else //博弈论的方法
            {
                //自己的记录是否存在
                if (!cachedresults.ContainsKey(this))
                {
                    throw new Exception("myresult result is null");
                }

                MODEventTrustResult myReport = cachedresults[this];

                //计算每个节点的先验概率和条件概率
                foreach (KeyValuePair<Node, MODEventTrustResult> k in cachedresults)
                {
                    if (!this.pNormal.ContainsKey(k.Key))
                        this.pNormal.Add(k.Key, global.pInitNormal);
                }
                Node[] reportNodes = cachedresults.Keys.ToArray();

                //朴素博弈论
                if (global.Step2DeduceMethod == DeduceMethod.Game)
                {
                    reportNodes = cachedresults.Keys.ToArray();

                    double pDrop = global.pInitDrop;
                    //SetDropBySupport(ref pDropBySupportM, ref pDropByNonsupportM, this.nodeIteractions);

                    Deduce2Result d2r = DeduceA2(reportNodes, true, suspectedNodeId, cachedresults, this.Id,
                        pDrop, this.pNormal, this.nodeTrustWeights, this.nodeTrustWeightsLastUpdate, this.nodeIteractions);
                    bool isAccept = d2r.IsAccept;

                }
                else if (global.Step2DeduceMethod == DeduceMethod.OrgGame || global.Step2DeduceMethod == DeduceMethod.CoOrgGame)
                {
                    //这里的pNormal是更新后的了
                    Dictionary<Node, double> pNormal = new Dictionary<Node, double>();

                    Dictionary<Node, MODEventTrustResult> suspectedReportNodes =
                        AdjustNodeTrust(this.nodeTrustWeights, this.nodeTrustWeightsLastUpdate, this.nodeSuspectCount, 
                        this.orgDirectTrustWeights, this.nodeHistoryMyVariance, this.nodeHistoryTotalVariance,
                        cachedresults, pkgIdent, this, suspectedNodeId);
                    
                    foreach (Node reportNode in reportNodes)
                    {
                        Organization org = global.orgs[((Reader)reportNode).OrgId];

                        //机构的性质暗示了节点的先验概率
                        if (this.nodeTrustWeights.ContainsKey(org))
                            pNormal.Add(reportNode, this.pNormal[reportNode] * this.nodeTrustWeights[org]);
                        else
                            pNormal.Add(reportNode, this.pNormal[reportNode]);
                    }

                    double pDrop = global.pInitDrop;

                    Deduce2Result d2r = DeduceA2(reportNodes, true, suspectedNodeId, cachedresults, this.Id,
                        pDrop, pNormal, this.nodeTrustWeights, this.nodeTrustWeightsLastUpdate, this.nodeIteractions);

                    bool isAccept = d2r.IsAccept;


                    //如果没有与事件怀疑节点不是邻居，则参考
                    if (global.Step2DeduceMethod == DeduceMethod.CoOrgGame && this.Neighbors.ContainsKey(suspectedNodeId))
                    {
                        foreach (int nbId in this.Neighbors.Keys)
                        {
                            MODReader nbReader = (MODReader)global.readers[nbId];
                            if (nbReader.Neighbors.ContainsKey(suspectedNodeId))
                                continue;

                            for (int i = 0; i < global.orgNum; i++)
                            {
                                Organization org = global.orgs[i];
                                if (!this.nodeSuspectCount.ContainsKey(org) || this.nodeSuspectCount[org].Count < 1)
                                    continue;
                                DirectTrustEntity entity = new DirectTrustEntity();
                                entity.value = this.nodeTrustWeights[org];
                                entity.time = scheduler.currentTime;
                                nbReader.orgDirectTrustWeights[org.Id].Add(entity);
                            }
                        }
                    }
                }
                else
                    throw new Exception("Unknown deduce type");
            }

            //Console.WriteLine("end DeduceEventType");

            //需要所有节点都进行判断，所以就不添加这句话了
            //MODEventTrustResult.DeducedPackets.Add(pkgIdent);
            return;
        }

        public int GetLongestNormalNeighbor()
        {
            int minNbId = -1;
            double minBeacon = 10000;
            foreach (int nbId in this.Neighbors.Keys)
            {
                MODReader nbNode = (MODReader)global.readers[nbId];
                if (minBeacon > this.Neighbors[nbId].firstBeacon && !((MODReader)global.readers[nbId]).IsMalicious())
                {
                    minBeacon = Neighbors[nbId].firstBeacon;
                    minNbId = nbId;
                }
            }
            return minNbId;
        }


        public Deduce2Result DeduceA2(Node[] reportNodes, bool isDeduceStep2, int suspectedNodeId, Dictionary<Node, MODEventTrustResult> cachedresults, 
            int deduceNodeId, double pDrop, Dictionary<Node, double> pNormal, Dictionary<Node, double> localNodeTrustWeights,
            Dictionary<Node, double> localNodeTrustWeightsLastUpdate, List<IteratorType> iteractions)
        {
            string pkgIdent = cachedresults.First().Value.eventIdent;

            foreach (Node node in reportNodes)
            {
                if (!localNodeTrustWeights.ContainsKey(node))//如果没有记录，则设其初始权重为1
                {
                    localNodeTrustWeights.Add(node, 1f);
                    localNodeTrustWeightsLastUpdate.Add(node, scheduler.currentTime);
                }
                Organization org = global.orgs[((Reader)node).OrgId];
                if (!localNodeTrustWeights.ContainsKey(org))
                {
                    localNodeTrustWeights.Add(org, 1f);
                    localNodeTrustWeightsLastUpdate.Add(org, scheduler.currentTime);
                }
            }
            foreach (Organization org in global.orgs)
            {
                if (!localNodeTrustWeights.ContainsKey(org))
                {
                    localNodeTrustWeights.Add(org, 1f);
                    localNodeTrustWeightsLastUpdate.Add(org, scheduler.currentTime);
                }
            }

            double supportMWeight = 0, nonsupportMWeight = 0;
            double supportMNodes = 0, nonsupportMNodes = 0;
            bool isReportSupportM = false;
            //计算报告是support还是nonsupport
            foreach (KeyValuePair<Node, MODEventTrustResult> k in cachedresults)
            {
                Node node = k.Key;
                MODEventTrustResult result = k.Value;

                if (MODEventTrust.getDist(result.ds) < global.ReportMinDist)
                    continue;

                Organization org = global.orgs[((Reader)node).OrgId];
                if (result.supportDroppingMalicious > 0)
                {
                    supportMWeight += localNodeTrustWeights[node] * localNodeTrustWeights[org];
                    supportMNodes++;
                }
                else
                {
                    nonsupportMWeight += localNodeTrustWeights[node] * localNodeTrustWeights[org];
                    nonsupportMNodes++;
                }
            }

            if (supportMWeight > nonsupportMWeight)
                isReportSupportM = true;
            else
                isReportSupportM = false;


            //double pGroupNormalBySupportM = 0, pGroupMaliciousBySupportM = 0;
            //double pGroupNormalByNonsupportM = 0, pGroupMaliciousByNonsupportM = 0;
            double pGroupNormal = 0;
            Combination c = new Combination();
            //只计算占优势的一方,temp是优势方
            //for (int m = reportNodes.Length / 2 + 1; m < reportNodes.Length; m++)
            for (int m = 0; m <= reportNodes.Length; m++)
            {
                List<Node[]> lists = c.combination(reportNodes, m);

                //求pGroupNormal
                for (int i = 0; i < lists.Count; i++)
                {
                    Node[] list = (Node[])lists[i];
                    double w1 = 0, w2 = 0;
                    HashSet<Node> set1 = new HashSet<Node>();
                    HashSet<Node> set2 = new HashSet<Node>();

                    foreach (Node node in list)
                    {
                        Organization org = global.orgs[((Reader)node).OrgId];
                        set1.Add(node);
                        w1 += localNodeTrustWeights[node]*localNodeTrustWeights[org];
                    }

                    foreach (Node node in reportNodes)//找出相反的节点
                    {
                        if (set1.Contains(node))
                            continue;
                        Organization org = global.orgs[((Reader)node).OrgId];
                        set2.Add(node);
                        w2 += localNodeTrustWeights[node] * localNodeTrustWeights[org];
                    }
                    if (w1 < w2)//如果权重和不够，则skip
                        continue;
                    //剩下的都是set1权值大于set2的情况

                    double p1 = 1;
                    foreach (Node node in set1)
                    {
                        p1 = p1 * pNormal[node];
                    }
                    foreach (Node node in set2)//找出相反的节点
                    {
                        double pMalicious = 1 - pNormal[node];
                        p1 = p1 * pMalicious;
                    }
                    pGroupNormal += p1;
                }
            }
            

            double aAccept = 0, aReject = 0;
            bool isAccept = false;

            double pFwrd = 1 - pDrop;
            double pGroupMalicious = 1 - pGroupNormal;


            double pSupportMByDropAndGroupNormal= 0, pSupportMByFwrdAndGroupNormal = 0, pSupportMByDropAndGroupMalicious = 0,
                pSupportMByFwrdAndGroupMalicious = 0, pNonsupportMByDropAndGroupNormal = 0, pNonsupportMByFwrdAndGroupNormal = 0,
                pNonsupportMByDropAndGroupMalicious = 0, pNonsupportMByFwrdAndGroupMalicious = 0;

            CalculateBelief(ref pSupportMByDropAndGroupNormal, ref pSupportMByFwrdAndGroupNormal,
                ref pSupportMByDropAndGroupMalicious, ref pSupportMByFwrdAndGroupMalicious,
                ref pNonsupportMByDropAndGroupNormal, ref pNonsupportMByFwrdAndGroupNormal,
                ref pNonsupportMByDropAndGroupMalicious, ref pNonsupportMByFwrdAndGroupMalicious, iteractions);


            double uA2DropAndNormalAndSupportMDAndAccept = global.uA2DropAndNormalAndSupportMDAndAccept;
            double uA2FwrdAndNormalAndSupportMDAndAccept = global.uA2FwrdAndNormalAndSupportMDAndAccept;
            double uA2DropAndMaliciousAndSupportMDAndAccept = global.uA2DropAndMaliciousAndSupportMDAndAccept;
            double uA2FwrdAndMaliciousAndSupportMDAndAccept = global.uA2FwrdAndMaliciousAndSupportMDAndAccept;
            double uA2DropAndNormalAndSupportMDAndReject = global.uA2DropAndNormalAndSupportMDAndReject;
            double uA2FwrdAndNormalAndSupportMDAndReject = global.uA2FwrdAndNormalAndSupportMDAndReject;
            double uA2DropAndMaliciousAndSupportMDAndReject = global.uA2DropAndMaliciousAndSupportMDAndReject;
            double uA2FwrdAndMaliciousAndSupportMDAndReject = global.uA2FwrdAndMaliciousAndSupportMDAndReject;
            double uA2DropAndNormalAndNonsupportMDAndAccept = global.uA2DropAndNormalAndNonsupportMDAndAccept;
            double uA2FwrdAndNormalAndNonsupportMDAndAccept = global.uA2FwrdAndNormalAndNonsupportMDAndAccept;
            double uA2DropAndMaliciousAndNonsupportMDAndAccept = global.uA2DropAndMaliciousAndNonsupportMDAndAccept;
            double uA2FwrdAndMaliciousAndNonsupportMDAndAccept = global.uA2FwrdAndMaliciousAndNonsupportMDAndAccept;
            double uA2DropAndNormalAndNonsupportMDAndReject = global.uA2DropAndNormalAndNonsupportMDAndReject;
            double uA2FwrdAndNormalAndNonsupportMDAndReject = global.uA2FwrdAndNormalAndNonsupportMDAndReject;
            double uA2DropAndMaliciousAndNonsupportMDAndReject = global.uA2DropAndMaliciousAndNonsupportMDAndReject;
            double uA2FwrdAndMaliciousAndNonsupportMDAndReject = global.uA2FwrdAndMaliciousAndNonsupportMDAndReject;
            
            Reader deduceNode = global.readers[deduceNodeId];
            UpdateAwardFunction(cachedresults[deduceNode].supportDroppingMalicious > 0, isReportSupportM,  
                ref uA2DropAndNormalAndSupportMDAndAccept, ref uA2FwrdAndNormalAndSupportMDAndAccept,
                ref uA2DropAndMaliciousAndSupportMDAndAccept, ref uA2FwrdAndMaliciousAndSupportMDAndAccept,
                ref uA2DropAndNormalAndSupportMDAndReject, ref uA2FwrdAndNormalAndSupportMDAndReject,
                ref uA2DropAndMaliciousAndSupportMDAndReject, ref uA2FwrdAndMaliciousAndSupportMDAndReject,
                ref uA2DropAndNormalAndNonsupportMDAndAccept, ref uA2FwrdAndNormalAndNonsupportMDAndAccept, 
                ref uA2DropAndMaliciousAndNonsupportMDAndAccept, ref uA2FwrdAndMaliciousAndNonsupportMDAndAccept,
                ref uA2DropAndNormalAndNonsupportMDAndReject, ref uA2FwrdAndNormalAndNonsupportMDAndReject,
                ref uA2DropAndMaliciousAndNonsupportMDAndReject, ref uA2FwrdAndMaliciousAndNonsupportMDAndReject
                );


            if (isReportSupportM)
            {
                double pSupportM = pSupportMByDropAndGroupNormal * pDrop * pGroupNormal
                    + pSupportMByFwrdAndGroupNormal * pFwrd * pGroupNormal
                    + pSupportMByDropAndGroupMalicious * pDrop * pGroupMalicious
                    + pSupportMByFwrdAndGroupMalicious * pFwrd * pGroupMalicious;

                double pDropAndGroupNormalBySupportM = pSupportMByDropAndGroupNormal * pDrop * pGroupNormal/pSupportM;
                double pFwrdAndGroupNormalBySupportM = pSupportMByFwrdAndGroupNormal * pFwrd * pGroupNormal / pSupportM;
                double pDropAndGroupMaliciousBySupportM = pSupportMByDropAndGroupMalicious * pDrop * pGroupMalicious / pSupportM;
                double pFwrdAndGroupMaliciousBySupportM = pSupportMByFwrdAndGroupMalicious * pFwrd * pGroupMalicious / pSupportM;


                //计算效用函数与后验概率的乘积
                double aNormalAndSupportMDAndAccept = pDropAndGroupNormalBySupportM * uA2DropAndNormalAndSupportMDAndAccept
                    + pFwrdAndGroupNormalBySupportM * uA2FwrdAndNormalAndSupportMDAndAccept;
                double aMaliciousAndSupportMDAndAccept = pDropAndGroupMaliciousBySupportM * uA2DropAndMaliciousAndSupportMDAndAccept
                    + pFwrdAndGroupMaliciousBySupportM * uA2FwrdAndMaliciousAndSupportMDAndAccept;

                double aNormalAndSupportMDAndReject = pDropAndGroupNormalBySupportM * uA2DropAndNormalAndSupportMDAndReject
                    + pFwrdAndGroupNormalBySupportM * uA2FwrdAndNormalAndSupportMDAndReject;
                double aMaliciousAndSupportMDAndReject = pDropAndGroupMaliciousBySupportM * uA2DropAndMaliciousAndSupportMDAndReject
                    + pFwrdAndGroupMaliciousBySupportM * uA2FwrdAndMaliciousAndSupportMDAndReject;

                aAccept = aNormalAndSupportMDAndAccept + aMaliciousAndSupportMDAndAccept;
                aReject = aNormalAndSupportMDAndReject + aMaliciousAndSupportMDAndReject;

                isAccept = (aAccept > aReject);
                //Console.WriteLine("pGroupNormalBySupportM:{0}, pDropBySupportM:{1}", pGroupNormalBySupportM, pDrop);
                //Console.WriteLine("pGroupMaliciousBySupportM:{0}, (1-pDropBySupportM):{1}", pGroupMaliciousBySupportM, (1 - pDrop));


            }
            else //NonsupportM 报告说事件是正常的
            {

                double pNonsupportM = pNonsupportMByDropAndGroupNormal * pDrop * pGroupNormal
                    + pNonsupportMByFwrdAndGroupNormal * pFwrd * pGroupNormal
                    + pNonsupportMByDropAndGroupMalicious * pDrop * pGroupMalicious
                    + pNonsupportMByFwrdAndGroupMalicious * pFwrd * pGroupMalicious;
                

                double pGroupDropAndNormalByNonsupportM = pNonsupportMByDropAndGroupNormal * pDrop * pGroupNormal / pNonsupportM;
                double pGroupFwrdAndNormalByNonsupportM = pNonsupportMByFwrdAndGroupNormal * pFwrd * pGroupNormal / pNonsupportM;
                double pGroupDropAndMaliciousByNonsupportM = pNonsupportMByDropAndGroupMalicious * pDrop * pGroupMalicious / pNonsupportM;
                double pGroupFwrdAndMaliciousByNonsupportM = pNonsupportMByFwrdAndGroupMalicious * pFwrd * pGroupMalicious / pNonsupportM;

                double aNormalAndNonsupportMDAndAccept = pGroupDropAndNormalByNonsupportM * uA2DropAndNormalAndNonsupportMDAndAccept
                    + pGroupFwrdAndNormalByNonsupportM * uA2FwrdAndNormalAndNonsupportMDAndAccept;
                double aMaliciousAndNonsupportMDAndAccept = pGroupDropAndMaliciousByNonsupportM * uA2DropAndMaliciousAndNonsupportMDAndAccept
                    + pGroupFwrdAndMaliciousByNonsupportM * uA2FwrdAndMaliciousAndNonsupportMDAndAccept;

                double aNormalAndNonsupportMDAndReject = pGroupDropAndNormalByNonsupportM * uA2DropAndNormalAndNonsupportMDAndReject
                    + pGroupFwrdAndNormalByNonsupportM * uA2FwrdAndNormalAndNonsupportMDAndReject;
                double aMaliciousAndNonsupportMDAndReject = pGroupDropAndMaliciousByNonsupportM * uA2DropAndMaliciousAndNonsupportMDAndReject
                    + pGroupFwrdAndMaliciousByNonsupportM * uA2FwrdAndMaliciousAndNonsupportMDAndReject;

                aAccept = aNormalAndNonsupportMDAndAccept + aMaliciousAndNonsupportMDAndAccept;
                aReject = aNormalAndNonsupportMDAndReject + aMaliciousAndNonsupportMDAndReject;

                isAccept = (aAccept > aReject);
                //Console.WriteLine("pGroupNormalByNonsupportM:{0}, pDropByNonsupportM:{1}", pGroupNormal, pDropByNonsupportM);
                //Console.WriteLine("pGroupMaliciousByNonsupportM:{0}, (1-pDropByNonsupportM):{1}", pGroupMalicious, (1 - pDropByNonsupportM));
            }
            bool isDuduceNormalEvent= (isReportSupportM ^ isAccept);
            string deductionStep = "";
            if(isDeduceStep2 == false)
                deductionStep = "DEDUCTION1-1";
            else if(deduceNode.Neighbors.ContainsKey(suspectedNodeId))//是第二步
                deductionStep = "DEDUCTION1-2";
            else
                deductionStep = "DEDUCTION1-3";
            string sAccept = (isAccept == true) ? "accept" : "reject";
            string sSupport = (isReportSupportM == true) ? "supporting" : "nonsupporting";

            //这里由于真实事件都是正常的，所以直接用true代替,ps:DropData代表正常场景下丢包
            string sResult = (isDuduceNormalEvent != global.DropData) ? "Succ" : "Fail";
            sResult = (isDeduceStep2 == true) ? sResult : "None";
            //尽管在第一步中，deduction是deduceNode做的，但是我们这里写是this做的
            Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4} {5}{6} is {7} by {8}. [{9}:{10}]\t[{11}:{12}]\t[{13}:{14}]-${15}$:{16}",
                scheduler.currentTime, deductionStep, this.type, this.Id, sSupport, NodeType.READER, suspectedNodeId, sAccept, deduceNodeId,
                aAccept, aReject, supportMWeight, nonsupportMWeight, supportMNodes, nonsupportMNodes, pkgIdent, sResult);

            if (isDeduceStep2 == true)
            {
                Console.Write("{0:F4} [ORG_TRUST] {1}{2}:\t", scheduler.currentTime, this.type, this.Id);
                for (int i = 0; i < global.orgNum; i++)
                {
                    Console.Write("{0}-", localNodeTrustWeights[global.orgs[i]]);
                }
                Console.WriteLine();
            }

            //比较各个节点的报告与整体报告的差别，如果我接受整体报告，则惩罚与整体报告不同的节点(即与supportReport相反的节点)，反之亦然

            if (isDeduceStep2 == true)
            {
                PunishMaliciousNodes(isDuduceNormalEvent, cachedresults);
                if (this.Neighbors.ContainsKey(suspectedNodeId))
                    UpdateNodeBelieves(isDuduceNormalEvent, pGroupNormal > 0.5, isReportSupportM, iteractions, isDeduceStep2);
            }

            Deduce2Result d2r = new Deduce2Result();
            d2r.IsAccept = isAccept;
            d2r.IsTotalReportSupport = isReportSupportM;
            return d2r;
        }

        void UpdateAwardFunction(bool mySupport, bool totalSupport, 
                ref double uA2DropAndNormalAndSupportMDAndAccept, ref double uA2FwrdAndNormalAndSupportMDAndAccept,
                ref double uA2DropAndMaliciousAndSupportMDAndAccept, ref double uA2FwrdAndMaliciousAndSupportMDAndAccept,
                ref double uA2DropAndNormalAndSupportMDAndReject, ref double uA2FwrdAndNormalAndSupportMDAndReject,
                ref double uA2DropAndMaliciousAndSupportMDAndReject, ref double uA2FwrdAndMaliciousAndSupportMDAndReject,
                ref double uA2DropAndNormalAndNonsupportMDAndAccept, ref double uA2FwrdAndNormalAndNonsupportMDAndAccept, 
                ref double uA2DropAndMaliciousAndNonsupportMDAndAccept, ref double uA2FwrdAndMaliciousAndNonsupportMDAndAccept,
                ref double uA2DropAndNormalAndNonsupportMDAndReject, ref double uA2FwrdAndNormalAndNonsupportMDAndReject,
                ref double uA2DropAndMaliciousAndNonsupportMDAndReject, ref double uA2FwrdAndMaliciousAndNonsupportMDAndReject)
        {
            //效用函数更新因子，如果与自己相同的话，最终的效用将增加，否则减少
            if (global.Step2DeduceMethod == DeduceMethod.Game)
                return;
             //如果两者相同，则接受的因子增加
            bool eqSP = (mySupport == totalSupport);
            uA2DropAndNormalAndSupportMDAndAccept = UpdateValue(eqSP, true, uA2DropAndNormalAndSupportMDAndAccept);
            uA2FwrdAndNormalAndSupportMDAndAccept = UpdateValue(eqSP, true, uA2FwrdAndNormalAndSupportMDAndAccept);
            uA2DropAndMaliciousAndSupportMDAndAccept = UpdateValue(eqSP, true, uA2DropAndMaliciousAndSupportMDAndAccept);
            uA2FwrdAndMaliciousAndSupportMDAndAccept = UpdateValue(eqSP, true, uA2FwrdAndMaliciousAndSupportMDAndAccept);
            uA2DropAndNormalAndSupportMDAndReject = UpdateValue(eqSP, false, uA2DropAndNormalAndSupportMDAndReject);
            uA2FwrdAndNormalAndSupportMDAndReject = UpdateValue(eqSP, false, uA2FwrdAndNormalAndSupportMDAndReject);
            uA2DropAndMaliciousAndSupportMDAndReject = UpdateValue(eqSP, false, uA2DropAndMaliciousAndSupportMDAndReject);
            uA2FwrdAndMaliciousAndSupportMDAndReject = UpdateValue(eqSP, false, uA2FwrdAndMaliciousAndSupportMDAndReject);
            uA2DropAndNormalAndNonsupportMDAndAccept = UpdateValue(eqSP, true, uA2DropAndNormalAndNonsupportMDAndAccept);
            uA2FwrdAndNormalAndNonsupportMDAndAccept = UpdateValue(eqSP, true, uA2FwrdAndNormalAndNonsupportMDAndAccept);
            uA2DropAndMaliciousAndNonsupportMDAndAccept = UpdateValue(eqSP, true, uA2DropAndMaliciousAndNonsupportMDAndAccept);
            uA2FwrdAndMaliciousAndNonsupportMDAndAccept = UpdateValue(eqSP, true, uA2FwrdAndMaliciousAndNonsupportMDAndAccept);
            uA2DropAndNormalAndNonsupportMDAndReject = UpdateValue(eqSP,false , uA2DropAndNormalAndNonsupportMDAndReject);
            uA2FwrdAndNormalAndNonsupportMDAndReject = UpdateValue(eqSP,false, uA2FwrdAndNormalAndNonsupportMDAndReject);
            uA2DropAndMaliciousAndNonsupportMDAndReject = UpdateValue(eqSP, false, uA2DropAndMaliciousAndNonsupportMDAndReject);
            uA2FwrdAndMaliciousAndNonsupportMDAndReject = UpdateValue(eqSP, false, uA2FwrdAndMaliciousAndNonsupportMDAndReject);
        }

        double UpdateValue(bool eqSP, bool accept, double v)
        {
            double x1 = 1.2;
            double x2 = 0.8;
            if (eqSP == accept) //award
            {
                if (v > 0)
                    return v * x1;
                else
                    return v * x2;
            }
            else
            {
                if (v > 0)
                    return v * x2;
                else
                    return v * x1;
            }
        }

        //更新对节点的期望值
        public void UpdateNodeBelieves(bool isDuduceNormalEvent, bool isNodeNormal, bool isReportSupportM, 
            List<IteratorType> iterations, bool isDeduceStep2)
        {
            iterations.Add(new IteratorType(isDuduceNormalEvent, isNodeNormal, isReportSupportM));
        }

        //惩罚函数，supportEventIsMalicious是整体报告支持事件是否是恶意的
        public void PunishMaliciousNodes(bool duduceIsNormal, Dictionary<Node, MODEventTrustResult> results)
        {
            if (global.debug)
                Console.Write("punish readers: \t");
            foreach (KeyValuePair<Node, MODEventTrustResult> result in results)
            {
                Node node = result.Key;

                if (duduceIsNormal == (result.Value.supportDroppingMalicious < 0))//两者一致
                {
                    this.pNormal[node] = this.pNormal[node] * global.RewardFactor;
                }
                else
                {
                    this.pNormal[node] = this.pNormal[node] * global.PunishmentFactor;
                    if (global.debug)
                        Console.Write("{0}:{1}\t", node.Id, this.pNormal[node]);
                }
            }
            if (global.debug)
                Console.WriteLine();

            //这里就不通过发数据包了，直接更改报告节点的评价了
            //foreach (int nbId in this.Neighbors.Keys)
            foreach (MODReader reportNode in results.Keys)
            {
                foreach (MODReader nbNode1 in results.Keys)
                {
                    if (!reportNode.pNBNormal.ContainsKey(this))
                        reportNode.pNBNormal.Add(this, new Dictionary<Node, double>());
                    if (!reportNode.pNBNormal[this].ContainsKey(nbNode1))
                        reportNode.pNBNormal[this].Add(nbNode1, global.pInitNormal);
                    if (duduceIsNormal == (results[nbNode1].supportDroppingMalicious < 0))
                        reportNode.pNBNormal[this][nbNode1] *= global.RewardFactor;
                    else
                        reportNode.pNBNormal[this][nbNode1] *= global.PunishmentFactor;
                }
                if (global.Interactive)
                {
                    if (!reportNode.NBNodeTrustWeights.ContainsKey(this))
                    {
                        reportNode.NBNodeTrustWeights.Add(this, new Dictionary<Node, double>());
                        reportNode.NBNodeTrustWeightsLastUpdate.Add(this, new Dictionary<Node, double>());
                        reportNode.NBNodeSuspectCount.Add(this, new Dictionary<Node, List<double>>());
                        reportNode.NBNodeHistoryMyVariance.Add(this, new Dictionary<Node, List<double>>());
                        reportNode.NBNodeHistoryTotalVariance.Add(this, new Dictionary<Node, List<double>>());
                    }


                    for (int i = 0; i < global.orgNum; i++)
                    {
                        Organization org = global.orgs[i];
                        if (!reportNode.NBNodeTrustWeights[this].ContainsKey(org))
                            reportNode.NBNodeTrustWeights[this].Add(org, this.nodeTrustWeights[org]);
                        else
                            reportNode.NBNodeTrustWeights[this][org] = this.nodeTrustWeights[org];
                        if (!reportNode.NBNodeTrustWeightsLastUpdate[this].ContainsKey(org))
                            reportNode.NBNodeTrustWeightsLastUpdate[this].Add(org, scheduler.currentTime);
                        else
                            reportNode.NBNodeTrustWeightsLastUpdate[this][org] = scheduler.currentTime;
                    }
                }
            }
            
        }


        //在接收到DATA数据包后，检查是否发生了抛弃数据包的情况
        public void CheckReceivedPacket(MODPhenomemon p)
        {
            int suspectedNodeId = p.nodeId;            
            List<MODEventTrustResult> results = new List<MODEventTrustResult>();

            string pkgIdent = MODEventTrust.GetPacketIdent(p.pkg);

            MODEventTrustResult realr = null;
            if (p == null)//没观察到接收数据包，则不确定
            {
                string[] x = pkgIdent.Split(new char[] { '-', '>' });
                int prevId = int.Parse(x[0]);
                Reader suspectedNode = global.readers[suspectedNodeId];
                Reader prevNode = global.readers[prevId];
                double[] speeds = new double[3];
                if (this.Speed != null)
                    speeds[0] = this.Speed[0];
                if (suspectedNode.Speed != null)
                    speeds[1] = suspectedNode.Speed[0];
                if (prevNode.Speed != null)
                    speeds[2] = prevNode.Speed[0];
                bool[] isNeighbors = new bool[2] { this.Neighbors.ContainsKey(prevId), this.Neighbors.ContainsKey(suspectedNodeId) };

                realr = MODEventTrust.NotObservedEventTrustResult(suspectedNodeId, this.Id,
                            pkgIdent, MODEventCategoryType.DropPacket, speeds, isNeighbors);
            }
            else
            {
                realr = MODEventTrust.DeduceDropPacketMaliciouslyByPacket(this.Id, this.observedPhenomemons, scheduler.currentTime, p);
            }

            //正常节点或异常节点，如果正常事件，则不报告            
            if (!this.IsMalicious() && (realr.supportDroppingMalicious <= 0 || global.DropData == false))//normal
                return;
            //如果是恶意节点，事件也是恶意的，也不报告
            if (this.IsMalicious() && (realr.supportDroppingMalicious > 0 || global.DropData == true))//malicious
                return;

            MODEventTrustResult myresult = null;

            if (global.debug)
                Console.Write("[Debug] node{0} CheckReceivedPacket of {1} of reader{2}\t", this.Id, MODEventTrust.GetPacketIdent(p.pkg), myresult.nodeId);

            if (pkgIdent != global.currentPkgIdent)
            {
                if (scheduler.currentTime - global.currentPkgIdentUpdate > 4)
                {
                    global.currentPkgIdentUpdate = scheduler.currentTime;
                    global.currentPkgIdent = pkgIdent;
                }
                else
                    return;
            }


            if (!this.receivedEventReports.ContainsKey(pkgIdent))
                this.receivedEventReports.Add(pkgIdent, new Dictionary<Node, MODEventTrustResult>());
            if (this.receivedEventReports[pkgIdent].ContainsKey(this))
            {
                myresult = this.receivedEventReports[pkgIdent][this];
            }
            else
            {
                myresult = GetEventTrustResult(pkgIdent, p, results, suspectedNodeId, this.Id, realr);
                this.receivedEventReports[pkgIdent].Add(this, myresult);
            }

            //如果我是恶意节点，我推测和真实事件一致，也不报告
            if (this.IsMalicious() && (myresult.supportDroppingMalicious == realr.supportDroppingMalicious))//malicious
                return;


            //报告恶意事件
            //过一段时间转发事件报告
            Console.WriteLine("Reader{0} inits an event report, suspected node is {1}", this.Id, myresult.nodeId);

            Event.AddEvent(new Event(scheduler.currentTime + 0.05f, EventType.FWD_EVENT_REPORT, this, pkgIdent));
            //如果是正常的观测节点，过一段时间检查所有邻居对该事件的报告
            if (!this.IsMalicious() && !this.toDeducedEventReports.Contains(pkgIdent))
            {
                Event.AddEvent(new Event(scheduler.currentTime + global.checkPhenomemonTimeout, EventType.DEDUCE_EVENT, this, pkgIdent));
                this.toDeducedEventReports.Add(pkgIdent);
            }
        }

        public bool IsMalicious()
        {
            return this.readerType != BehaviorType.NORMAL || ((MODOrganization)global.orgs[this.OrgId]).orgType != BehaviorType.NORMAL;
        }


        //收到其他节点的事件报告，转发，或者仅仅保存
        public void RecvEventReport(Packet pkg)
        {
            if (pkg.Dst != Id && pkg.Dst != BroadcastNode.Node.Id)
            {
                RoutePacket(pkg);
                return;
            }

            MemoryStream ms = new MemoryStream(pkg.TrustReport.result);
            BinaryFormatter formatter = new BinaryFormatter();
            List<MODEventTrustResult> results = (List<MODEventTrustResult>)formatter.Deserialize(ms);

            string pkgIdent = results[0].eventIdent;
            int suspectedNodeId = results[0].nodeId;



            if (!global.monitoredNodes.Contains(suspectedNodeId))
                throw new Exception("monitor node does not contain "+ suspectedNodeId);
            
            if (global.debug)
            {
                Console.Write("READER{0} recv {1} reports. ident:{2}\t", Id, results.Count, pkgIdent);
                foreach (MODEventTrustResult r in results)
                    Console.Write("{0}\t", r.reportNodeId);
                Console.WriteLine();
            }


            Dictionary<Node, MODEventTrustResult> cachedresults = null;
            if (!this.receivedEventReports.ContainsKey(pkgIdent))
            {
                this.receivedEventReports.Add(pkgIdent, new Dictionary<Node, MODEventTrustResult>());
            }
            

            int newcount = 0;            

            cachedresults = this.receivedEventReports[pkgIdent];
            //不在cachedresults的项，或比我的缓存更新的项
            foreach (MODEventTrustResult r in results)
            {
                Reader reportNode = Reader.GetReader(r.reportNodeId);
                if (!cachedresults.ContainsKey(reportNode))
                {
                    newcount++;
                    cachedresults.Add(reportNode, r);
                }
                else if (cachedresults[reportNode].timeStamp < r.timeStamp)
                {
                    cachedresults.Remove(reportNode);
                    cachedresults.Add(reportNode, r);
                }
            }
            if (newcount == 0)//和以前的一样，返回即可
                return;

            //这里先注释掉，看看非邻居的效果

            //非邻居节点，则直接定时判断
            if (!this.Neighbors.ContainsKey(suspectedNodeId))
            {
                if (!this.IsMalicious() && !this.toDeducedEventReports.Contains(pkgIdent))
                {
                    Event.AddEvent(new Event(scheduler.currentTime + global.checkPhenomemonTimeout, EventType.DEDUCE_EVENT, this, pkgIdent));
                    this.toDeducedEventReports.Add(pkgIdent);
                }
                return;
            }


            if (!cachedresults.ContainsKey(this))
            {
                MODPhenomemon p = MODEventTrust.GetPhenomemon(pkgIdent, this.Id, this.observedPhenomemons);
                MODEventTrustResult myresult = GetEventTrustResult(pkgIdent, p, cachedresults.Values.ToList(), suspectedNodeId, this.Id);
                cachedresults.Add(this, myresult);
                //TODO 这里涉及到没有观察到收到数据包，应该是不支持节点异常的 myresult.normal = //;
            }
            else if (cachedresults[this].eventIdent == "")//之前没有观察到现象，但是现在观察到了
            {
                MODPhenomemon p = MODEventTrust.GetPhenomemon(pkgIdent, this.Id, this.observedPhenomemons);
                if (p != null)
                {
                    MODEventTrustResult myresult = GetEventTrustResult(pkgIdent, p, cachedresults.Values.ToList(), suspectedNodeId, this.Id);
                    cachedresults[this] = myresult;
                }
            }
            /*
            foreach (MODEventTrustResult r in cachedresults.Values)
            {
                Console.Write("{0}:{1}\t", r.reportNodeId, r.normal);
            }
            Console.WriteLine();
             */


            //过一段时间转发事件报告
            Event.AddEvent(new Event(scheduler.currentTime + 0.05f, EventType.FWD_EVENT_REPORT, this, pkgIdent));

            if (!this.IsMalicious() && !this.toDeducedEventReports.Contains(pkgIdent))
            {
                Event.AddEvent(new Event(scheduler.currentTime + global.checkPhenomemonTimeout, EventType.DEDUCE_EVENT, this, pkgIdent));
                this.toDeducedEventReports.Add(pkgIdent);
            }

            return;
        }


        public void ForwardEventReport(string ident)
        {
            List<MODEventTrustResult> results = null;
            if (receivedEventReports.ContainsKey(ident))
                results = this.receivedEventReports[ident].Values.ToList();
            else
            {
                Console.WriteLine("no such an ident in receivedEventReports");
                return;
            }
            MemoryStream ms = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, results);
            ms.Seek(0, 0);
            byte[] tmp = ms.ToArray();

            if(global.debug)
                Console.WriteLine("[ForwardEventReport] READER{0} reports\tident:{1}\t", results.Count, ident);
            
            Packet pkg = new Packet(this, BroadcastNode.Node, PacketType.EVENT_REPORT);
            pkg.TrustReport = new TrustReportField(0, tmp, tmp.Length);
            SendPacketDirectly(scheduler.currentTime, pkg);            
        }


        public override void Recv(Packet pkg)
        {
            pkg.seqInited = false;
            //只有reader才需要检查，但是里面函数处理了
            CheckPacketCount(pkg);


            //如果不存在邻居中，则添加.
            //如果存在，则更新时间
            //if (pkg.Beacon == null && !this.Neighbors.ContainsKey(pkg.Prev) && pkg.PrevType == NodeType.READER)
            if (pkg.Beacon == null && pkg.PrevType == NodeType.READER)
                RecvBeacon(pkg);


            //Check the Phenomemon
            if (pkg.PrevType == NodeType.READER && pkg.Type == PacketType.DATA)
                AddReceivePacketPhenomemon(pkg);


            if ((pkg.Next != Id && pkg.Next != BroadcastNode.Node.Id) || pkg.NextType != NodeType.READER)
            {
                return;
            }

            if (pkg.TTL == 0)
            {
                if (global.debug)
                    Console.WriteLine("debug: TTL drops to 0, abort.");
                return;
            }
            pkg.TTL--;
            ProcessPacket(pkg);
        }


        public override void ProcessPacket(Packet pkg)
        {
            //I send the packet myself, ignore
            if (pkg.Prev == Id && pkg.PrevType == type)
            {
                return;
            }

            if (this.IsMalicious() && pkg.Dst != Id)
            {
                if (pkg.Type == PacketType.DATA && global.DropData == true)
                {
                    Console.WriteLine("{0:F4} [{1}] {2}{3} Drop data of {4}{5} due to bad node. packet ident:{6}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev, pkg.getId());
                    return;
                }
            }

            switch (pkg.Type)
            {
                //Readers
                case PacketType.BEACON:
                    RecvBeacon(pkg);
                    break;
                case PacketType.EVENT_REPORT:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    RecvEventReport(pkg);
                    break;
                //Some codes are hided in the base class.
                default:
                    base.ProcessPacket(pkg);
                    return;
            }
        }

    }
}
