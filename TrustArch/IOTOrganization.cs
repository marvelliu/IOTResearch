using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;

namespace TrustArch
{
    public class IOTOrganizationTrust : IComparable<IOTOrganizationTrust>
    {
        public int id;
        public double trustValue;
        public IOTOrganizationTrust(int id, double trustValue)
        {
            this.id = id;
            this.trustValue = trustValue;
        }


        public int CompareTo(IOTOrganizationTrust other)
        {
            return this.trustValue.CompareTo(other.trustValue);
        }
    }

    public class OrgDirectTrust
    {
        public int count;
        public double trustValue;
    }

    public class IOTOrganization:Organization
    {
        Dictionary<int, List<int>> ownNodes;
        Dictionary<int, List<int>> trustNodes;
        IOTGlobal global;

        List<IOTNodeTrustResult> cachedNodeTrustResult;
        Dictionary<int, IOTNodeTrustTypeResult> cacheHistoricaldNodeTrustResult;
        Dictionary<int, Dictionary<int, HashSet<int>>> cachedMonitorRequests;
        Dictionary<int, OrgDirectTrust> cachedOrgTrustResult;

        new public static IOTOrganization ProduceOrganization(int id, string name)
        {
            return new IOTOrganization(id, name);
        }

        public IOTOrganization(int id, string name)
            : base(id, name)
        {
            global = (IOTGlobal)Global.getInstance();
            this.ownNodes = new Dictionary<int, List<int>>();
            this.trustNodes = new Dictionary<int, List<int>>();
            this.cachedNodeTrustResult = new List<IOTNodeTrustResult>();
            this.cacheHistoricaldNodeTrustResult = new Dictionary<int, IOTNodeTrustTypeResult>();
            this.cachedMonitorRequests = new Dictionary<int,Dictionary<int,HashSet<int>>>();
            this.cachedOrgTrustResult = new Dictionary<int, OrgDirectTrust>();
            CheckRoutine();
        }

        new public static void GenerateNodes()
        {
            Global global = Global.getInstance();
            for (int i = 0; i < global.readerNum; i++)
            {
                Reader reader = global.readerConstructor(i, (int)Utility.U_Rand(0, global.orgNum)); 
                global.readers[i] = reader;
            }
        }

        public void AssignMonitorReader(int node)
        {
            IOTReader r = (IOTReader)global.readers[node];
            r.AssignMonitor(this.Id);
        }


        public override void Recv(Packet pkg)
        {

            switch (pkg.Type)
            {
                case PacketType.TAG_HEADER:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    RecvTagHeader(pkg);
                    break;
                case PacketType.NODE_REPORT:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    RecvNodeTrustReport(pkg);
                    break;
                case PacketType.DATA:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    RecvData(pkg);
                    break;
                default:
                    throw new Exception("Org receives unknown packet type: "+pkg.Type);
            }
        }

        public void RecvData(Packet pkg)
        {
            if (pkg.Dst != Id || pkg.DstType != type)
                throw new Exception("FATAL: wrong destation");
            Console.WriteLine("{0}{1} recv data.",this.type, this.Id);
        }

