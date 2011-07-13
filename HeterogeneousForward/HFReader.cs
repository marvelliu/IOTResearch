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
    public class TagEntity
    {
        public int allowedTagNum;
        public uint allowTags;
        public TagEntity(int allowedTagNum, uint allowTags)
        {
            this.allowedTagNum = allowedTagNum;
            this.allowTags = allowTags;
        }
    }

    [Serializable]
    public class SWHubCandidate : IComparable, ICloneable
    {
        public int nodeId;
        public TagEntity tagEntity;
        public int hops;

        public int CompareTo(object obj)
        {
            SWHubCandidate b = (SWHubCandidate)obj;
            return b.tagEntity.allowedTagNum - this.tagEntity.allowedTagNum;
        }
        public SWHubCandidate(int nodeId, TagEntity tagEntity, int hops)
        {
            this.nodeId = nodeId;
            this.tagEntity = tagEntity;
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

    class PingedSWHub
    {
        public int hops;
        public uint tags;
        public double time;

        public PingedSWHub(int hops, uint tags, double time)
        {
            this.hops = hops;
            this.tags = tags;
            this.time = time;
        }
    }

    public class HFReader : Reader
    {
        public List<ForwardStrategy> forwardStrategies;
        public List<SWHubCandidate> swHubCandidates;
        TagEntity availTagEntity = null;
        public bool isSwHub = false;
        Dictionary<int, Dictionary<int, PingedSWHub>> cachedPingedSWHubs = null;
        public HashSet<string> receivedSWPackets;
        private HFGlobal global;

        new public static HFReader ProduceReader(int id, int org)
        {
            return new HFReader(id, org);
        }

        public HFReader(int id, int org)
            : base(id, org)
        {
            this.global = (HFGlobal)Global.getInstance();
            this.forwardStrategies = new List<ForwardStrategy>();
            this.swHubCandidates = new List<SWHubCandidate>();
            this.cachedPingedSWHubs = new Dictionary<int, Dictionary<int, PingedSWHub>>();
            this.receivedSWPackets = new HashSet<string>();
            //Event.AddEvent(new Event( global.startTime + global.checkSWHubCandidateInterval, EventType.CHK_SW_NB, this, null));
        }

        /*
        public static void AddDefaultForwardStrategy()
        {
            Global global = Global.getInstance();
            for (int i = 0; i < global.readerNum; i++)
            {
                ((HFReader)global.readers[i]).forwardStrategies.Add(new ForwardStrategy());
            }
        }*/

        public override void RecvAODVRequest(Packet pkg)
        {
            if (CheckTags(pkg) == false)
                return;
            base.RecvAODVRequest(pkg);
        }

        public override bool RoutePacket(Packet pkg)
        {
            int dst = pkg.Dst;

            string pkgId = pkg.getId();
            if (global.debug)
                Console.WriteLine("debug RoutePacket pkgId:{0}", pkgId);
            if (!this.receivedPackets.Contains(pkgId))
                this.receivedPackets.Add(pkgId);
            else
                return true;

            if (pkg.PrevType != NodeType.READER)
                Debug.Assert(true, "Error, not reader!");
            else if (pkg.Prev != Id && !Neighbors.ContainsKey(pkg.Prev))//not itself
                return false;

            // to itself
            if (pkg.Dst == Id && pkg.DstType == NodeType.READER)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3}->{4}{5}, total: {6}", scheduler.currentTime, pkg.Type, pkg.SrcType, pkg.Src, this.type, this.Id, scheduler.currentTime - pkg.beginSentTime);
                //
                //SendPacketDirectly(scheduler.currentTime, pkg);
                return true;
            }

            if(this.Id!=pkg.Src &&CheckTags(pkg) == false)
                return false;

            //have to flood...
            SendAODVData(pkg);
            return true;
        }

        SWHubCandidate GetSWHubCandidate(int id)
        {
            foreach (SWHubCandidate c in this.swHubCandidates)
            {
                if (c.nodeId == id)
                    return c;
            }
            return null;
        }

        public void SendSWData(Packet pkg)
        {
            int dst = pkg.Dst;
            Reader node = global.readers[pkg.Prev];
            if (pkg.Type != PacketType.DATA && pkg.Type != PacketType.SW_REQUEST)
                throw new Exception("not DATA or type!");
            //Check Route Table
            if (this.Neighbors.ContainsKey(dst))
            {
                SWHubCandidate c = GetSWHubCandidate(dst);
                if (c == null)
                    throw new Exception("Neighor not exist in HubCandidates");
                if (IsAllowedTags(c.tagEntity.allowTags, pkg.Tags))
                {
                    this.retryOnSendingFailture = true;
                    pkg.Next = c.nodeId;
                    SendPacketDirectly(scheduler.currentTime, pkg);
                    this.retryOnSendingFailture = false;
                }
                else
                    Console.WriteLine("Error: reader{0} not accept packet!", dst);
                return;
            }

            //是否为某个swHub
            int nextId = FindNextNodeInSWHubRouteTable(dst, pkg.Tags);
            if (nextId>0) 
            {
                PingedSWHub entity = this.cachedPingedSWHubs[dst][nextId];
                //Console.WriteLine("{0}-{1}", entity.hops, entity.time);
                pkg.Prev = this.Id;
                pkg.Next = nextId;
                pkg.PrevType = pkg.NextType = NodeType.READER;
                pkg.TTL = Math.Max(entity.hops + 1, pkg.TTL);
                this.retryOnSendingFailture = true;
                SendPacketDirectly(scheduler.currentTime, pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            //如果没找到，则发往所有的swHub
            if (global.routeMethod == RouteMethod.SmallWorld && 
                pkg.Type != PacketType.PING_REQUEST && pkg.Type!= PacketType.PING_RESPONSE && pkg.Type!= PacketType.SW_REQUEST
                && this.swHubCandidates.Count >0)
            {
                //如果是swHub，转发到所有hub，否则只转发到最近的hub
                if (this.isSwHub == true)
                {
                    /*
                    int n = Math.Min((global).choosenNearCandidateNum, this.swHubCandidates.Count);
                    for (int i = 0; i < n; i++)
                    {
                        SendSmallWorldRequest(this.swHubCandidates[i], pkg);
                    }*/
                    foreach(int swHub in this.cachedPingedSWHubs.Keys)
                    {                     
                        Dictionary<int, PingedSWHub> p = this.cachedPingedSWHubs[swHub];
                        int nextId1 = GetNextNodeOfLestHopsFromSwHub(p, pkg.Tags);
                        if (nextId1 > 0)
                            SendSmallWorldRequest(swHub, nextId1, pkg);
                        else
                            Console.WriteLine("Next node not found for hub Reader{0}", swHub);
                    }
                }
                else
                {
                    int nearestSWHub = GetNearestSWHub(pkg.Tags);
                    if (nearestSWHub > 0)
                    {
                        Dictionary<int, PingedSWHub> p = this.cachedPingedSWHubs[nearestSWHub];
                        int nextId1 = GetNextNodeOfLestHopsFromSwHub(p, pkg.Tags);
                        if (nextId1 > 0)
                            SendSmallWorldRequest(nearestSWHub, nextId1, pkg);
                        else
                            Console.WriteLine("Next node not found for hub Reader{0}", nearestSWHub);
                    }
                    else
                        Console.WriteLine("nearestSWHub is empty, abort... ");

                }
            }

            //同时也发送aodv请求
            Console.WriteLine("{0:F4} [{1}] {2}{3} tries to send to {4}{5} but no route", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.DstType, pkg.Dst);
            pkg.TTL = global.innerSWTTL;
            SendAODVRequest(Node.BroadcastNode, this.Id, dst, pkg.TTL - 1);
            AddPendingAODVData(pkg);
        }

        int GetLestHopsFromSwHub(Dictionary<int, PingedSWHub> p, uint tags)
        {
            int hx = 999;
            foreach (PingedSWHub h in p.Values)
            {
                if (h.hops < hx && IsAllowedTags(h.tags, tags))
                {
                    hx = h.hops;
                }
            }
            return hx;
        }

        int GetNextNodeOfLestHopsFromSwHub(Dictionary<int, PingedSWHub> p, uint tags)
        {
            int hx = 999;
            int next = -1;
            foreach (int n in p.Keys)
            {
                PingedSWHub h = p[n];
                if (h.hops < hx && IsAllowedTags(h.tags, tags))
                {
                    hx = h.hops;
                    next = n;
                }
            }
            return next;
        }

        int GetNearestSWHub(uint tags)
        {
            int mh = 999;
            int ns = 0;

            if (this.cachedPingedSWHubs.Count == 0)
                return -1;

            foreach (int swHub in this.cachedPingedSWHubs.Keys)
            {
                Dictionary<int, PingedSWHub> p = this.cachedPingedSWHubs[swHub];

                int hx = GetLestHopsFromSwHub(p, tags);
                if (hx < mh)
                {
                    mh = hx;
                    ns = swHub;
                }
            }
            return ns;
        }

        bool IsAllowedTags(uint selfTags, uint tags)
        {
            return (selfTags | tags) == selfTags;
        }

        public int FindNextNodeInSWHubRouteTable(int dst, uint tags)
        {
            if(!this.cachedPingedSWHubs.ContainsKey(dst))
                return -2;
            foreach(int nextId in this.cachedPingedSWHubs[dst].Keys)
            {
                PingedSWHub p = this.cachedPingedSWHubs[dst][nextId];
                if(scheduler.currentTime - p.time <= 10 && IsAllowedTags(p.tags, tags))
                    return nextId;
            }
            return -2;
        }


        /*
        public override void SendPingRequest(int nodeId)
        {
            this.retryOnSendingFailture = true;
            Reader node = global.readers[nodeId];
            Packet pkg = new Packet(this, node, PacketType.PING_REQUEST);
            pkg.Data = 0;//hops
            pkg.TTL = global.maxSwHubHops;
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
        }*/


        public override bool SendData(Packet pkg)
        {

            if (global.debug && pkg.Type == PacketType.DATA)
                Console.WriteLine("debug: SendData pkgId:{0}, type:{1}", pkg.getId(), pkg.Type);

            if(this.Id == pkg.Src)
                SendSWData(pkg);
            else
                RoutePacket(pkg);
            return true;
        }


        //向所有节点发送ping，中间节点记录
        //nodeId应为广播
        public override void SendPingRequest(int nodeId)
        {
            Node node = Node.getNode(nodeId, NodeType.READER) ;
            Packet pkg = new Packet(this, node, PacketType.PING_REQUEST);
            if (this.availTagEntity == null)
                this.availTagEntity = CaculateTagEntity(this.forwardStrategies);
            pkg.Data = this.availTagEntity.allowTags;//hops
            pkg.TTL = global.outerSWTTL;
            SendPacketDirectly(scheduler.currentTime, pkg);
        }

        public override void RecvPingRequest(Packet pkg)
        {
            Reader node = global.readers[pkg.Prev];

            if (this.Id == pkg.Src)
                return;
            if (!this.cachedPingedSWHubs.ContainsKey(pkg.Src))
            {
                this.cachedPingedSWHubs.Add(pkg.Src, new Dictionary<int,PingedSWHub>());
            }
            Dictionary<int, PingedSWHub> d = this.cachedPingedSWHubs[pkg.Src];
            if (d.ContainsKey(pkg.Prev))
            {

                PingedSWHub p = d[pkg.Prev];
                //很久之前的，或可用tag更大，则更新
                if (scheduler.currentTime - p.time >= 5 || CaculateTagNum(p.tags) < CaculateTagNum((uint)pkg.Data))
                {
                    p.time = scheduler.currentTime;
                    p.hops = global.TTL - pkg.TTL;
                    p.tags = (uint)pkg.Data;
                }
                else //无需处理，返回
                    return;
            }
            else
            {
                d.Add(pkg.Prev, new PingedSWHub(global.TTL - pkg.TTL, (uint)pkg.Data, scheduler.currentTime));
            }
           
            //继续转发
            Packet pkg1 = (Packet)pkg.Clone();
            pkg1.Prev = this.Id;
            //Console.WriteLine("debug pkg ttl: {0}", pkg1.TTL);
            SendPacketDirectly(scheduler.currentTime, pkg);
        }


        public void SendSmallWorldRequest(int dstId, int nextId, Packet pkg)
        {
            //这里借用AODVRequest字段存放数据包的目的地
            Packet pkg1 = (Packet)pkg.Clone();
            pkg1.SWRequest = new SWRequestField(pkg.Src, pkg.Dst, pkg.PacketSeq, pkg.Type);
            pkg1.Dst = dstId;
            pkg1.Next = nextId;
            pkg1.Src = this.Id;
            pkg1.Type = PacketType.SW_REQUEST;
            this.retryOnSendingFailture = true;
            SendData(pkg1);
            this.retryOnSendingFailture = false;
        }

        public void RecvSmallWorldRequest(Packet pkg)
        {
            string origPkgId = NodeType.READER.ToString() + pkg.SWRequest.origSrc + "-" + NodeType.READER.ToString() + pkg.SWRequest.origDst + ":" + pkg.SWRequest.origSenderSeq;
            if (global.debug)
                Console.WriteLine("debug: RecvSmallWorldRequest orig packet Id:{0}", origPkgId);
            

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
            pkg.PacketSeq = pkg.SWRequest.origSenderSeq;
            pkg.TTL = global.innerSWTTL;

            string packetId = pkg.getId();
            if (this.receivedSWPackets.Contains(packetId))
                return;
            else
                this.receivedSWPackets.Add(packetId);

            // to itself
            if (pkg.Dst == Id && pkg.DstType == NodeType.READER)
            {
                string pkgId = pkg.getId();
                if (!this.receivedPackets.Contains(pkgId))
                {
                    this.receivedPackets.Add(pkgId);
                    Console.WriteLine("{0:F4} [{1}] {2}{3}->{4}{5}, total: {6}", scheduler.currentTime, pkg.Type, pkg.SrcType, pkg.Src, this.type, this.Id, scheduler.currentTime - pkg.beginSentTime);
                }
                return;
            }

            //向swHub及自己区域内发送
            SendSWData(pkg);

            //如果想继续通过小世界转发的话，则执行recv
            //Recv(pkg);
        }

        public List<SWHubCandidate> GetNearMaxCandidates()
        {
            List<SWHubCandidate> result = new List<SWHubCandidate>();
            this.swHubCandidates.Sort(); //逆序排序

            while (this.swHubCandidates.Count > global.maxStoredNearCandidateNum)
            {
                this.swHubCandidates.RemoveAt(this.swHubCandidates.Count-1);
            }
            int max = Math.Min(global.maxNearCandidateNum, this.swHubCandidates.Count);
            for (int i = 0; i < max; i++)
                result.Add(this.swHubCandidates[i]);
            return result;
        }

        public TagEntity CaculateTagEntity(List<ForwardStrategy> fs)
        {
            uint allowTags = 0;

            unchecked
            {
                if (global.defaultForwardStrategyAction == ForwardStrategyAction.ACCEPT)
                    allowTags = (uint)-1;
                else
                    allowTags = 0;
            }

            List<ForwardStrategy> accecpted = new List<ForwardStrategy>();
            List<ForwardStrategy> refused = new List<ForwardStrategy>();
            foreach (ForwardStrategy f in fs)
            {
                if (f.Action == ForwardStrategyAction.ACCEPT)
                    accecpted.Add(f);
                else
                    refused.Add(f);
            }
            foreach(ForwardStrategy f in accecpted)
            {
                allowTags = allowTags | f.Tags;
            }
            foreach(ForwardStrategy f in refused)
            {
                allowTags = allowTags & ~f.Tags;
            }
                
            uint mask = (uint)(1 << global.tagNameNum+1)-1;
            allowTags = allowTags & mask; //0(32-tagnum)1(tagnum)

            /*
            for(int i=0;i< global.tagNameNum;i++)
            {
                allowTags ^= (uint)1 << i;
                foreach (ForwardStrategy f in fs)
                {
                    if (f.Action == ForwardStrategyAction.ACCEPT)
                        continue;
                    if ((f.Tags & (ulong)1 << i) == 0)
                    {
                        allowTags &= (~((uint)1 << i));
                        break;
                    }
                }
            }*/
            int n = CaculateTagNum(allowTags);
            return new TagEntity(n, allowTags);
        }

        public int CaculateTagNum(uint tags)
        {
            int n = 0;
            for (int i = 0; i < global.tagNameNum; i++)
            {
                if ((tags & (ulong)1 << i) != 0)
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
                    c = new SWHubCandidate(node.Id, CaculateTagEntity(fs), 1);
                    this.swHubCandidates.Add(c);
                }

                //然后计算其他节点
                List<SWHubCandidate> candidates = data.MaxSWHubCandidates;
                foreach(SWHubCandidate c0 in candidates)
                {
                    if (c0.nodeId == this.Id)
                        continue;
                    //限制hop数
                    if (c0.hops > global.maxSwHubHops)
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
            //计算自己允许的tag数，计算一次即可
            if (this.availTagEntity == null)
            {
                this.availTagEntity = CaculateTagEntity(this.forwardStrategies);
            }

            //如果自己可允许的tag比其他节点多，且大于一个阈值，则将自己作为hub
            List<SWHubCandidate> temp = new List<SWHubCandidate>();
            foreach (SWHubCandidate c in this.swHubCandidates)
            {
                if (c.tagEntity.allowedTagNum >= global.minSWHubRequiredTags)
                    temp.Add(c);
            }
            //理论上swHubCandidates和temp都是排好序的列表
            int n = Math.Min(temp.Count, global.choosenNearCandidateNum);
            if (global.currentSWHubNumber < global.swHubRatio * global.readerNum && global.currentSWHubNumber < global.maxSwHubs
                && this.Neighbors.Count > global.minSwHubNeighbors
                && n > 0 && this.availTagEntity.allowedTagNum >= this.swHubCandidates[n - 1].tagEntity.allowedTagNum
                && this.availTagEntity.allowedTagNum >= global.minSwHubAvailTagThrethold
                && this.isSwHub == false)//自己可以是swhub
            {
                Console.WriteLine("Reader{0} set itself as a swHub", this.Id);
                this.isSwHub = true;
                global.currentSWHubNumber++;

                /*
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
                }*/
            }
            if(this.isSwHub == true)
                SendPingRequest(Node.BroadcastNode.Id);
            Event.AddEvent(new Event(
                scheduler.currentTime + global.checkSWHubCandidateInterval, EventType.CHK_SW_NB, this, null));
        }



        public override void SendPacketDirectly(float time, Packet pkg)
        {
            float recv_time = 0;
            pkg.Prev = Id;
            global.PacketSeq++;

            if (pkg.Type == PacketType.AODV_REQUEST)
                Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}({6}->{7}->{8})", time, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()), pkg.Src, pkg.Dst, pkg.AODVRequest.dst);
            else if (pkg.Type == PacketType.AODV_REPLY)
                Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}({6}->{7}->{8})", time, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()), pkg.Src, pkg.Dst, ((AODVReply)pkg.Data).dst);
            else
                Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}({6}->{7}->{8})", time, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()), pkg.Src, pkg.Prev, pkg.Dst);

            if (pkg.Next == Node.BroadcastNode.Id) //Broadcast
            {
                List<Reader> list = GetAllNearReaders(global.nodeMaxDist, true);
                if (list.Count == 0)
                    return;
                this.packetCounter++;
                this.packetSeq++;

                for (int i = 0; i < list.Count; i++)
                {
                    Packet pkg1 = pkg.Clone() as Packet;
                    pkg1.SenderSeq = this.packetSeq;
                    pkg1.DelPacketNode = list[0].Id;
                    if (pkg.Src == Id)
                        pkg1.PacketSeq = this.packetSeq;

                    //Console.WriteLine("+packet count: {0}->{1} {2}_{3}", pkg.Prev, pkg.Next, global.readers[pkg.Prev].packetCounter,pkg.PacketSeq);
                    recv_time = global.processDelay + (float)(Utility.Distance(this, list[i]) / global.lightSpeed);
                    Event.AddEvent(
                        new Event(time + recv_time, EventType.RECV, list[i], pkg1));
                }
                this.packetSeq++;
            }
            else
            {

                pkg.PrevType = type;
                pkg.Prev = Id;

                switch (pkg.NextType)
                {
                    case NodeType.READER:
                        //bool fail = false;
                        List<Reader> list = GetAllNearReaders(global.nodeMaxDist, true);
                        Reader nextNode = (Reader)Node.getNode(pkg.Next, NodeType.READER);
                        if (!list.Contains(global.readers[pkg.Next])) //节点未在区域内,删除该节点，0.2秒后重试
                        {
                            Console.WriteLine("{0:F4} [{1}] {2}{3} Drop data of {4}{5} due to sending failture.", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.NextType, pkg.Next);
                            this.Neighbors.Remove(pkg.Next);
                            foreach(Dictionary<int, PingedSWHub> p in this.cachedPingedSWHubs.Values)
                            {
                                if(p.ContainsKey(pkg.Next))
                                    p.Remove(pkg.Next);
                            }
                            //this.routeTable.Remove(pkg.Dst);
                            if (retryOnSendingFailture == true && (pkg.Type != PacketType.BEACON && pkg.Type != PacketType.AODV_REPLY && pkg.Type != PacketType.AODV_REQUEST))
                            {
                                Event.AddEvent(new Event(scheduler.currentTime + 0.2f, EventType.SND_DATA, this, pkg));
                                Console.WriteLine("retry");
                                //fail = true;
                                return;
                            }
                            list.Add(global.readers[pkg.Next]);
                        }
                        int totalPackets = 0;
                        foreach (Reader r in list)
                            totalPackets += r.packetCounter;
                        this.packetCounter++;
                        //Console.WriteLine("+packet count: {0}->{1} {2}_{3}", pkg.Prev, pkg.Next, global.readers[pkg.Prev].packetCounter,pkg.PacketSeq);                        

                        double prop = (totalPackets > 90) ? (0.1) : totalPackets / (-100.0) + 1.0;

                        this.packetSeq++;
                        for (int i = 0; i < list.Count; i++)
                        {
                            while (true)
                            {
                                double ran = Utility.U_Rand(1);
                                recv_time += global.processDelay;
                                if (ran < prop)
                                    break;
                            }
                            Packet pkg1 = pkg.Clone() as Packet;
                            pkg1.SenderSeq = this.packetSeq;
                            if (pkg.Src == Id)
                                pkg1.PacketSeq = this.packetSeq;
                            //Console.WriteLine("[DEBUG] recv reader{0}-{1}", list[i].id, pkg1.PacketSeq);

                            recv_time += (float)(Utility.Distance(this, (MobileNode)list[i]) / global.lightSpeed);

                            Event.AddEvent(
                                new Event(time + recv_time, EventType.RECV,
                                    list[i], pkg1));
                        }
                        break;
                    default:
                        Console.WriteLine("Error Type!");
                        break;
                }
            }
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
