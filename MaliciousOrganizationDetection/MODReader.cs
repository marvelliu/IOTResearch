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

    public enum ReaderType
    {
        NORMAL,
        DROP_PACKET,
        //这里简化为只检查是否抛弃数据包
    }

    public class MODReader : Reader
    {

        private MODGlobal global;
        public ReaderType readerType;

        private int totalReceivedPackets;

        private Dictionary<int, MODPhenomemon> neighborSpeedPhenomemons;
        //普通节点观察到的现象
        private HashSet<MODPhenomemon> observedPhenomemons;

        private Dictionary<string, Dictionary<int, MODEventTrustResult>> receivedEventReports;
        private HashSet<string> deducedEventReports;


        private Dictionary<int, double> pNormal;
        private Dictionary<int, double> pSupportByNormal;
        private Dictionary<int, double> pNonsupportByNormal;
        private Dictionary<int, double> pSupportByMalicious;
        private Dictionary<int, double> pNonsupportByMalicious;

        private Dictionary<int, Dictionary<int, double>> pNBNormal;
        private Dictionary<int, Dictionary<int, double>> pNBSupportByNormal;
        private Dictionary<int, Dictionary<int, double>> pNBNonsupportByNormal;
        private Dictionary<int, Dictionary<int, double>> pNBSupportByMalicious;
        private Dictionary<int, Dictionary<int, double>> pNBNonsupportByMalicious;

        new public static MODReader ProduceReader(int id, int org)
        {
            return new MODReader(id, org);
        }

        public MODReader(int id, int org)
            : base(id, org)
        {
            this.global = (MODGlobal)Global.getInstance();
            this.readerType = ReaderType.NORMAL;
            this.observedPhenomemons = new HashSet<MODPhenomemon>();
            this.neighborSpeedPhenomemons = new Dictionary<int, MODPhenomemon>();
            this.receivedEventReports = new Dictionary<string, Dictionary<int, MODEventTrustResult>>();
            this.deducedEventReports = new HashSet<string>();

            this.pNormal = new Dictionary<int, double>();
            this.pSupportByNormal = new Dictionary<int, double>();
            this.pNonsupportByNormal = new Dictionary<int, double>();
            this.pSupportByMalicious = new Dictionary<int, double>();
            this.pNonsupportByMalicious = new Dictionary<int, double>();

            this.pNBNormal = new Dictionary<int, Dictionary<int, double>>();
            this.pNBSupportByNormal = new Dictionary<int, Dictionary<int, double>>();
            this.pNBNonsupportByNormal = new Dictionary<int, Dictionary<int, double>>();
            this.pNBSupportByMalicious = new Dictionary<int, Dictionary<int, double>>();
            this.pNBNonsupportByMalicious = new Dictionary<int, Dictionary<int, double>>();


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
            if (global.debug)
                Console.WriteLine("{0}{1} check routing.", type, Id);
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


        private MODEventTrustResult GetEventTrustResult(MODPhenomemon p, List<MODEventTrustResult> results)
        {
            bool forge = false;
            MODEventTrustResult result = null;
            int nodeId = p.nodeId;
            string pkgIdent = MODEventTrust.GetPacketIdent(p.pkg);

            bool isAccept = false;
            bool isSupport = false;
            //如果是恶意节点，则会考察检测节点可能的动作
            if (this.IsMalicious())
            {
                if (global.deduceMethod == DeduceMethod.Native)//原始的话，恶意节点无条件伪造报告

                    forge = true;
            }

            else if (global.deduceMethod == DeduceMethod.Game)
            {
                //如果是博弈论，则判断检测节点的观点
                //此处仅以其周围邻居为参考，而非报告节点的邻居，这是由于ad-hoc的局限性所致的
                Dictionary<int, MODEventTrustResult> localcachedresults = new Dictionary<int, MODEventTrustResult>();
                foreach (MODEventTrustResult r in results)
                {
                    localcachedresults.Add(r.reportNode, r);
                }
                //初始化邻居的结构，且找到最久的邻居
                int minNbId = -1;
                double minBeacon = 10000;
                foreach (int nbId in this.Neighbors.Keys)
                {
                    //如果已经保存，则继续
                    if (localcachedresults.ContainsKey(nbId))
                        continue;
                    MODReader nbNode = (MODReader)global.readers[nbId];
                    MODEventTrustResult r = null;
                    if (nbNode.IsMalicious())
                    {
                        r = MODEventTrust.ForgeMaliciousEventTrustResult(p.nodeId, this.Id, MODEventTrust.GetPacketIdent(p.pkg), MODEventCategoryType.DropPacket);
                        r.normal = -1;
                    }
                    else
                    {
                        r = new MODEventTrustResult(p.pkg.Prev, nbId, pkgIdent, MODEventCategoryType.DropPacket, null);
                        r.normal = 1;
                    }
                    localcachedresults.Add(nbId, r);

                    if (minBeacon < this.Neighbors[nbId].firstBeacon)
                    {
                        minBeacon = Neighbors[nbId].firstBeacon;
                        minNbId = nbId;
                    }
                }


                if (!this.pNBNormal.ContainsKey(minNbId))
                {
                    this.pNBNormal.Add(minNbId, new Dictionary<int, double>());
                    this.pNBSupportByNormal.Add(minNbId, new Dictionary<int, double>());
                    this.pNBSupportByMalicious.Add(minNbId, new Dictionary<int, double>());
                    this.pNBNonsupportByNormal.Add(minNbId, new Dictionary<int, double>());
                    this.pNBNonsupportByMalicious.Add(minNbId, new Dictionary<int, double>());
                }
                if (!this.pNBNormal[minNbId].ContainsKey(this.Id))
                {
                    this.pNBNormal[minNbId].Add(this.Id, global.pInitNormal);
                    this.pNBSupportByNormal[minNbId].Add(this.Id, global.pInitSupportByNormal);
                    this.pNBSupportByMalicious[minNbId].Add(this.Id, global.pInitSupportByMalicious);
                    this.pNBNonsupportByNormal[minNbId].Add(this.Id, global.pInitNonsupportByNormal);
                    this.pNBNonsupportByMalicious[minNbId].Add(this.Id, global.pInitNonsupportByMalicious);
                }


                //这个邻居节点对该节点邻居的印象
                Dictionary<int, double> pMaliciousBySupport = new Dictionary<int, double>();
                Dictionary<int, double> pMaliciousByNonsupport = new Dictionary<int, double>();
                Dictionary<int, double> pNormalBySupport = new Dictionary<int, double>();
                Dictionary<int, double> pNormalByNonsupport = new Dictionary<int, double>();

                int[] reportNodes = localcachedresults.Keys.ToArray();
                foreach (int reportNode in reportNodes)
                {
                    double pMalicious = 1 - this.pNBNormal[minNbId][reportNode];
                    double p1 = pMalicious * this.pNBSupportByNormal[minNbId][reportNode] + this.pNBNormal[minNbId][reportNode] * this.pNBSupportByNormal[minNbId][reportNode];
                    pMaliciousBySupport[reportNode] = pMalicious * this.pNBSupportByMalicious[minNbId][reportNode] / p1;
                    pNormalBySupport[reportNode] = this.pNBNormal[minNbId][reportNode] * this.pNBSupportByNormal[minNbId][reportNode] / p1;

                    double p2 = pMalicious * this.pNBSupportByMalicious[minNbId][reportNode] + this.pNBNormal[minNbId][reportNode] * this.pNBNonsupportByMalicious[minNbId][reportNode];
                    pMaliciousByNonsupport[reportNode] = pMalicious * this.pNBNonsupportByMalicious[minNbId][reportNode] / p2;
                    pNormalByNonsupport[reportNode] = this.pNBNormal[minNbId][reportNode] * this.pNBNonsupportByNormal[minNbId][reportNode] / p2;

                }



                //然后模拟计算
                if (global.deduceMethod == DeduceMethod.Game)
                {
                    isAccept = DeduceA2(reportNodes, true, nodeId, localcachedresults, pMaliciousBySupport, pNormalBySupport, pMaliciousByNonsupport, pNormalByNonsupport);
                    isSupport = DeduceA1(isAccept);
                    if (isSupport == false)//如果检测节点不同意，则返回
                        forge = true;
                }
            }
            else
            {
                forge = true;
            }


            if (!this.IsMalicious() && forge == true)
            {
                result = MODEventTrust.DeduceDropPacketMaliciouslyByPacket(this.Id, this.observedPhenomemons, scheduler.currentTime, p);

            }
            else //恶意节点
            {
                result = MODEventTrust.ForgeMaliciousEventTrustResult(p.nodeId, this.Id,
                    MODEventTrust.GetPacketIdent(p.pkg), MODEventCategoryType.DropPacket);
            }
            return result;
        }


        //将接收到的数据包添加到观察到的现象中
        public void AddReceivePacketPhenomemon(Packet pkg)
        {
            MODPhenomemon p;
            this.totalReceivedPackets++;
            //忽略广播包(从实际来看，发送广播包的一般是节点本身的行为，不需要考虑其对数据包的恶意操作)
            if (pkg.Next == BroadcastNode.Node.Id)
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

            //记录接收现象
            /*
            if (pkg.Next != pkg.Dst)
            {
                p = new MODPhenomemon(MODPhenomemonType.RECV_PACKET, pkg.Next, scheduler.currentTime, pkg);
                p.likehood = global.recvLikehood;
                this.observedPhenomemons.Add(p);
                if (global.debug)
                    Console.WriteLine("[Debug] reader{0} add a SEND phenomemon of reader{1}", Id, pkg.Prev);
            }*/

        }


        //恶意观察节点推断采取的行为
        public bool DeduceA1(bool isAccept)
        {
            bool isSupport = false;
            double aSupport = 0, aNonsupport = 0;
            if (isAccept)
            {
                //计算效用函数与后验概率的乘积
                aSupport = global.uA1MaliciousAndSupportAndAccept;
                aNonsupport = global.uA1MaliciousAndNonsupportAndAccept;
            }
            else
            {
                aSupport = global.uA1MaliciousAndSupportAndReject;
                aNonsupport = global.uA1MaliciousAndNonsupportAndReject;
            }
            isSupport = (aSupport > aNonsupport);
            return isSupport;
        }


        //对最终邻居的结果进行分析
        public void DeduceEventType(string ident)
        {
            if(MODEventTrustResult.DeducedPackets.Contains(ident))
                return;
            Console.WriteLine("Reader{0} Deduces Event Type for {1}", this.Id, ident);
            Dictionary<int, MODEventTrustResult> cachedresults = null;
            if (this.receivedEventReports.ContainsKey(ident))
                cachedresults = this.receivedEventReports[ident];
            else
                throw new Exception("no such an ident in receivedEventReports");

            int nodeId = -1;
            foreach (KeyValuePair<int, MODEventTrustResult> r in cachedresults)
            {
                nodeId = r.Value.node;
                break;
            }

            if (global.deduceMethod == DeduceMethod.Native)
            {
                int normalCount = 0, maliciousCount = 0;
                foreach (KeyValuePair<int, MODEventTrustResult> r in cachedresults)
                {
                    if (r.Value.normal > 0)
                        normalCount++;
                    else
                        maliciousCount++;

                    Console.Write("{0}:{1}\t",r.Key, r.Value.normal);
                }
                if (normalCount > maliciousCount)
                    Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4}{5} is normal. {6}-{7}", scheduler.currentTime, "DEDUCTION", this.type, this.Id, NodeType.READER, nodeId, normalCount, maliciousCount);
                else
                    Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4}{5} is malicious. {6}-{7}", scheduler.currentTime, "DEDUCTION", this.type, this.Id, NodeType.READER, nodeId, normalCount, maliciousCount);
            }
            else
            {
                if(!cachedresults.ContainsKey(this.Id))
                {
                    throw new Exception("myresult result is null");
                }

                //先算先验概率和后验概率
                foreach(KeyValuePair<int, MODEventTrustResult> k in cachedresults)
                {
                    if (!this.pNormal.ContainsKey(k.Key))
                        this.pNormal.Add(k.Key, global.pInitNormal);
                    if (!this.pSupportByMalicious.ContainsKey(k.Key))
                        this.pSupportByMalicious.Add(k.Key, global.pInitSupportByMalicious);
                    if (!this.pSupportByNormal.ContainsKey(k.Key))
                        this.pSupportByNormal.Add(k.Key, global.pInitSupportByNormal);
                    if (!this.pNonsupportByNormal.ContainsKey(k.Key))
                        this.pNonsupportByNormal.Add(k.Key, global.pInitNonsupportByNormal);
                    if (!this.pNonsupportByMalicious.ContainsKey(k.Key))
                        this.pNonsupportByMalicious.Add(k.Key, global.pInitNonsupportByMalicious);
                }
                Dictionary<int, double> pMaliciousBySupport = new Dictionary<int,double>();
                Dictionary<int, double> pMaliciousByNonsupport = new Dictionary<int, double>();
                Dictionary<int, double> pNormalBySupport = new Dictionary<int,double>();
                Dictionary<int, double> pNormalByNonsupport = new Dictionary<int,double>();

                int[] reportNodes = cachedresults.Keys.ToArray();                
                foreach (int reportNode in reportNodes)
                {
                    double pMalicious = 1 - this.pNormal[reportNode];
                    double p1 = pMalicious * this.pSupportByMalicious[reportNode] + this.pNormal[reportNode] * this.pSupportByNormal[reportNode];
                    pMaliciousBySupport[reportNode] = pMalicious * this.pSupportByMalicious[reportNode] / p1;
                    pNormalBySupport[reportNode] = this.pNormal[reportNode] * this.pSupportByNormal[reportNode] / p1;

                    double p2 = pMalicious * this.pSupportByMalicious[reportNode] + this.pNormal[reportNode] * this.pNonsupportByMalicious[reportNode];
                    pMaliciousByNonsupport[reportNode] = pMalicious * this.pNonsupportByMalicious[reportNode] / p2;
                    pNormalByNonsupport[reportNode] = this.pNormal[reportNode] * this.pNonsupportByNormal[reportNode] / p2;

                }


                if (global.deduceMethod == DeduceMethod.Game)
                {

                    bool isAccept = DeduceA2(reportNodes, true, nodeId, cachedresults, pMaliciousBySupport, pNormalBySupport, pMaliciousByNonsupport, pNormalByNonsupport);
                    //Console.WriteLine("{0:F4} [{1}] {2}{3} deduces: {4}-{5}", scheduler.currentTime, "DEDUCTION", this.type, this.Id, pGroupNormal, pGroupMalicious);
                }
                else
                {
                }
            }

            MODEventTrustResult.DeducedPackets.Add(ident);
            return;
        }

        public bool DeduceA2(int[] reportNodes, bool isDeduceStep2, int nodeId, Dictionary<int, MODEventTrustResult> cachedresults, 
            Dictionary<int, double> pMaliciousBySupport, Dictionary<int, double> pNormalBySupport,
            Dictionary<int, double> pMaliciousByNonsupport, Dictionary<int, double> pNormalByNonsupport)
        {

            double pGroupNormal = 0, pGroupMalicious = 0;
            double pGroupNormalBySupport = 0, pGroupMaliciousBySupport = 0;
            double pGroupNormalByNonsupport = 0, pGroupMaliciousByNonsupport = 0;

            Combination c = new Combination();

            //只计算占优势的一方
            for (int m = reportNodes.Length / 2 + 1; m < reportNodes.Length; m++)
            {
                List<int[]> list = c.combination(reportNodes, m);

                for (int i = 0; i < list.Count; i++)
                {
                    double p1 = 1, p2 = 1, p3 = 1, p4 = 1, p5 = 1, p6 = 1;
                    int[] temp = (int[])list[i];
                    HashSet<int> usedId = new HashSet<int>();
                    for (int j = 0; j < temp.Length; j++)
                    {
                        double pMalicious = 1 - this.pNormal[temp[j]];
                        p1 = p1 * this.pNormal[temp[j]];
                        p2 = p2 * pMalicious;
                        p3 = p3 * pNormalBySupport[temp[j]];
                        p4 = p4 * pMaliciousBySupport[temp[j]];
                        p5 = p5 * pNormalByNonsupport[temp[j]];
                        p6 = p6 * pMaliciousByNonsupport[temp[j]];
                        usedId.Add(temp[j]);
                        //Console.Write("{0}*", this.pNormal[temp[j]]);
                    }

                    foreach (int node in cachedresults.Keys)//找出异常的节点
                    {
                        if (!usedId.Contains(node))
                        {
                            double pMalicious = 1 - this.pNormal[node];
                            p1 = p1 * pMalicious;
                            p2 = p2 * this.pNormal[node];

                            p3 = p3 * pMaliciousBySupport[node];
                            p4 = p4 * pNormalBySupport[node];

                            p5 = p5 * pMaliciousByNonsupport[node];
                            p6 = p6 * pNormalByNonsupport[node];
                        }
                    }
                    //Console.Write("{0}\t", p1);
                    pGroupNormal = pGroupNormal + p1;
                    pGroupMalicious = pGroupMalicious + p2;

                    pGroupNormalBySupport = pGroupNormalBySupport + p3;
                    pGroupMaliciousBySupport = pGroupMaliciousBySupport + p4;
                    pGroupNormalByNonsupport = pGroupNormalByNonsupport + p5;
                    pGroupMaliciousByNonsupport = pGroupMaliciousByNonsupport + p6;



                }
                Console.WriteLine("{0}\t{1}", list.Count, pGroupNormal);
            }

            int nsupport = 0, nnonsupport = 0;
            bool supportReport = false;
            //计算报告是support还是nonsupport
            foreach (MODEventTrustResult result in cachedresults.Values)
            {
                if (result.normal > 0)
                    nsupport++;
                else
                    nnonsupport++;
            }

            if (nsupport > nnonsupport)
                supportReport = true;
            else
                supportReport = false;


            double aAccept = 0, aReject = 0;
            bool isAccept = false;

            if (supportReport)
            {
                //计算效用函数与后验概率的乘积
                double aNormalAndSupportAndAccept = pGroupNormalBySupport * global.uA2NormalAndSupportAndAccept;
                double aMaliciousAndSupportAndAccept = pGroupNormalBySupport * global.uA2MaliciousAndSupportAndAccept;

                double aNormalAndSupportAndReject = pGroupNormalBySupport * global.uA2NormalAndSupportAndReject;
                double aMaliciousAndSupportAndReject = pGroupNormalBySupport * global.uA2MaliciousAndSupportAndReject;

                aAccept = aNormalAndSupportAndAccept + aMaliciousAndSupportAndAccept;
                aReject = aNormalAndSupportAndReject + aMaliciousAndSupportAndReject;

                isAccept = (aAccept > aReject);
                if (isAccept)
                {
                    Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4}{5} is accept. {6}-{7}", scheduler.currentTime, "DEDUCTION", this.type, this.Id, NodeType.READER, nodeId, aAccept, aReject);
                    PunishMaliciousNodes(aNormalAndSupportAndAccept - aMaliciousAndSupportAndAccept > 0, cachedresults);
                }
                else
                {
                    Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4}{5} is reject. {6}-{7}}", scheduler.currentTime, "DEDUCTION", this.type, this.Id, NodeType.READER, nodeId, aAccept, aReject);
                    PunishMaliciousNodes(aNormalAndSupportAndReject - aMaliciousAndSupportAndReject > 0, cachedresults);
                }
                return isAccept;


            }
            else
            {
                double aNormalAndNonsupportAndAccept = pGroupNormalByNonsupport * global.uA2NormalAndNonsupportAndAccept;
                double aMaliciousAndNonsupportAndAccept = pGroupNormalByNonsupport * global.uA2MaliciousAndNonsupportAndAccept;

                double aNormalAndNonsupportAndReject = pGroupNormalByNonsupport * global.uA2NormalAndNonsupportAndReject;
                double aMaliciousAndNonsupportAndReject = pGroupNormalByNonsupport * global.uA2MaliciousAndNonsupportAndReject;

                aAccept = aNormalAndNonsupportAndAccept + aMaliciousAndNonsupportAndAccept;
                aReject = aNormalAndNonsupportAndReject + aMaliciousAndNonsupportAndReject;


                isAccept = (aAccept > aReject);

                if (isDeduceStep2)//如果是检测节点处理的话，需要惩罚，否则不需要
                {
                    if (isAccept)
                    {
                        Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4}{5} is accept. {6}-{7}", scheduler.currentTime, "DEDUCTION", this.type, this.Id, NodeType.READER, nodeId, aAccept, aReject);
                        PunishMaliciousNodes(aNormalAndNonsupportAndAccept - aMaliciousAndNonsupportAndAccept > 0, cachedresults);
                    }
                    else
                    {
                        Console.WriteLine("{0:F4} [{1}] {2}{3} deduces {4}{5} is reject. {6}-{7}}", scheduler.currentTime, "DEDUCTION", this.type, this.Id, NodeType.READER, nodeId, aAccept, aReject);
                        PunishMaliciousNodes(aNormalAndNonsupportAndReject - aMaliciousAndNonsupportAndReject > 0, cachedresults);
                    }
                }
                return isAccept;
            }
        }


        public void PunishMaliciousNodes(bool normal, Dictionary<int, MODEventTrustResult> results)
        {
            foreach (KeyValuePair<int, MODEventTrustResult> result in results)
            {
                int nodeId = result.Key;

                if (normal == (result.Value.normal > 0))//两者一致
                {
                    this.pNormal[nodeId] = this.pNormal[nodeId] * global.RewardFactor;
                }
                else
                {
                    this.pNormal[nodeId] = this.pNormal[nodeId] * global.PunishmentFactor;
                }
            }

            //这里就不通过发数据包了，直接更改邻居节点的评价了
            foreach (int nbId in this.Neighbors.Keys)
            {
                //如果邻居
                if (results.ContainsKey(nbId))
                {
                    MODReader nbNode = (MODReader)global.readers[nbId];
                    if (!nbNode.pNBNormal.ContainsKey(this.Id))
                    {
                        nbNode.pNBNormal.Add(this.Id, new Dictionary<int, double>());
                        nbNode.pNBSupportByNormal.Add(this.Id, new Dictionary<int, double>());
                        nbNode.pNBSupportByMalicious.Add(this.Id, new Dictionary<int, double>());
                        nbNode.pNBNonsupportByNormal.Add(this.Id, new Dictionary<int, double>());
                        nbNode.pNBNonsupportByMalicious.Add(this.Id, new Dictionary<int, double>());
                    }
                    if (!nbNode.pNBNormal[this.Id].ContainsKey(nbId))
                    {
                        nbNode.pNBNormal[this.Id].Add(nbId, global.pInitNormal);
                        nbNode.pNBSupportByNormal[this.Id].Add(nbId, global.pInitSupportByNormal);
                        nbNode.pNBSupportByMalicious[this.Id].Add(nbId, global.pInitSupportByMalicious);
                        nbNode.pNBNonsupportByNormal[this.Id].Add(nbId, global.pInitNonsupportByNormal);
                        nbNode.pNBNonsupportByMalicious[this.Id].Add(nbId, global.pInitNonsupportByMalicious);
                    }
                    if(normal == (results[nbId].normal > 0))
                        nbNode.pNBNormal[this.Id][nbId] *= global.RewardFactor;
                    else
                        nbNode.pNBNormal[this.Id][nbId] *= global.PunishmentFactor;
                }
            }
        }


        //在接收到DATA数据包后，检查是否发生了抛弃数据包的情况
        public void CheckReceivedPacket(MODPhenomemon p)
        {
            int nodeId = p.nodeId;
            List<MODEventTrustResult> results = new List<MODEventTrustResult>();

            Console.WriteLine("[Debug] node{0} CheckReceivedPacket of {1}", this.Id, MODEventTrust.GetPacketIdent(p.pkg));

            //TODO,这里假设只考虑节点1
            if (p.pkg.Prev != 1)
                return;

            MODEventTrustResult result = GetEventTrustResult(p, results);

            string ident = result.eventIdent;
            if (!this.receivedEventReports.ContainsKey(ident))
            {
                this.receivedEventReports.Add(ident, new Dictionary<int, MODEventTrustResult>());
                this.receivedEventReports[ident].Add(this.Id, result);
            }


            //如果是恶意节点，则会考察检测节点可能的动作
            if (this.IsMalicious() && global.deduceMethod == DeduceMethod.Game)
            {
                //如果是博弈论，则判断检测节点的观点
                //此处仅以其周围邻居为参考，而非报告节点的邻居，这是由于ad-hoc的局限性所致的
                Dictionary<int, MODEventTrustResult> cachedresults = new Dictionary<int, MODEventTrustResult>();
                //初始化邻居的结构，且找到最久的邻居
                int minNbId = -1;
                double minBeacon = 10000;
                foreach (int nbId in this.Neighbors.Keys)
                {
                    MODReader nbNode = (MODReader)global.readers[nbId];
                    MODEventTrustResult r = new MODEventTrustResult(p.pkg.Prev, nbId, ident, MODEventCategoryType.DropPacket, null);
                    if (nbNode.IsMalicious())
                    {
                        r.normal = -1;
                    }
                    else
                    {
                        r.normal = 1;
                    }
                    cachedresults.Add(nbId, r);

                    if (minBeacon < this.Neighbors[nbId].firstBeacon)
                    {
                        minBeacon = Neighbors[nbId].firstBeacon;
                        minNbId = nbId;
                    }
                }


                if (!this.pNBNormal.ContainsKey(minNbId))
                {
                    this.pNBNormal.Add(minNbId, new Dictionary<int, double>());
                    this.pNBSupportByNormal.Add(minNbId, new Dictionary<int, double>());
                    this.pNBSupportByMalicious.Add(minNbId, new Dictionary<int, double>());
                    this.pNBNonsupportByNormal.Add(minNbId, new Dictionary<int, double>());
                    this.pNBNonsupportByMalicious.Add(minNbId, new Dictionary<int, double>());
                }
                if (!this.pNBNormal[minNbId].ContainsKey(this.Id))
                {
                    this.pNBNormal[minNbId].Add(this.Id, global.pInitNormal);
                    this.pNBSupportByNormal[minNbId].Add(this.Id, global.pInitSupportByNormal);
                    this.pNBSupportByMalicious[minNbId].Add(this.Id, global.pInitSupportByMalicious);
                    this.pNBNonsupportByNormal[minNbId].Add(this.Id, global.pInitNonsupportByNormal);
                    this.pNBNonsupportByMalicious[minNbId].Add(this.Id, global.pInitNonsupportByMalicious);
                }


                //这个邻居节点对该节点邻居的印象
                Dictionary<int, double> pMaliciousBySupport = new Dictionary<int, double>();
                Dictionary<int, double> pMaliciousByNonsupport = new Dictionary<int, double>();
                Dictionary<int, double> pNormalBySupport = new Dictionary<int, double>();
                Dictionary<int, double> pNormalByNonsupport = new Dictionary<int, double>();

                int[] reportNodes = cachedresults.Keys.ToArray();
                foreach (int reportNode in reportNodes)
                {
                    double pMalicious = 1 - this.pNBNormal[minNbId][reportNode];
                    double p1 = pMalicious * this.pNBSupportByNormal[minNbId][reportNode] + this.pNBNormal[minNbId][reportNode] * this.pNBSupportByNormal[minNbId][reportNode];
                    pMaliciousBySupport[reportNode] = pMalicious * this.pNBSupportByMalicious[minNbId][reportNode] / p1;
                    pNormalBySupport[reportNode] = this.pNBNormal[minNbId][reportNode] * this.pNBSupportByNormal[minNbId][reportNode] / p1;

                    double p2 = pMalicious * this.pNBSupportByMalicious[minNbId][reportNode] + this.pNBNormal[minNbId][reportNode] * this.pNBNonsupportByMalicious[minNbId][reportNode];
                    pMaliciousByNonsupport[reportNode] = pMalicious * this.pNBNonsupportByMalicious[minNbId][reportNode] / p2;
                    pNormalByNonsupport[reportNode] = this.pNBNormal[minNbId][reportNode] * this.pNBNonsupportByNormal[minNbId][reportNode] / p2;

                }


                //然后模拟计算
                if (global.deduceMethod == DeduceMethod.Game)
                {
                    bool isAccept = DeduceA2(reportNodes, true, nodeId, cachedresults, pMaliciousBySupport, pNormalBySupport, pMaliciousByNonsupport, pNormalByNonsupport);
                    if (isAccept == false)//如果检测节点不同意，则返回
                        return;
                }
            }


            //正常节点
            if (result.normal > 0 && this.receivedEventReports[ident][this.Id].normal == result.normal)//normal
                return;
            //报告恶意事件
            if (global.debug)
                Console.WriteLine("reader{0} report a event of {1} of reader{2}", Id, result.eventIdent, result.node);

            /*
            results.Add(result);

            
            byte[] buf = new byte[global.BufSize];
            MemoryStream ms = new MemoryStream(buf);
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, results);
            byte[] tmp = new byte[ms.Position];
            Array.Copy(buf, tmp, ms.Position);

            Packet pkg = new Packet(this, BroadcastNode.Node, PacketType.EVENT_REPORT);
            pkg.TrustReport = new TrustReportField(0, tmp, tmp.Length);
            SendPacketDirectly(scheduler.currentTime, pkg);
             * */

            //过一段时间转发事件报告
            Event.AddEvent(new Event(scheduler.currentTime + 0.05f, EventType.FWD_EVENT_REPORT, this, ident));
            //过一段时间检查事件报告
            Event.AddEvent(new Event(scheduler.currentTime + global.checkPhenomemonTimeout, EventType.DEDUCE_EVENT, this, ident));
        }

        public bool IsMalicious()
        {
            return this.readerType != ReaderType.NORMAL || ((MODOrganization)global.orgs[this.OrgId]).IsMalicious;
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

            string ident = results[0].eventIdent;
            int suspectedNodeId = results[0].node;
            if (!this.Neighbors.ContainsKey(suspectedNodeId))
                return;

            if (global.debug)
                Console.WriteLine("READER{0} recv {1} reports. ident:{2}", Id, results.Count, ident);


            Dictionary<int, MODEventTrustResult> cachedresults = null;
            if (!this.receivedEventReports.ContainsKey(ident))
            {
                this.receivedEventReports.Add(ident, new Dictionary<int, MODEventTrustResult>());
            }

            int newcount = 0;

            cachedresults = this.receivedEventReports[ident];
            //不在cachedresults的新项
            foreach (MODEventTrustResult r in results)
            {
                if (!cachedresults.ContainsKey(r.reportNode))
                {
                    newcount++;
                    cachedresults.Add(r.reportNode, r);
                }
            }
            if (newcount == 0)//和以前的一样，返回即可
                return;

            if (!cachedresults.ContainsKey(this.Id) || cachedresults[this.Id].eventIdent=="")
            {
                MODPhenomemon p = MODEventTrust.GetPhenomemon(ident, this.Id, this.observedPhenomemons);
                MODEventTrustResult myresult = null;
                if (p != null)//观察到现象
                {
                    myresult = GetEventTrustResult(p, cachedresults.Values.ToList());
                    cachedresults.Add(this.Id, myresult);
                }
                else
                {
                    myresult = MODEventTrust.NotObservedEventTrustResult(suspectedNodeId, this.Id,
                            "", MODEventCategoryType.DropPacket);
                    cachedresults[this.Id] = myresult;
                }
                //TODO 这里涉及到没有观察到收到数据包，应该是不支持节点异常的 myresult.normal = //;
                
            }

            foreach (MODEventTrustResult r in cachedresults.Values)
            {
                Console.Write("{0}:{1}\t", r.reportNode, r.normal);
            }
            Console.WriteLine();

            //过一段时间转发事件报告
            Event.AddEvent(new Event(scheduler.currentTime + 0.05f, EventType.FWD_EVENT_REPORT, this, ident));

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
                Console.WriteLine("[ForwardEventReport] {0} reports\tident:{1}\t", results.Count, ident);
            
            Packet pkg = new Packet(this, BroadcastNode.Node, PacketType.EVENT_REPORT);
            pkg.TrustReport = new TrustReportField(0, tmp, tmp.Length);
            SendPacketDirectly(scheduler.currentTime, pkg);            
        }


        public override void Recv(Packet pkg)
        {
            pkg.seqInited = false;
            //只有reader才需要检查，但是里面函数处理了
            CheckPacketCount(pkg);

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

            //如果不存在邻居中，则添加.
            //如果存在，则更新时间
            //if (pkg.Beacon == null && !this.Neighbors.ContainsKey(pkg.Prev) && pkg.PrevType == NodeType.READER)
            if (pkg.Beacon == null && pkg.PrevType == NodeType.READER)
                RecvBeacon(pkg);

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