        public void RecvTagHeader(Packet pkg)
        {
            int tagId = pkg.ObjectTagHeader.tagId;
            int orgId = pkg.ObjectTagHeader.orgId;
            int networkId = pkg.ObjectTagHeader.networkId;
            int requestNode = pkg.Src;
            int gwNode = pkg.Prev;

            if (!EvaluateNodeTrust(requestNode))
            {
                Console.WriteLine("Reader{0} is not trustable", requestNode);
                return;
            }

            //查找信任的节点
            //首先从缓存中寻找
            int monitorNode = FindTrustNodeFromCache(networkId);
            //如果找不到，则在该ad-hoc网络中查找
            //TODO 可引入节点信任和机构信任两个因素
            if (monitorNode < 0)
            {
                if (FindTrustNodeFromNetwork(networkId, gwNode) > 0)
                {
                    if (!this.cachedMonitorRequests.ContainsKey(networkId))
                        this.cachedMonitorRequests.Add(networkId, new Dictionary<int, HashSet<int>>());
                    if (!this.cachedMonitorRequests[networkId].ContainsKey(requestNode))
                        this.cachedMonitorRequests[networkId].Add(requestNode, new HashSet<int>());
                    if (!this.cachedMonitorRequests[networkId][requestNode].Contains(tagId))
                        this.cachedMonitorRequests[networkId][requestNode].Add(tagId);
                }
            }
            else
            {
                //send auth key to the reader..
                Packet pkg1 = new Packet(this, global.readers[requestNode], PacketType.AUTHORIZATION);
                pkg1.Next = pkg.Prev;
                pkg1.NextType = pkg.PrevType;
                //TODO auth key should be made.
                pkg1.Authorization = new AuthorizationField(new int[]{pkg.ObjectTagHeader.tagId}, new int[]{pkg.ObjectTagHeader.tagId});
                SendPacketDirectly(scheduler.currentTime, pkg1);
            }

        }


        bool EvaluateNodeTrust(int node)
        {
            if (this.cacheHistoricaldNodeTrustResult.ContainsKey(node))
            {
                IOTNodeTrustTypeResult nodeTrustType = cacheHistoricaldNodeTrustResult[node];
                if (nodeTrustType.type != IOTNodeType.NORMAL)
                    return false;
            }
            return true;
        }

        public int FindTrustNodeFromCache(int network)
        {
            //先从缓存中找
            if (this.trustNodes.ContainsKey(network))
            {
                List<int> l = this.trustNodes[network];
                if (l.Count > 0)
                {
                    int m = (int)Utility.U_Rand(l.Count);
                    return l[m];
                }
            }
            //然后找自己的节点
            if (this.ownNodes.ContainsKey(network))
            {
                List<int> l = this.ownNodes[network];
                if (l.Count > 0)
                {
                    int m = (int)Utility.U_Rand(l.Count);
                    return l[m];
                }                    
            }
            return -1;
        }

        public int FindTrustNodeFromNetwork(int network, int fromReader)
        {
            /* 
             * DOC:如果找不到可信的节点，则尝试下列方法：
             * 1 找出可信的机构，在该ad-hoc网络发送请求，找到属于该可信机构的阅读器
             * 2 找出ad-hoc网络中较为可信的节点
             */

            //以前发过请求，直接返回
            if (this.cachedMonitorRequests.ContainsKey(network))
                return 1;

            //这里，我们节省一个步骤，即向trustmanager索取机构信任的步骤，可认为是每隔一段时间进行的
            List<IOTOrganizationTrust> orgTrusts = global.trustManager.getSortedOrgReputations();
            List<int> temp = new List<int>();
            int maxCount = Math.Min(orgTrusts.Count, 5);
            for (int i = 0; i < maxCount; i++)
            {
                //每个机构的信誉值至少大于一个阈值
                if(orgTrusts[i].trustValue>0.5)
                    temp.Add(orgTrusts[i].id);
            }
            int[] orgs = temp.ToArray();
            if (orgs.Length == 0)
            {
                Console.WriteLine("No trustable orgs, abort.");
                return -1;
            }
            
            Packet pkg = new Packet(this, global.readers[fromReader], PacketType.GET_MONITOR_REQUEST);
            pkg.GetMonitorRequest = new GetMonitorRequestField(orgs, network, this.Id);
            SendPacketDirectly(scheduler.currentTime, pkg);
            return 1;
        }

        //接收到监控节点发出的节点信任的报告
        public void RecvNodeTrustReport(Packet pkg)
        {
            int org = pkg.TrustReport.org;
            if (Id != org)
            {
                Console.WriteLine("Wrong organization {0}\n", org);
            }
            MemoryStream ms = new MemoryStream(pkg.TrustReport.result);
            BinaryFormatter formatter = new BinaryFormatter();
            List<IOTNodeTrustResult> result = (List<IOTNodeTrustResult>)formatter.Deserialize(ms);
            foreach (IOTNodeTrustResult r in result)
                this.cachedNodeTrustResult.Add(r);
        }

