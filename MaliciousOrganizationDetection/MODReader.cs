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
        public bool Drop;
        public bool Normal;
        public bool SupportM;

        public IteratorType(bool Drop, bool Normal, bool SupportM)
        {
            this.Drop = Drop;
            this.Normal = Normal;
            this.SupportM = SupportM;
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
        private List<IteratorType> nodeIteractions;//博弈次数，越多则经验越多
        private Dictionary<Node, List<double>> nodeHistoryVariance;

        private Dictionary<Node, Dictionary<Node, List<double>>> NBNodeSuspectCount;
        private Dictionary<Node, Dictionary<Node, double>> NBNodeTrustWeight;
        private Dictionary<Node, List<IteratorType>> NBNodeIteractions;//博弈次数，越多则经验越多
        private Dictionary<Node, Dictionary<Node, List<double>>> NBNodeHistoryVariance;

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
            this.NBNodeIteractions = new Dictionary<Node,List<IteratorType>>();
            this.nodeIteractions = new List<IteratorType>();
            this.nodeHistoryVariance = new Dictionary<Node, List<double>>();

            this.NBNodeSuspectCount = new Dictionary<Node, Dictionary<Node, List<double>>>();
            this.NBNodeTrustWeight = new Dictionary<Node, Dictionary<Node, double>>();
            this.NBNodeHistoryVariance = new Dictionary<Node, Dictionary<Node, List<double>>>();





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


        private MODEventTrustResult GetEventTrustResult(MODPhenomemon p, List<MODEventTrustResult> results, int suspectedNodeId, int reportNodeId)
        {
            bool forge = false;
            MODEventTrustResult result = null;
            string pkgIdent = "";

            MODEventTrustResult realr = null;

            if (global.debug)
                Console.WriteLine("Reader{0} GetEventTrustResult", this.Id);

            if (p == null)//没观察到接收数据包，则不确定
            {
                Reader suspectedNode = global.readers[suspectedNodeId];
                double speed = 0;
                if (this.Speed != null)
                    speed = this.Speed[0];
                if (suspectedNode.Speed != null)
                    speed += suspectedNode.Speed[0];
                pkgIdent = "";
                realr = MODEventTrust.NotObservedEventTrustResult(suspectedNodeId, this.Id,
                            pkgIdent, MODEventCategoryType.DropPacket, speed);
            }
            else
            {
                pkgIdent = MODEventTrust.GetPacketIdent(p.pkg);
                realr = MODEventTrust.DeduceDropPacketMaliciouslyByPacket(this.Id, this.observedPhenomemons, scheduler.currentTime, p);
            }

            bool isAccept = false;
            bool isSupport = false;
            bool isCompositeReportSupport = false;
            //如果是恶意节点，则会考察检测节点可能的动作
            if (this.IsMalicious())
            {
                if (global.Step1DeduceMethod == DeduceMethod.Native)//原始的话，恶意节点无条件伪造报告
                    forge = true;
                else if (global.Step1DeduceMethod == DeduceMethod.Game || global.Step1DeduceMethod == DeduceMethod.OrgGame)
                {
                    //如果是博弈论，则判断检测节点的观点
                    //此处仅以其周围邻居为参考，而非报告节点的邻居，这是由于ad-hoc的局限性所致的
                    Dictionary<Node, MODEventTrustResult> localcachedresults = new Dictionary<Node, MODEventTrustResult>();
                    foreach (MODEventTrustResult r in results)
                        localcachedresults.Add(Reader.GetReader(r.reportNodeId), r);

                    ReduceReports(localcachedresults);
                    //初始化邻居的结构，且找到最久的邻居
                    int minNbId = GetLongestNormalNeighbor();
                    Reader minNbNode = global.readers[minNbId];

                    foreach (int nbId in this.Neighbors.Keys)
                    {

                        MODReader nbNode = (MODReader)global.readers[nbId];
                        //如果已经保存，则继续
                        if (!localcachedresults.ContainsKey(nbNode))
                        {
                            if (localcachedresults.Count >= global.MaxReportCount)
                                break;
                            MODEventTrustResult r = null;
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
                            localcachedresults.Add(nbNode, r);
                        }
                    }

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

                    if (!this.NBNodeTrustWeight.ContainsKey(minNbNode))
                    {
                        this.NBNodeTrustWeight.Add(minNbNode, new Dictionary<Node, double>());
                        this.NBNodeSuspectCount.Add(minNbNode, new Dictionary<Node, List<double>>());
                    }

                    Dictionary<Node, double> pNormal = new Dictionary<Node, double>();

                    //这个邻居节点对该节点邻居的印象
                    foreach (Node reportNode in reportNodes)
                    {
                        Organization org = global.orgs[((Reader)reportNode).OrgId];
                        if (this.NBNodeTrustWeight[minNbNode].ContainsKey(org))
                            pNormal.Add(reportNode, this.pNBNormal[minNbNode][reportNode] * this.NBNodeTrustWeight[minNbNode][org]);
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
                    //if (global.deduceMethod == DeduceMethod.Game )
                    //{
                    Deduce2Result d2r = DeduceA2(reportNodes, false, suspectedNodeId, localcachedresults, minNbNode.Id,
                        pDrop, pNormal, this.NBNodeTrustWeight[minNbNode],
                        this.NBNodeIteractions[minNbNode]);
                    isAccept = d2r.IsAccept;
                    isCompositeReportSupport = d2r.IsTotalReportSupport;
                    //此时，真实事件是否正常已知，自己的性质，检测节点的最佳行为已知，但是由于整体报告的性质可能出现变化，
                    //设有共有n个节点，m个正常节点，那么如果少于p-m/2个恶意节点改变自己的报告，则整体报告维持不变，否则整体报告改变。
                    isSupport = DeduceA1(realr.supportDroppingMalicious <= 0, isCompositeReportSupport, isAccept);
                    if (isSupport == false)//如果检测节点不同意，则返回
                        forge = true;
                    else
                        forge = false;
                    //}
                }
                else
                {
                    forge = true;
                }

            }


            if (!this.IsMalicious() || forge == false)
            {
                Console.WriteLine("READER{0} not forge a malicious report", this.Id);
                return realr;

            }
            else //恶意节点
            {
                Console.WriteLine("READER{0} forge a malicious report", this.Id);
                //真实事件是恶意的
                if (realr.supportDroppingMalicious > 0)
                    result = MODEventTrust.ForgeNormalEventTrustResult(p.nodeId, this.Id,
                        MODEventTrust.GetPacketIdent(p.pkg), MODEventCategoryType.DropPacket);
                else
                    result = MODEventTrust.ForgeMaliciousEventTrustResult(suspectedNodeId, this.Id,
                    pkgIdent, MODEventCategoryType.DropPacket);
                return result;
            }
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
                if (it.Drop && it.Normal)
                    DropAndGroupNormal++;
                if (it.Drop && !it.Normal)
                    DropAndGroupMalicious++;
                if (!it.Drop && it.Normal)
                    FwrdAndGroupNormal++;
                if (it.Drop && it.Normal)
                    FwrdAndGroupMalicious++;

                if (it.Drop && it.Normal && it.SupportM)
                    SupportMAndDropAndGroupNormal++;
                if (it.Drop && it.Normal && !it.SupportM)
                    NonsupportMAndDropAndGroupNormal++;
                if (it.Drop && !it.Normal && it.SupportM)
                    SupportMAndDropAndGroupMalicious++;
                if (it.Drop && !it.Normal && !it.SupportM)
                    NonsupportMAndDropAndGroupMalicious++;
                if (!it.Drop && it.Normal && it.SupportM)
                    SupportMAndFwrdAndGroupNormal++;
                if (!it.Drop && it.Normal && !it.SupportM)
                    NonsupportMAndFwrdAndGroupNormal++;
                if (it.Drop && it.Normal && it.SupportM)
                    SupportMAndFwrdAndGroupMalicious++;
                if (it.Drop && it.Normal && !it.SupportM)
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

            //如果不是我需要监控的节点，返回
            if (!global.monitoredNodes.Contains(pkg.Prev))
                return;


            //记录发送现象
            if (pkg.Next != BroadcastNode.Node.Id)
            {
                p = new MODPhenomemon(MODPhenomemonType.SEND_PACKET, pkg.Prev, scheduler.currentTime, pkg);
                p.likehood = global.sendLikehood;
                this.observedPhenomemons.Add(p);
                if (global.debug)
                    Console.WriteLine("[Debug] reader{0} add a RECV phenomemon of reader{1}", Id, pkg.Next);

                //延迟检查事件
                Event.AddEvent(new Event(Scheduler.getInstance().currentTime + global.checkReceivedPacketTimeout, EventType.CHK_RECV_PKT, this, p));
            }

            //数据包到达目的地，忽略
        }


        //恶意观察节点推断采取的行为
        //isAccept是接受事件是恶意的
        public bool DeduceA1(bool normalEvent, bool isCompositeReportSupport, bool isAccept)
        {
            bool isSupport = false;
            double aSupport = 0, aNonsupport = 0;

            aSupport = MODEventTrust.A1Fun(normalEvent, true, isCompositeReportSupport, isAccept);
            aNonsupport = MODEventTrust.A1Fun(normalEvent, false, isCompositeReportSupport, isAccept);

            isSupport = (aSupport > aNonsupport);
            return isSupport;
        }

        public Dictionary<Node, MODEventTrustResult> AdjustNodeTrust(Dictionary<Node, double> localNodeTrust,
            Dictionary<Node, List<double>> localNodeSuspectCount, Dictionary<Node, MODEventTrustResult> cachedresults, 
            string pkgIdent, Node deducingNode, int suspectNodeId)
        {
            Node[] reportNodes = cachedresults.Keys.ToArray();
            MODEventTrustResult myReport = cachedresults[deducingNode];
            Dictionary<int, HashSet<int>> orgMapping = new Dictionary<int, HashSet<int>>();
            Dictionary<Node, MODEventTrustResult> suspectedReportReaders = new Dictionary<Node, MODEventTrustResult>();
            Dictionary<Node, MODEventTrustResult> suspectedReportOrgs = new Dictionary<Node, MODEventTrustResult>();
            Dictionary<Node, MODEventTrustResult> finalSuspectedReportNodes = new Dictionary<Node, MODEventTrustResult>();

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


            if (totalOrgReport.variance > global.MaxTotalOrgVariance)
            {
                //找到所有与自己差别较大的机构
                foreach (KeyValuePair<int, HashSet<int>> k in orgMapping)
                {
                    int orgId = k.Key;
                    Organization org = global.orgs[orgId];
                    double dist = MODEventTrust.Distance(myReport, orgReports[org]);
                    if (dist > global.MaxReportDistance)
                    {
                        suspectedReportOrgs.Add(org, orgReports[org]);
                        finalSuspectedReportNodes.Add(org, orgReports[org]);
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
                        if (MODEventTrust.Distance(myReport, cachedresults[node]) > global.MaxReportDistance)
                        {
                            finalSuspectedReportNodes.Add(node, cachedresults[node]);
                        }
                    }
                }

                //将可疑机构与自己的结果的比较
                foreach (KeyValuePair<Node, MODEventTrustResult> k in finalSuspectedReportNodes)
                {
                    Node node = k.Key;
                    double variance = k.Value.myvariance;
                    double freq = 1, historyVarianceStandardDeviation = 1;

                    if (!localNodeSuspectCount.ContainsKey(node))
                        localNodeSuspectCount.Add(node, new List<double>());

                    if (!localNodeTrust.ContainsKey(node))
                        localNodeTrust.Add(node, 1f);
                    
                    if (node.type == NodeType.READER)//考察节点短期的行为，则置1
                        localNodeTrust[node] = 1f;

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
                    if (!this.nodeHistoryVariance.ContainsKey(node))
                        this.nodeHistoryVariance.Add(node, new List<double>());
                    this.nodeHistoryVariance[node].Add(variance);
                    if (this.nodeHistoryVariance[node].Count > 2)//曲线至少有三个点
                    {
                        //使历史记录数量维持在MaxHistoryCount之内
                        if (nodeHistoryVariance[node].Count > global.MaxHistoryCount)
                            nodeHistoryVariance[node].RemoveRange(0, nodeHistoryVariance[node].Count - global.MaxHistoryCount);

                        double[] arcs = new double[this.nodeHistoryVariance[node].Count];
                        for (int i = 0; i < this.nodeHistoryVariance[node].Count; i++)
                            arcs[i] = 0.1f * i;

                        LinearRegression reg = new LinearRegression();
                        reg.BuildLSMCurve(arcs, this.nodeHistoryVariance[node], 1, false);
                        //考察reg.CoefficientsStandardError
                        historyVarianceStandardDeviation = reg.StandardDeviation;
                    }

                    localNodeTrust[node] *= global.AdjustFactor /
                        ((Math.Log(freq + 1, global.SuspectedCountBase) + 1) * Math.Pow(global.VarianceBase, variance)
                        * (Math.Log(historyVarianceStandardDeviation, global.HistoryVSDBase) + 1));
                    if (localNodeTrust[node] > 1)
                        localNodeTrust[node] = 0.99f;
                    else if(localNodeTrust[node] <=0)
                        localNodeTrust[node] = 0.1f;

                }
            }
            return finalSuspectedReportNodes;
        }


        private void ReduceReports(Dictionary<Node, MODEventTrustResult> results)
        {
            //如果超过最大值，则正常和异常节点各去掉一个
            while (results.Count > global.MaxReportCount)
            {
                Node n1 = null, n2 = null;
                foreach (KeyValuePair<Node, MODEventTrustResult> k in results)
                {
                    if (n1 == null && k.Value.supportDroppingMalicious > 0 && k.Key != this)
                        n1 = k.Key;
                    else if (n2 == null && k.Value.supportDroppingMalicious <= 0 && k.Key != this)
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
            if(MODEventTrustResult.DeducedPackets.Contains(pkgIdent))
                return;
            Console.WriteLine("Reader{0} Deduces Event Type for {1}", this.Id, pkgIdent);
            
            Dictionary<Node, MODEventTrustResult> cachedresults = null;
            if (this.receivedEventReports.ContainsKey(pkgIdent))
                cachedresults = this.receivedEventReports[pkgIdent];
            else
                throw new Exception("no such an ident in receivedEventReports");


            ReduceReports(cachedresults);
            foreach(KeyValuePair<Node, MODEventTrustResult> k in cachedresults)
            {
                Console.Write("{0}:{1}\t", k.Key, k.Value.supportDroppingMalicious);
            }
            Console.WriteLine();

            int suspectNodeId = -1;
            foreach (KeyValuePair<Node, MODEventTrustResult> r in cachedresults)
            {
                suspectNodeId = r.Value.node;
                break;
            }


            if (global.Step2DeduceMethod == DeduceMethod.Native)
            {
                int normalCount = 0, maliciousCount = 0;
                foreach (KeyValuePair<Node, MODEventTrustResult> r in cachedresults)
                {
                    if (r.Value.supportDroppingMalicious > 0)
                        normalCount++;
                    else
                        maliciousCount++;

                    //Console.Write("{0}:{1}\t",r.Key, r.Value.normal);
                }
                if (normalCount > maliciousCount)
                    Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4}{5} is normal. {6}-{7}", scheduler.currentTime, "DEDUCTION2", this.type, this.Id, NodeType.READER, suspectNodeId, normalCount, maliciousCount);
                else
                    Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4}{5} is malicious. {6}-{7}", scheduler.currentTime, "DEDUCTION2", this.type, this.Id, NodeType.READER, suspectNodeId, normalCount, maliciousCount);
            }
            else //博弈论的方法
            {
                //自己的记录是否存在
                if(!cachedresults.ContainsKey(this))
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

                    Deduce2Result d2r = DeduceA2(reportNodes, true, suspectNodeId, cachedresults, this.Id,
                        pDrop, this.pNormal, this.nodeTrustWeights, this.nodeIteractions);
                    bool isAccept = d2r.IsAccept;
                    
                }
                else if (global.Step2DeduceMethod == DeduceMethod.OrgGame)
                {
                    //这里的pNormal是更新后的了
                    Dictionary<Node, double> pNormal = new Dictionary<Node, double>();

                    Dictionary<Node, MODEventTrustResult> suspectedReportNodes = 
                        AdjustNodeTrust(this.nodeTrustWeights, this.nodeSuspectCount, cachedresults, pkgIdent, this, suspectNodeId);

                    foreach (Node reportNode in reportNodes)
                    {
                        Organization org = global.orgs[((Reader)reportNode).OrgId];

                        //机构的性质暗示了节点的先验概率
                        if (this.nodeTrustWeights.ContainsKey(org))
                            pNormal.Add(reportNode, this.pNormal[reportNode] * this.nodeTrustWeights[org]);
                        else
                            pNormal.Add(reportNode, this.pNormal[reportNode]);
                        if (global.debug)
                        {
                            //Console.WriteLine("node:{0}\t,pNormal:{1}", reportNode, pNormal[reportNode]);
                        }
                    }

                    double pDrop = global.pInitDrop;
                    //double pDropBySupportM = 0, pDropByNonsupportM = 0;
                    //SetDropBySupport(ref pDropBySupportM, ref pDropByNonsupportM, this.nodeIteractions);


                    Deduce2Result d2r = DeduceA2(reportNodes, true, suspectNodeId, cachedresults, this.Id,
                        pDrop, pNormal, this.nodeTrustWeights, this.nodeIteractions);

                    bool isAccept = d2r.IsAccept;
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


        public Deduce2Result DeduceA2(Node[] reportNodes, bool isDeduceStep2, int nodeId, Dictionary<Node, MODEventTrustResult> cachedresults, 
            int step1Node, double pDrop, Dictionary<Node, double> pNormal, Dictionary<Node, double> nodeTrustWeight,
            List<IteratorType> iteractions)
        {
            
            foreach (Node node in reportNodes)
            {
                if (!nodeTrustWeight.ContainsKey(node))//如果没有记录，则设其初始权重为1
                    nodeTrustWeight.Add(node, 1f);
                Organization org = global.orgs[((Reader)node).OrgId];
                if (!nodeTrustWeight.ContainsKey(org))
                    nodeTrustWeight.Add(org, 1f);
            }

            double supportMWeight = 0, nonsupportMWeight = 0;
            double supportMNodes = 0, nonsupportMNodes = 0;
            bool isReportSupportM = false;
            //计算报告是support还是nonsupport
            foreach (KeyValuePair<Node, MODEventTrustResult> k in cachedresults)
            {
                Node node = k.Key;
                MODEventTrustResult result = k.Value;
                if (result.supportDroppingMalicious > 0)
                {
                    supportMWeight += nodeTrustWeight[node];
                    supportMNodes++;
                }
                else
                {
                    nonsupportMWeight += nodeTrustWeight[node];
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
                        w1 += nodeTrustWeight[node]*nodeTrustWeight[org];
                    }

                    foreach (Node node in reportNodes)//找出相反的节点
                    {
                        if (set1.Contains(node))
                            continue;
                        Organization org = global.orgs[((Reader)node).OrgId];
                        set2.Add(node);
                        w2 += nodeTrustWeight[node] * nodeTrustWeight[org];
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
                double aNormalAndSupportMDAndAccept = pDropAndGroupNormalBySupportM * global.uA2DropAndNormalAndSupportMDAndAccept
                    + pFwrdAndGroupNormalBySupportM * global.uA2FwrdAndNormalAndSupportMDAndAccept;
                double aMaliciousAndSupportMDAndAccept = pDropAndGroupMaliciousBySupportM * global.uA2DropAndMaliciousAndSupportMDAndAccept
                    + pFwrdAndGroupMaliciousBySupportM * global.uA2FwrdAndMaliciousAndSupportMDAndAccept;

                double aNormalAndSupportMDAndReject = pDropAndGroupNormalBySupportM * global.uA2DropAndNormalAndSupportMDAndReject
                    + pFwrdAndGroupNormalBySupportM * global.uA2FwrdAndNormalAndSupportMDAndReject;
                double aMaliciousAndSupportMDAndReject = pDropAndGroupMaliciousBySupportM * global.uA2DropAndMaliciousAndSupportMDAndReject
                    + pFwrdAndGroupMaliciousBySupportM * global.uA2FwrdAndMaliciousAndSupportMDAndReject;

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

                double aNormalAndNonsupportMDAndAccept = pGroupDropAndNormalByNonsupportM * global.uA2DropAndNormalAndNonsupportMDAndAccept
                    + pGroupFwrdAndNormalByNonsupportM * global.uA2FwrdAndNormalAndNonsupportMDAndAccept;
                double aMaliciousAndNonsupportMDAndAccept = pGroupDropAndMaliciousByNonsupportM * global.uA2DropAndMaliciousAndNonsupportMDAndAccept
                    + pGroupFwrdAndMaliciousByNonsupportM * global.uA2FwrdAndMaliciousAndNonsupportMDAndAccept;

                double aNormalAndNonsupportMDAndReject = pGroupDropAndNormalByNonsupportM * global.uA2DropAndNormalAndNonsupportMDAndReject
                    + pGroupFwrdAndNormalByNonsupportM * global.uA2FwrdAndNormalAndNonsupportMDAndReject;
                double aMaliciousAndNonsupportMDAndReject = pGroupDropAndMaliciousByNonsupportM * global.uA2DropAndMaliciousAndNonsupportMDAndReject
                    + pGroupFwrdAndMaliciousByNonsupportM * global.uA2FwrdAndMaliciousAndNonsupportMDAndReject;

                aAccept = aNormalAndNonsupportMDAndAccept + aMaliciousAndNonsupportMDAndAccept;
                aReject = aNormalAndNonsupportMDAndReject + aMaliciousAndNonsupportMDAndReject;

                isAccept = (aAccept > aReject);
                //Console.WriteLine("pGroupNormalByNonsupportM:{0}, pDropByNonsupportM:{1}", pGroupNormal, pDropByNonsupportM);
                //Console.WriteLine("pGroupMaliciousByNonsupportM:{0}, (1-pDropByNonsupportM):{1}", pGroupMalicious, (1 - pDropByNonsupportM));
            }
            bool duduceIsNormal= (isReportSupportM ^ isAccept);
            string deductionStep = (isDeduceStep2 == true) ? "DEDUCTION1-2" : "DEDUCTION1-1";
            string sAccept = (isAccept == true) ? "accept" : "reject";
            string sSupport = (isReportSupportM == true) ? "supporting" : "nonsupporting";

            //这里由于真实事件都是正常的，所以直接用true代替
            string sResult = (duduceIsNormal == true) ? "Succ" : "Fail";
            sResult = (isDeduceStep2 == true) ? sResult : " ";
            Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4} {5}{6} is {7} by {8}. [{9}:{10}]\t[{11}:{12}]\t[{13}:{14}]-{15}",
                scheduler.currentTime, deductionStep, this.type, this.Id, sSupport, NodeType.READER, nodeId, sAccept, step1Node, 
                aAccept, aReject, supportMWeight, nonsupportMWeight, supportMNodes, nonsupportMNodes, sResult);

            //比较各个节点的报告与整体报告的差别，如果我接受整体报告，则惩罚与整体报告不同的节点(即与supportReport相反的节点)，反之亦然
            
            if(isDeduceStep2 == true)
                PunishMaliciousNodes(duduceIsNormal, cachedresults);
            //TODO
            bool duduceIsDrop = true;
            UpdateNodeBelieves(isReportSupportM, duduceIsNormal, duduceIsDrop, iteractions, isDeduceStep2);

            Deduce2Result d2r = new Deduce2Result();
            d2r.IsAccept = isAccept;
            d2r.IsTotalReportSupport = isReportSupportM;
            return d2r;
        }

        //更新对节点的期望值
        public void UpdateNodeBelieves(bool isReportSupportM, bool deduceIsNormal, bool deduceIsDrop,
            List<IteratorType> iterations, bool isDeduceStep2)
        {
            iterations.Add(new IteratorType(deduceIsDrop, deduceIsNormal, isReportSupportM));
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

            //这里就不通过发数据包了，直接更改邻居节点的评价了
            foreach (int nbId in this.Neighbors.Keys)
            {
                //如果邻居
                MODReader nbNode = (MODReader)global.readers[nbId];
                if (results.ContainsKey(nbNode))
                {
                    if (!nbNode.pNBNormal.ContainsKey(this))
                        nbNode.pNBNormal.Add(this, new Dictionary<Node, double>());
                    if (!nbNode.pNBNormal[this].ContainsKey(nbNode))
                        nbNode.pNBNormal[this].Add(nbNode, global.pInitNormal);
                    if (duduceIsNormal == (results[nbNode].supportDroppingMalicious < 0))
                        nbNode.pNBNormal[this][nbNode] *= global.RewardFactor;
                    else
                        nbNode.pNBNormal[this][nbNode] *= global.PunishmentFactor;
                }
            }
        }


        //在接收到DATA数据包后，检查是否发生了抛弃数据包的情况
        public void CheckReceivedPacket(MODPhenomemon p)
        {
            int suspectedNodeId = p.nodeId;            
            List<MODEventTrustResult> results = new List<MODEventTrustResult>();

            //TODO,这里假设只考虑节点1
            if (p.pkg.Prev != 1)
                return;

            MODEventTrustResult result = GetEventTrustResult(p, results, suspectedNodeId, this.Id);

            if (global.debug)
                Console.Write("[Debug] node{0} CheckReceivedPacket of {1} of reader{2}\t", this.Id, MODEventTrust.GetPacketIdent(p.pkg), result.node);
            
            string pkgIdent = result.eventIdent;
            if (!this.receivedEventReports.ContainsKey(pkgIdent))
            {
                this.receivedEventReports.Add(pkgIdent, new Dictionary<Node, MODEventTrustResult>());
                this.receivedEventReports[pkgIdent].Add(this, result);
            }

            //正常节点或异常节点，如果正常事件，则不报告            
            if (result.supportDroppingMalicious <= 0 && !this.IsMalicious())//normal
                return;

            //报告恶意事件
            //过一段时间转发事件报告
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

            //发给自己的报告
            MemoryStream ms = new MemoryStream(pkg.TrustReport.result);
            BinaryFormatter formatter = new BinaryFormatter();
            List<MODEventTrustResult> results = (List<MODEventTrustResult>)formatter.Deserialize(ms);

            string pkgIdent = results[0].eventIdent;
            int suspectedNodeId = results[0].node;
            if (!this.Neighbors.ContainsKey(suspectedNodeId))
                return;

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

            if (!cachedresults.ContainsKey(this))
            {
                MODPhenomemon p = MODEventTrust.GetPhenomemon(pkgIdent, this.Id, this.observedPhenomemons);
                MODEventTrustResult myresult = GetEventTrustResult(p, cachedresults.Values.ToList(), suspectedNodeId, this.Id);                
                cachedresults.Add(this, myresult);
                //TODO 这里涉及到没有观察到收到数据包，应该是不支持节点异常的 myresult.normal = //;
            }
            else if (cachedresults[this].eventIdent == "")//之前没有观察到现象，但是现在观察到了
            {
                MODPhenomemon p = MODEventTrust.GetPhenomemon(pkgIdent, this.Id, this.observedPhenomemons);
                if (p != null)
                {
                    MODEventTrustResult myresult = GetEventTrustResult(p, cachedresults.Values.ToList(), suspectedNodeId, this.Id);
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
