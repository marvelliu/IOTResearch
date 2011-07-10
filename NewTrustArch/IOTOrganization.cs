using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using TrustArch;

namespace NewTrustArch
{
    public class ObjectDataRecord
    {
        public int seq;
        public int lastReader;
        public int reportReader;

        public ObjectDataRecord(int seq, int lastReader, int reportReader)
        {
            this.seq = seq;
            this.lastReader = lastReader;
            this.reportReader = reportReader;
        }
    }

    public class ObjectDataRecordList
    {
        public int minSeq;
        public int maxSeq;
        public Dictionary<int, ObjectDataRecord> list;

        public ObjectDataRecordList()
        {
            minSeq = 0;
            maxSeq = 0;
            list = new Dictionary<int, ObjectDataRecord>();
        }
    }

    public class EventCount
    {
        public int badCount = 0;
        public int totalCount = 0;
    }

    public class IOTOrganization:Organization
    {
        Dictionary<int, List<int>> ownNodes;
        Dictionary<int, List<int>> trustNodes;
        new IOTGlobal global;

        List<IOTNodeTrustResult> cachedNodeTrustResult;
        Dictionary<int, IOTNodeTrustTypeResult> cacheHistoricaldNodeTrustResult;
        Dictionary<int, OrgDirectTrust> cachedOrgTrustResult;
        Dictionary<int, ObjectDataRecordList> cachedObjectDataRecordList;

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
            this.cachedOrgTrustResult = new Dictionary<int, OrgDirectTrust>();

            this.cachedObjectDataRecordList = new Dictionary<int,ObjectDataRecordList>();
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
            int lastReader = pkg.DataInfo.lastReader;
            int reportReader = pkg.Src;
            int hash = pkg.DataInfo.packetHash;
            int seq = pkg.DataInfo.seq;
            int obj = pkg.Src;

            if(hash!=0)
            {
                Console.WriteLine("Packet hash incorrect, maybe is modified");
                throw new Exception("Packet hash incorrect. not implemented");
            }

            if(!this.cachedObjectDataRecordList.ContainsKey(obj))
            {
                this.cachedObjectDataRecordList.Add(obj, new ObjectDataRecordList());
            }
            ObjectDataRecordList l = this.cachedObjectDataRecordList[obj];
            if (!l.list.ContainsKey(seq))
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} add a data packet of seq {4} of OBJECT{5}", scheduler.currentTime, "ADD_SEQ", this.type, this.Id, seq, pkg.Src);
                l.list.Add(seq, new ObjectDataRecord(seq, lastReader, reportReader));
                if (l.maxSeq < seq)
                    l.maxSeq = seq;
                if (l.minSeq > seq)
                    l.minSeq = seq;
            }
            else
            {
                throw new Exception("seq already exist.");
            }
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

            //send auth key to the reader..
            Packet pkg1 = new Packet(this, global.readers[requestNode], PacketType.AUTHORIZATION);
            pkg1.Next = pkg.Prev;
            pkg1.NextType = pkg.PrevType;
            //TODO auth key should be made.
            pkg1.Authorization = new AuthorizationField(new int[] { pkg.ObjectTagHeader.tagId }, new int[] { pkg.ObjectTagHeader.tagId });
            SendPacketDirectly(scheduler.currentTime, pkg1);

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


        //每隔一段时间检查一下节点的信任值
        public void CheckRoutine()
        {
            Dictionary<int, EventCount> checkedNodes = new Dictionary<int, EventCount>();
            foreach (KeyValuePair<int, ObjectDataRecordList> k in this.cachedObjectDataRecordList)
            {
                int obj = k.Key;
                ObjectDataRecordList l = k.Value;
                for (int seq = l.minSeq + 1; seq <= l.maxSeq; seq++)
                {
                    if (!l.list.ContainsKey(seq))
                        continue;

                    int lastNode = l.list[seq].lastReader;
                    if (lastNode < 0)
                        continue;
                    if (!checkedNodes.ContainsKey(lastNode))
                        checkedNodes.Add(lastNode, new EventCount());

                    if (!l.list.ContainsKey(seq - 1))
                    {
                        //不正常的
                        checkedNodes[lastNode].badCount++;
                    }
                    checkedNodes[lastNode].totalCount++;
                }
            }

            List<IOTNodeTrustTypeResult> nodeTrustResults = new List<IOTNodeTrustTypeResult>();
            foreach (KeyValuePair<int, EventCount> k in checkedNodes)
            {
                int node = k.Key;
                int badCount = k.Value.badCount;
                int totalCount = k.Value.totalCount;
                float badCountRate = ((float)badCount)/ totalCount;
                if (badCount > global.ConfirmedThrehold && badCountRate>global.CofirmedRateThrehold)
                {
                    IOTNodeTrustTypeResult nodeTrustType = new IOTNodeTrustTypeResult(node, global.readers[node].OrgId, IOTNodeType.MALICIOUS);

                    Console.WriteLine("{0}{1} deduces Reader{2} not work well", type, Id, nodeTrustType.nodeId);
                    nodeTrustResults.Add(nodeTrustType);
                    if (!this.cacheHistoricaldNodeTrustResult.ContainsKey(node))
                        this.cacheHistoricaldNodeTrustResult.Add(node, nodeTrustType);
                    else
                        this.cacheHistoricaldNodeTrustResult[node] = nodeTrustType;
                }
            }
            //将最终的数据发送给信任管理机构
            if (nodeTrustResults.Count > 0)
            {
                byte[] buf = new byte[((IOTGlobal)global).BufSize * nodeTrustResults.Count];
                MemoryStream ms = new MemoryStream(buf);
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, nodeTrustResults);
                byte[] tmp = new byte[ms.Position];
                Array.Copy(buf, tmp, ms.Position);

                Packet pkg = new Packet(this, global.trustManager, PacketType.NODE_TYPE_REPORT);
                pkg.TrustReport = new TrustReportField(0, tmp, tmp.Length);
                SendPacketDirectly(scheduler.currentTime, pkg);
            }


            float time = scheduler.currentTime + global.checkNodeTimeout;
            Event.AddEvent(new Event(time, EventType.CHK_RT_TIMEOUT, this, null));
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