        //每隔一段时间检查一下节点的信任值
        public void CheckRoutine()
        {
            if (global.debug)
                Console.WriteLine("{0}{1} check routing.", type, Id);
            if (this.cachedNodeTrustResult.Count > 0)
            {
                if (global.debug)
                    Console.WriteLine("ORG{0} reports:{1}", Id, this.cachedNodeTrustResult.Count);
                //先将收集到的节点报告根据节点分类
                Dictionary<int, List<IOTNodeTrustResult>> hashedNodeTrustResult =
                    new Dictionary<int, List<IOTNodeTrustResult>>();
                foreach (IOTNodeTrustResult nodeTrust in this.cachedNodeTrustResult)
                {
                    int nodeId = nodeTrust.nodeId;
                    if (!hashedNodeTrustResult.ContainsKey(nodeId))
                        hashedNodeTrustResult.Add(nodeTrust.nodeId, new List<IOTNodeTrustResult>());
                    hashedNodeTrustResult[nodeId].Add(nodeTrust);
                }

                //计算每个节点的信任值，放入combinedNodeTrustResult中
                List<IOTNodeTrustTypeResult> combinedNodeTrustResult = new List<IOTNodeTrustTypeResult>();
                foreach (KeyValuePair<int, List<IOTNodeTrustResult>> k in hashedNodeTrustResult)
                {
                    int node = k.Key;
                    List<IOTNodeTrustResult> nodeTrusts = k.Value;
                    IOTNodeTrustResult combinedNodeTrust = DeduceNodeTrust(node, nodeTrusts);
                    IOTNodeTrustTypeResult nodeTrustType = DeduceNodeFinalTrustType(node, combinedNodeTrust);
                    if (nodeTrustType.type != IOTNodeType.NORMAL)
                    {
                        Console.WriteLine("{0}{1} deduces Reader{2} not work well", type, Id, nodeTrustType.nodeId);
                        combinedNodeTrustResult.Add(nodeTrustType);
                        if (!this.cacheHistoricaldNodeTrustResult.ContainsKey(node))
                            this.cacheHistoricaldNodeTrustResult.Add(node, nodeTrustType);
                        else
                            this.cacheHistoricaldNodeTrustResult[node] = nodeTrustType;
                    }
                }
                //添加到自己对机构的信任缓存中，成为直接信任
                //对于Normal，信任为0.9
                //对于Faulty，信任为0.6
                //对于Malicious，信任为0.3
                Dictionary<IOTNodeType, double> trustMap = new Dictionary<IOTNodeType, double>()
                {
                    {IOTNodeType.NORMAL,0.9},
                    {IOTNodeType.ENV_FAULTY, 0.6},
                    {IOTNodeType.NODE_FAULTY, 0.6},
                    {IOTNodeType.MALICIOUS, 0.6},
                };
                double oldTrustRatio = 0.2f;
                foreach (IOTNodeTrustTypeResult r in combinedNodeTrustResult)
                {
                    double nodeTrust = trustMap[r.type];
                    if (!this.cachedOrgTrustResult.ContainsKey(r.orgId))
                    {
                        OrgDirectTrust dt = new OrgDirectTrust();
                        dt.count = 1;
                        dt.trustValue = nodeTrust;
                        this.cachedOrgTrustResult.Add(r.orgId, dt);
                        continue;
                    }

                    double orgOldTrust = this.cachedOrgTrustResult[r.orgId].trustValue;
                    int count = this.cachedOrgTrustResult[r.orgId].count;
                    double newTrust = (1 - oldTrustRatio) * nodeTrust + oldTrustRatio * orgOldTrust;
                    this.cachedOrgTrustResult[r.orgId].count = count + 1;
                    this.cachedOrgTrustResult[r.orgId].trustValue = newTrust;
                }

                //将最终的数据发送给信任管理机构
                if (combinedNodeTrustResult.Count > 0)
                {
                    byte[] buf = new byte[global.BufSize * hashedNodeTrustResult.Count];
                    MemoryStream ms = new MemoryStream(buf);
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(ms, combinedNodeTrustResult);
                    byte[] tmp = new byte[ms.Position];
                    Array.Copy(buf, tmp, ms.Position);

                    Packet pkg = new Packet(this, global.trustManager, PacketType.NODE_TYPE_REPORT);
                    pkg.TrustReport = new TrustReportField(0, tmp, tmp.Length);
                    SendPacketDirectly(scheduler.currentTime, pkg);
                }
                //clear the cache.
                this.cachedNodeTrustResult.Clear();

            }


            float time = scheduler.currentTime + global.checkNodeTimeout;
            Event.AddEvent(new Event(time, EventType.CHK_RT_TIMEOUT, this, null));
        }

