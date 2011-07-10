using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;

namespace HeterogeneousForward
{
    [Serializable]
    public class SWHubCandidate : IComparable, ICloneable
    {
        public int nodeId;
        public int allowedTagNum;
        public int hops;

        public int CompareTo(object obj)
        {
            SWHubCandidate b = (SWHubCandidate)obj;
            return b.allowedTagNum - this.allowedTagNum;
        }
        public SWHubCandidate(int nodeId, int allowedTagNum, int hops)
        {
            this.nodeId = nodeId;
            this.allowedTagNum = allowedTagNum;
            this.hops = hops;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
    [Serializable]
    class HFBeaconData
    {
        public List<ForwardStrategy> ForwardStrategies;
        public List<SWHubCandidate> MaxSWHubCandidates;
        public HFBeaconData(List<ForwardStrategy> f, List<SWHubCandidate> n)
        {
            this.ForwardStrategies = f;
            this.MaxSWHubCandidates = n;
        }
    }

    public class HFReader : Reader
    {
        public List<ForwardStrategy> forwardStrategies;
        public List<SWHubCandidate> swHubCandidates;
        int availTags = -1;
        bool swHub = false;
        SWHubCandidate nearestSWHubCandidate = null;

        new public static HFReader ProduceReader(int id, int org)
        {
            return new HFReader(id, org);
        }

        public HFReader(int id, int org)
            : base(id, org)
        {
            this.forwardStrategies = new List<ForwardStrategy>();
            this.swHubCandidates = new List<SWHubCandidate>();
            Event.AddEvent(new Event(
                global.startTime + ((HFGlobal)Global.getInstance()).checkCandidateInterval, EventType.CHK_SW_NB, this, null));
        }

        public static void AddDefaultForwardStrategy()
        {
            Global global = Global.getInstance();
            for (int i = 0; i < global.readerNum; i++)
            {
                ((HFReader)global.readers[i]).forwardStrategies.Add(new ForwardStrategy());
            }
        }

        public override void RecvAODVRequest(Packet pkg)
        {
            if (CheckTags(pkg) == false)
                return;
            base.RecvAODVRequest(pkg);
        }

        public override bool RoutePacket(Packet pkg)
        {
            int dst = pkg.Dst;

            if (pkg.PrevType != NodeType.READER)
                Debug.Assert(true, "Error, not reader!");
            else if (pkg.Prev != Id && !Neighbors.ContainsKey(pkg.Prev))//not itself
                return false;

            // to itself
            if (pkg.Dst == Id && pkg.DstType == NodeType.READER)
            { 
                string pkgId = pkg.getId();
                if (!this.receivedPackets.Contains(pkgId))
                {
                    this.receivedPackets.Add(pkgId);
                    Console.WriteLine("{0:F4} [{1}] {2}{3}->{4}{5}, total: {6}", scheduler.currentTime, pkg.Type, pkg.SrcType, pkg.Src, this.type, this.Id, scheduler.currentTime - pkg.beginSentTime);
                }
                //
                //SendPacketDirectly(scheduler.currentTime, pkg);
                return true;
            }

            if(this.Id!=pkg.Src &&CheckTags(pkg) == false)
                return false;

            //have to flood...
            SendAODVData(pkg, dst);
            return true;
        }

        public override void SendAODVData(Packet pkg, int dst)
        {
            Reader node = global.readers[pkg.Prev];
            //Check Route Table

            if (ExistInRouteTable(dst))
            {
                RouteEntity entity = (RouteEntity)routeTable[dst];
                //Console.WriteLine("{0}-{1}", entity.hops, entity.time);
                pkg.Prev = Id;
                pkg.Next = entity.next;
                pkg.PrevType = pkg.NextType = NodeType.READER;
                pkg.TTL = Math.Max(entity.hops + 1, pkg.TTL);
                SendPacketDirectly(scheduler.currentTime, pkg);
                return;
            }

            //Not found...
            if (((HFGlobal)global).routeMethod == RouteMethod.SmallWorld && 
                pkg.Type != PacketType.PING_REQUEST && pkg.Type!= PacketType.PING_RESPONSE && pkg.Type!= PacketType.SW_REQUEST
                && this.swHubCandidates.Count >0)
            {
                //如果是swHub，转发到所有hub，否则只转发到最近的hub
                if (this.swHub == true)
                {
                    int n = Math.Min(((HFGlobal)global).choosenNearCandidateNum, this.swHubCandidates.Count);
                    for (int i = 0; i < n; i++)
                    {
                        SendSmallWorldRequest(this.swHubCandidates[i], pkg);
                    }
                }
                else
                {
                    SendSmallWorldRequest(this.nearestSWHubCandidate, pkg);
                }
            }

            //同时也发送aodv请求
            Console.WriteLine("{0:F4} [{1}] {2}{3} tries to send to {4}{5} but no route", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.DstType, pkg.Dst);
            SendAODVRequest(Node.BroadcastNode, this.Id, dst, pkg.TTL - 1);
            AddPendingAODVData(pkg);
        }


        public override void SendPingRequest(int nodeId)
        {
            this.retryOnSendingFailture = true;
            Reader node = global.readers[nodeId];
            Packet pkg = new Packet(this, node, PacketType.PING_REQUEST);
            pkg.Data = 0;//hops
            pkg.TTL = ((HFGlobal)global).maxSwHubHops;
            SendAODVData(pkg);
            this.retryOnSendingFailture = false;
        }


        public override void RecvPingResponse(Packet pkg)
        {
            Reader node = global.readers[pkg.Prev];
            pkg.Data = (int)pkg.Data + 1;
            if (pkg.Dst == this.Id && pkg.DstType == this.type)
            {
                if (!this.routeTable.ContainsKey(pkg.Src))
                    this.routeTable.Add(pkg.Src, new RouteEntity(pkg.Src, pkg.Prev, (int)pkg.Data, scheduler.currentTime, scheduler.currentTime));
                else
                {
                    this.routeTable[pkg.Prev].hops = (int)pkg.Data;
                    this.routeTable[pkg.Prev].next = pkg.Prev;
                    this.routeTable[pkg.Prev].remoteLastUpdatedTime = scheduler.currentTime;
                    this.routeTable[pkg.Prev].localLastUpdatedTime = scheduler.currentTime;
                }
            }
            else
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
            }
        }



        public void SendSmallWorldRequest(SWHubCandidate c, Packet pkg)
        {
            //这里借用AODVRequest字段存放数据包的目的地
            Packet pkg1 = (Packet)pkg.Clone();
            pkg1.SWRequest = new SWRequestField(pkg.Src, pkg.Dst, pkg.Type);
            pkg1.Dst = c.nodeId;
            pkg1.Src = this.Id;
            pkg1.Type = PacketType.SW_REQUEST;
            this.retryOnSendingFailture = true;
            SendData(pkg1);
            this.retryOnSendingFailture = false;
        }

        public void RecvSmallWorldRequest(Packet pkg)
        {
            if (pkg.Dst != this.Id)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }
            pkg.Src = pkg.SWRequest.origSrc;
            pkg.Dst = pkg.SWRequest.origDst;
            pkg.Type = pkg.SWRequest.origType;

            if(pkg.Dst == this.Id)


            RoutePacket(pkg);
            //如果想继续通过小世界转发的话，则执行recv
            //Recv(pkg);
        }

