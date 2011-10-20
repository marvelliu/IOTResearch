using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;

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
            if (this.observedPhenomemons.Count > 0)
            {
                List<MODEventTrustResult> list = MODEventTrust.DeduceAllEventTrusts(this.Id, this.observedPhenomemons, scheduler.currentTime);

                Dictionary<int, List<MODEventTrustResult>> l = new Dictionary<int, List<MODEventTrustResult>>();
                for (int i = 0; i < list.Count; i++)
                {
                    MODEventTrustResult r = list[i];
                    if (!l.ContainsKey(r.app))
                        l.Add(r.app, new List<MODEventTrustResult>());
                    l[r.app].Add(r);
                }
                foreach (KeyValuePair<int, List<MODEventTrustResult>> entity in l)
                {
                    int org = entity.Key;//机构和应用是一一对应的，所以不作区分
                    List<MODEventTrustResult> results = entity.Value;

                    foreach (MODEventTrustResult e in results)
                    {
                        Console.WriteLine("reader{0} report a event of {1} of reader{2}", Id, e.eventIdent, e.node);
                    }
                    /*
                    if (this.orgMonitorMapping.ContainsKey(org)
                        && this.orgMonitorMapping[org].Count > 0)
                    {
                        byte[] buf = new byte[global.BufSize * results.Count];
                        MemoryStream ms = new MemoryStream(buf);
                        BinaryFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(ms, results);
                        byte[] tmp = new byte[ms.Position];
                        Array.Copy(buf, tmp, ms.Position);
                        if (global.debug)
                            Console.WriteLine("READER{0} sends {1} reports.", Id, results.Count);

                        //发往每一个monitor节点
                        for (int j = 0; j < this.orgMonitorMapping[org].Count; j++)
                        {
                            int dst = this.orgMonitorMapping[org][j];
                            Packet pkg = new Packet(this, global.readers[dst], PacketType.EVENT_REPORT);
                            //pkg.TrustReport = new TrustReportField(org, data, bw.BaseStream.Position);
                            pkg.TrustReport = new TrustReportField(org, tmp, tmp.Length);
                            RoutePacket(pkg);
                            //SendPacketDirectly(scheduler.CurrentTime, pkg);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unable to find a monitor for org {0}, abort.", org);
                    }
                     * */
                //TODO 异常
                }
            }

            float time = scheduler.currentTime + global.checkPhenomemonTimeout;
            Event.AddEvent(new Event(time, EventType.CHK_RT_TIMEOUT, this, null));
            //Console.WriteLine("Reader{0} check routing done.", id);
        }



        //将接收到的数据包添加到观察到的现象中
        public void AddReceivePacketPhenomemon(Packet pkg)
        {
            MODPhenomemon p;
            this.totalReceivedPackets++;
            //忽略广播包(从实际来看，发送广播包的一般是节点本身的行为，不需要考虑其对数据包的恶意操作)
            if (pkg.Next == Node.BroadcastNode.Id)
                return;

            //记录发送现象
            if (pkg.Next != Node.BroadcastNode.Id)
            {
                p = new MODPhenomemon(MODPhenomemonType.SEND_PACKET, pkg.Prev, scheduler.currentTime, pkg);
                p.likehood = global.sendLikehood;
                this.observedPhenomemons.Add(p);
                if (global.debug)
                    Console.WriteLine("[Debug] reader{0} add a RECV phenomemon of reader{1}", Id, pkg.Next);
            }

            //数据包到达目的地，忽略

            //记录接收现象
            if (pkg.Next != pkg.Dst)
            {
                p = new MODPhenomemon(MODPhenomemonType.RECV_PACKET, pkg.Next, scheduler.currentTime, pkg);
                p.likehood = global.recvLikehood;
                this.observedPhenomemons.Add(p);
                if (global.debug)
                    Console.WriteLine("[Debug] reader{0} add a SEND phenomemon of reader{1}", Id, pkg.Prev);
            }
        }

        public override void Recv(Packet pkg)
        {
            pkg.seqInited = false;
            //只有reader才需要检查，但是里面函数处理了
            CheckPacketCount(pkg);

            //Check the Phenomemon
            if (pkg.PrevType == NodeType.READER)
                AddReceivePacketPhenomemon(pkg);


            if ((pkg.Next != Id && pkg.Next != Node.BroadcastNode.Id) || pkg.NextType != NodeType.READER)
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
                //Some codes are hided in the base class.
                default:
                    base.ProcessPacket(pkg);
                    return;
            }
        }

    }
}