        static double Max(List<double> list)
        {
            double result = list[0];
            foreach (double d in list)
            {
                if (d == 0)
                    continue;
                if (result < d)
                    result = d;
            }
            return result;
        }

        static double Min(List<double> list)
        {
            double result = list[0];
            foreach (double d in list)
            {
                if (d == 0)
                    continue;
                if (result > d)
                    result = d;
            }
            return result;
        }


        IOTNodeTrustTypeResult DeduceNodeFinalTrustType(int node, IOTNodeTrustResult combinedNodeTrust)
        {
            double b, p, confirmedRate;
            int confirmed;
            int org = combinedNodeTrust.orgId;
            combinedNodeTrust.ds.Cal();

            b = combinedNodeTrust.ds.b[IOTNodeTrust.pow(IOTNodeType.MALICIOUS)];
            p = combinedNodeTrust.ds.p[IOTNodeTrust.pow(IOTNodeType.MALICIOUS)];
            confirmed = combinedNodeTrust.confirmed[(int)IOTNodeType.MALICIOUS];
            confirmedRate = Min(new List<double>{
                combinedNodeTrust.confirmedRate[(int)IOTEventType.DropPacketMaliciously],
                combinedNodeTrust.confirmedRate[(int)IOTEventType.ModifyPacketMaliciously],
                combinedNodeTrust.confirmedRate[(int)IOTEventType.MakePacketMaliciously],
            });
            if (b > global.MaliciouslyBelief && p > global.MaliciouslyPlausibility
                && confirmed >= global.ConfirmedThrehold && confirmedRate >= global.CofirmedRateThrehold)
                return new IOTNodeTrustTypeResult(node, org, IOTNodeType.MALICIOUS);

            b = combinedNodeTrust.ds.b[IOTNodeTrust.pow(IOTNodeType.NODE_FAULTY)];
            p = combinedNodeTrust.ds.p[IOTNodeTrust.pow(IOTNodeType.NODE_FAULTY)];
            confirmed = combinedNodeTrust.confirmed[(int)IOTNodeType.NODE_FAULTY];
            confirmedRate = Min(new List<double>{
                combinedNodeTrust.confirmedRate[(int)IOTEventType.ModifyPacketDueToNodeFaulty],
                combinedNodeTrust.confirmedRate[(int)IOTEventType.NotMakeCommandButMove]
            });
            if (b > global.MaliciouslyBelief && p > global.MaliciouslyPlausibility
                && confirmed > global.ConfirmedThrehold && confirmedRate > global.CofirmedRateThrehold)
                return new IOTNodeTrustTypeResult(node, org, IOTNodeType.NODE_FAULTY);

            b = combinedNodeTrust.ds.b[IOTNodeTrust.pow(IOTNodeType.ENV_FAULTY)];
            p = combinedNodeTrust.ds.p[IOTNodeTrust.pow(IOTNodeType.ENV_FAULTY)];
            confirmed = combinedNodeTrust.confirmed[(int)IOTNodeType.ENV_FAULTY];
            confirmedRate = Min(new List<double>{
                combinedNodeTrust.confirmedRate[(int)IOTEventType.DropPacketDueToBandwith],
                combinedNodeTrust.confirmedRate[(int)IOTEventType.NotModifyPacketButNetworkFaulty],
                combinedNodeTrust.confirmedRate[(int)IOTEventType.NotMakeCommandButNetworkDelay]
            });
            if (b > global.MaliciouslyBelief && p > global.MaliciouslyPlausibility
                && confirmed > global.ConfirmedThrehold && confirmedRate > global.CofirmedRateThrehold)
                return new IOTNodeTrustTypeResult(node, org, IOTNodeType.ENV_FAULTY);

            b = combinedNodeTrust.ds.b[IOTNodeTrust.pow(IOTNodeType.NORMAL)];
            p = combinedNodeTrust.ds.p[IOTNodeTrust.pow(IOTNodeType.NORMAL)];
            confirmed = combinedNodeTrust.confirmed[(int)IOTNodeType.NORMAL];
            confirmedRate = Min(new List<double>{
                combinedNodeTrust.confirmedRate[(int)IOTEventType.NotDropPacket],
                combinedNodeTrust.confirmedRate[(int)IOTEventType.NotModifyPacket],
                combinedNodeTrust.confirmedRate[(int)IOTEventType.NotMakePacket]
            });

            if (b > global.MaliciouslyBelief && p > global.MaliciouslyPlausibility
                && confirmed > global.ConfirmedThrehold && confirmedRate > global.CofirmedRateThrehold)
                return new IOTNodeTrustTypeResult(node, org, IOTNodeType.NORMAL);
            return new IOTNodeTrustTypeResult(node, org, IOTNodeType.NORMAL);
        }