        public List<SWHubCandidate> GetNearMaxCandidates()
        {
            List<SWHubCandidate> result = new List<SWHubCandidate>();
            this.swHubCandidates.Sort(); //逆序排序

            while (this.swHubCandidates.Count > ((HFGlobal)global).maxStoredNearCandidateNum)
            {
                this.swHubCandidates.RemoveAt(this.swHubCandidates.Count-1);
            }
            int max = Math.Min(((HFGlobal)global).maxNearCandidateNum, this.swHubCandidates.Count);
            for (int i = 0; i < max; i++)
                result.Add(this.swHubCandidates[i]);
            return result;
        }

        public int CaculateTagNum(List<ForwardStrategy> fs)
        {
            ulong availTag = 0;
            for(int i=0;i< ((HFGlobal)global).tagNameNum;i++)
            {
                availTag ^= (ulong)1<<i;
                foreach (ForwardStrategy f in fs)
                {
                    if (f.Action == ForwardStrategyAction.ACCEPT)
                        continue;
                    if ((f.Tags & (ulong)1 << i) == 0)
                    {
                        availTag &= (~((ulong)1<<i));
                        break;
                    }
                }
            }
            int n = 0;
            for (int i = 0; i < ((HFGlobal)global).tagNameNum; i++)
            {
                if ((availTag & (ulong)1 << i) != 0)
                    n++;
            }
            return n;
        }

        public override void SendBeacon(float time)
        {
            Packet pkg = new Packet();
            pkg.Type = PacketType.BEACON;
            pkg.Src = pkg.Prev = Id;
            pkg.Dst = pkg.Next = -1;//Broadcast
            pkg.TTL = 1;

            pkg.Beacon = new BeaconField();
            if (this.gatewayEntities.Count != 0)
            {
                pkg.Beacon.gatewayEntities = new GatewayEntity[this.gatewayEntities.Count];
                int i = 0;
                foreach (int g in this.gatewayEntities.Keys)
                {
                    pkg.Beacon.gatewayEntities[i++] = new GatewayEntity(g, this.Id, this.gatewayEntities[g].hops);
                }
            }
            pkg.Data = new HFBeaconData(this.forwardStrategies, GetNearMaxCandidates());
            SendPacketDirectly(time, pkg);

            float nextBeacon = 0;
            if (scheduler.currentTime < global.beaconWarming)
                nextBeacon = (float)(Utility.P_Rand(10 * (global.beaconWarmingInterval + 0.4)) / 10);//0.5是为了设定最小值
            else
                nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            Event.AddEvent(new Event(scheduler.currentTime + nextBeacon, EventType.SND_BCN, this, null));
        }

        public override void RecvBeacon(Packet pkg)
        {
            base.RecvBeacon(pkg);

            if (pkg.Beacon != null)//避免其他情况
            {
                Reader node = global.readers[pkg.Src];
                HFNeighbor nb = null;
                if (Neighbors.ContainsKey(node.Id))
                    nb = (HFNeighbor)Neighbors[node.Id];
                if (nb == null || nb.lastBeacon < 0)
                    return;
                HFBeaconData data = (HFBeaconData)pkg.Data;
                List<ForwardStrategy> fs = data.ForwardStrategies;
                //Console.WriteLine("[BEACON] reader{0} adds reader{1} ClaimedForwardStrategy count: {2}", this.Id, node.Id, fs.Count);
                nb.ClaimedForwardStrategy = new ForwardStrategy[fs.Count];
                fs.CopyTo(nb.ClaimedForwardStrategy);

                //添加进自己的候选节点列表
                //首先计算邻居允许的节点
                SWHubCandidate c = null;
                for (int i = 0; i < this.swHubCandidates.Count;i++ )
                {
                    if (node.Id == this.swHubCandidates[i].nodeId)
                    {
                        c = this.swHubCandidates[i];
                        c.hops = 1;
                        break;
                    }
                }
                if (c == null)
                {
                    c = new SWHubCandidate(node.Id, CaculateTagNum(fs), 1);
                    this.swHubCandidates.Add(c);
                }

                //然后计算其他节点
                List<SWHubCandidate> candidates = data.MaxSWHubCandidates;
                foreach(SWHubCandidate c0 in candidates)
                {
                    if (c0.nodeId == this.Id)
                        continue;
                    //限制hop数
                    if (c0.hops > ((HFGlobal)global).maxSwHubHops)
                        continue;
                    bool found = false;
                    foreach (SWHubCandidate c1 in this.swHubCandidates)
                    {
                        if (c0.nodeId == c1.nodeId)
                        {
                            found = true;
                            if (c0.hops + 1 < c1.hops)
                                c1.hops = c0.hops + 1;
                            break; ;
                        }
                    }
                    if (found == false)
                    {
                        SWHubCandidate c2 = (SWHubCandidate)c0.Clone();
                        c2.hops++;
                        this.swHubCandidates.Add(c2);
                    }
                }
            }
        }

        public override Neighbor AddNeighbor(Reader nb)
        {
            if (!this.Neighbors.ContainsKey(nb.Id))
                this.Neighbors.Add(nb.Id, new HFNeighbor(nb));
            if (!this.routeTable.ContainsKey(nb.Id))
                this.routeTable.Add(nb.Id, new RouteEntity(nb.Id, nb.Id, 1, scheduler.currentTime, scheduler.currentTime));
            return this.Neighbors[nb.Id];
        }


        private bool CheckTags(Packet pkg)
        {
            //ping消息不需要检查
            if (pkg.Type == PacketType.PING_REQUEST || pkg.Type == PacketType.PING_RESPONSE)
                return true;
            uint tags = pkg.Tags;
            foreach (ForwardStrategy f in forwardStrategies)//one found.
            {
                if ((tags & f.Tags) == f.Tags)
                {
                    if (f.Action == ForwardStrategyAction.REFUSE)
                    {
                        Console.WriteLine("{0:F4} [{1}] {2}{3} drop from {4}{5} due to FORWARD_STRATEGIES", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                        return false;
                    }
                    else
                        break;
                }
            }
            return true;
        }

        public void CheckSmallWorldNeighbors(object o)
        {

            if (this.availTags < 0)
                this.availTags = CaculateTagNum(this.forwardStrategies);

            List<SWHubCandidate> temp = new List<SWHubCandidate>();
            foreach (SWHubCandidate c in this.swHubCandidates)
            {
                if (c.allowedTagNum >= ((HFGlobal)global).minSWHubRequiredTags)
                    temp.Add(c);
            }
            //理论上swHubCandidates和temp都是排好序的列表
            int n = Math.Min(temp.Count, ((HFGlobal)global).choosenNearCandidateNum);

            //自己不是swhub
            if (n==0 || this.availTags < this.swHubCandidates[n - 1].allowedTagNum)
            {
                this.swHub = false;
                if(n > 0)
                    this.nearestSWHubCandidate = this.swHubCandidates[n - 1];
                return;
            }

            if (this.swHub == false)
                Console.WriteLine("Reader{0} set itself as a swHub", this.Id);
            this.swHub = true;

            for (int i = 0; i < n; i++)
            {
                SWHubCandidate c = temp[i];
                if (!ExistInRouteTable(c.nodeId))
                    SendPingRequest(c.nodeId);
                else
                {
                    RouteEntity r = this.routeTable[c.nodeId];
                    if (scheduler.currentTime - r.remoteLastUpdatedTime > 10 || scheduler.currentTime - r.localLastUpdatedTime > 5)
                        SendPingRequest(c.nodeId);
                }
            }
            Event.AddEvent(new Event(
                scheduler.currentTime + ((HFGlobal)Global.getInstance()).checkCandidateInterval, EventType.CHK_SW_NB, this, null));
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
                case PacketType.SW_REQUEST:
                    RecvSmallWorldRequest(pkg);
                    break;
                //Some codes are hided in the base class.
                default:
                    base.ProcessPacket(pkg);
                    return;
            }
        }

    }
}