        //将一个节点的所有报告进行正交计算，推导出该节点的最终状态
        public IOTNodeTrustResult DeduceNodeTrust(int node, List<IOTNodeTrustResult> nodeTrusts)
        {
            int org = nodeTrusts[0].orgId;
            IOTNodeTrustResult combinedNodeTrust = new IOTNodeTrustResult(node, org);
            for (int i = 0; i < nodeTrusts.Count; i++)
            {
                IOTNodeTrustResult nodeTrust = nodeTrusts[i];
                if (i == 0)
                    combinedNodeTrust.ds = nodeTrust.ds;
                else
                    combinedNodeTrust.ds = 
                        DSClass.Combine(combinedNodeTrust.ds, nodeTrust.ds);//进行正交运算
                for (int j = 0; j < combinedNodeTrust.confirmed.Length; j++)
                    combinedNodeTrust.confirmed[j] = Math.Max(combinedNodeTrust.confirmed[j], nodeTrust.confirmed[j]);

                for (int j = 0; j < combinedNodeTrust.confirmedRate.Length; j++)
                    combinedNodeTrust.confirmedRate[j] = Math.Max(combinedNodeTrust.confirmedRate[j], nodeTrust.confirmedRate[j]);
            }
            return combinedNodeTrust;
        }

        public override void SendPacketDirectly(float time, Packet pkg)
        {
            pkg.Prev = Id;
            Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()));

            float recv_time = global.serverProcessDelay + global.internetDelay;
            if (pkg.Next == -1) //Broadcast
                return;//No such a case.
            else
            {
                Node node = null;
                switch (pkg.NextType)
                {
                    case NodeType.READER:
                        node = global.readers[pkg.Next];
                        break;
                    case NodeType.QUERIER:
                        node = global.queriers[pkg.Next];
                        break;
                    case NodeType.OBJECT:
                        node = global.objects[pkg.Next];
                        break;
                    case NodeType.TRUST_MANAGER:
                        node = global.trustManager;
                        break;
                    default:
                        Debug.Assert(false, "Error Next Type!");
                        break;
                }
                pkg.PrevType = type;
                pkg.Prev = Id;
                pkg.PacketSeq = this.sentPacketCount++;
                Event.AddEvent(
                    new Event(time + recv_time, EventType.RECV,
                        node, pkg));
            }
        }

    }
}
