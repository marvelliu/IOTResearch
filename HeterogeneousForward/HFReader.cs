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
        public Dictionary<int, PingedSWHub> PingedSwHubs;
        public HFBeaconData(List<ForwardStrategy> f, List<SWHubCandidate> n, Dictionary<int, PingedSWHub> p)
        {
            this.ForwardStrategies = f;
            this.MaxSWHubCandidates = n;
            this.PingedSwHubs = p;
        }
    }

    [Serializable]
    public class PingedSWHub
    {
        public int hops;
        public uint tags;
        public double localUpdateTime;
        public double remoteUpdateTime;

        public PingedSWHub(int hops, uint tags, double localUpdateTime, double remoteUpdateTime)
        {
            this.hops = hops;
            this.tags = tags;
            this.localUpdateTime = localUpdateTime;
            this.remoteUpdateTime = remoteUpdateTime;
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
        new public Dictionary<string, RouteEntity> routeTable;
        new public Dictionary<string, Dictionary<int, PendingAODVRequestCacheEntry>> pendingAODVRequests;
        new public Dictionary<string, List<PacketCacheEntry>> pendingAODVData;

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
            this.routeTable = new Dictionary<string, RouteEntity>();
            this.pendingAODVRequests = new Dictionary<string, Dictionary<int, PendingAODVRequestCacheEntry>>();
            this.pendingAODVData = new Dictionary<string, List<PacketCacheEntry>>();
            //Event.AddEvent(new Event( global.startTime + global.checkSWHubCandidateInterval, EventType.CHK_SW_NB, this, null));
        }


        public RouteEntity GetRouteEntityFromRouteTable(int dst, uint tags)
        {
            string k = dst + "-" + tags;
            string key = "";
            if (routeTable.ContainsKey(k))
                key = k;
            else if (routeTable.ContainsKey(dst.ToString()))
                key = dst.ToString();
            if (routeTable.ContainsKey(key) && (
                        scheduler.currentTime - routeTable[key].localLastUpdatedTime < Math.Min(0.5, global.beaconInterval)
                        || IsFreshRecord(routeTable[key].hops, routeTable[key].remoteLastUpdatedTime)
                        ))
                return routeTable[key];
            else
                return null;
        }

        public void SendAODVRequest(Node node, int src, int dst, int hops, uint tags)
        {
            Packet pkg = new Packet(this, node, PacketType.AODV_REQUEST);
            pkg.AODVRequest = new AODVRequestField(src, dst, hops);
            pkg.TTL = 1;
            pkg.Data = dst;
            pkg.Tags = tags;
            SendPacketDirectly(scheduler.currentTime, pkg);
        }


        public override void RecvAODVRequest(Packet pkg)
        {
            if (CheckTags(pkg) == false)
            {
                Console.WriteLine("reader{0} rejects tag{1} from reader{2}", this.Id, pkg.Tags, pkg.Prev);
                return;
            }
            //base.RecvAODVRequest(pkg);
            Reader node = global.readers[pkg.Prev];

            int src = pkg.AODVRequest.src;
            int dst = pkg.AODVRequest.dst;
            int hops = pkg.AODVRequest.hops;
            uint tags = pkg.Tags;

            //Console.WriteLine("ttl:{0}, hops:{1}", pkg.TTL, hops);
            if (!Neighbors.ContainsKey(node.Id))
                return;

            string key = dst + "-" + tags;
            if (pendingAODVRequests.ContainsKey(key)
                && pendingAODVRequests[key].ContainsKey(src)
                && scheduler.currentTime - pendingAODVRequests[key][src].firstTime < 1.5)
            {
                return;
            }

            if (src == this.Id)
                return;

            if (this.Id == dst)
            {
                SendAODVReply(dst, node, 0, scheduler.currentTime, tags);
                return;
            }

            //在快速运动的环境下需要加入超时机制
            RouteEntity entity = GetRouteEntityFromRouteTable(dst, tags);
            if (entity!= null)
            {
                if (entity.next != node.Id)//避免陷入死循环
                {
                    SendAODVReply(dst, node, entity.hops, entity.remoteLastUpdatedTime, tags);
                    return;
                }
            }
            //Not found...
            if (hops > 0)
            {
                //Console.WriteLine("hops:{0}", hops);
                SendAODVRequest(Node.BroadcastNode, src, dst, hops - 1, tags);
                AddPendingAODVRequest(src, node.Id, dst, true, tags);
            }
        }


        public void SendAODVReply(int dst, Reader node, int hops, double lastTime, uint tags)
        {
            Packet pkg = new Packet(this, node, PacketType.AODV_REPLY);
            pkg.TTL = 1;
            pkg.Data = new AODVReply(dst, hops, lastTime);
            pkg.Tags = tags;
            SendPacketDirectly(scheduler.currentTime, pkg);
        }



        public override void RecvAODVReply(Packet pkg)
        {
            Reader node = global.readers[pkg.Prev];

            if (!Neighbors.ContainsKey(node.Id))
                return;

            AODVReply reply = (AODVReply)pkg.Data;
            string key = reply.dst + "-" + pkg.Tags;
            if (!routeTable.ContainsKey(key))
            {
                routeTable.Add(key, new RouteEntity(reply.dst, node.Id, reply.hops + 1, reply.lastTime, scheduler.currentTime));
                //Console.WriteLine("{0}--{1}", routeTable[reply.dst].dst, routeTable[reply.dst]);
            }
            else
            {
                RouteEntity entity = (RouteEntity)routeTable[key];
                if (reply.hops < entity.hops || reply.lastTime - entity.remoteLastUpdatedTime > 1)
                {
                    entity.hops = reply.hops + 1;
                    entity.next = node.Id;
                    entity.remoteLastUpdatedTime = reply.lastTime;
                    entity.localLastUpdatedTime = scheduler.currentTime;
                }
            }
            if (pendingAODVRequests.ContainsKey(key))
            {
                foreach (int src in pendingAODVRequests[key].Keys)
                {
                    HashSet<int> prevs = (HashSet<int>)pendingAODVRequests[key][src].prevs;
                    foreach (int prev in prevs)
                    {
                        SendAODVReply(reply.dst, global.readers[prev], reply.hops + 1, reply.lastTime, pkg.Tags);
                    }
                    prevs.Clear();
                }
                pendingAODVRequests.Remove(key);
            }
            //Send pending datas...
            if (pendingAODVData.ContainsKey(key))
            {
                List<PacketCacheEntry> entries = (List<PacketCacheEntry>)pendingAODVData[key];
                foreach (PacketCacheEntry entry in entries)
                {
                    Packet pkg1 = entry.pkg;
                    if (routeTable.ContainsKey(key))
                        pkg1.TTL = Math.Max(pkg1.TTL, routeTable[key].hops + 1);
                    SendAODVData(pkg1);
                }
                pendingAODVData.Remove(key);
            }
        }

        public override void SendAODVData(Packet pkg, int dst)
        {
            Reader node = global.readers[pkg.Prev];
            //Check Route Table

            RouteEntity entity = GetRouteEntityFromRouteTable(dst, pkg.Tags);
            if (entity != null)
            {
                //Console.WriteLine("{0}-{1}", entity.hops, entity.time);
                pkg.Prev = Id;
                pkg.Next = entity.next;
                pkg.PrevType = pkg.NextType = NodeType.READER;
                pkg.TTL = Math.Max(entity.hops + 1, pkg.TTL);
                SendPacketDirectly(scheduler.currentTime, pkg);
                return;
            }
            //Not found...

            Console.WriteLine("{0:F4} [{1}] {2}{3} tries to send {4}{5} but no route", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.DstType, pkg.Dst);
            SendAODVRequest(Node.BroadcastNode, this.Id, dst, pkg.TTL - 1);
            AddPendingAODVData(pkg);
        }

        protected void AddPendingAODVRequest(int src, int prev, int dst, bool updateFirstTime, uint tags)
        {
            string key = dst + "-" + tags;
            if (!this.pendingAODVRequests.ContainsKey(key))
            {
                this.pendingAODVRequests.Add(key, new Dictionary<int, PendingAODVRequestCacheEntry>());
            }

            if (!this.pendingAODVRequests[key].ContainsKey(src))
                this.pendingAODVRequests[key].Add(src, new PendingAODVRequestCacheEntry());
            else if (updateFirstTime == true)
                this.pendingAODVRequests[key][src].firstTime = scheduler.currentTime;

            if (!this.pendingAODVRequests[key][src].prevs.Contains(prev))
                this.pendingAODVRequests[key][src].prevs.Add(prev);
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

        //RoutePacket只是路由普通的数据包，并非专门发往swhub的
        public override bool RoutePacket(Packet pkg)
        {
            int dst = pkg.Dst;
            if (pkg.SrcSenderSeq < 0)//未定该数据包的id
                initPacketSeq(pkg);

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
                Console.WriteLine("{0:F4} [{1}] {2}{3}->{4}{5}, total: {6}, {7}", scheduler.currentTime, pkg.Type, pkg.SrcType, pkg.Src, this.type, this.Id, scheduler.currentTime - pkg.beginSentTime, pkg.getId());
                //
                //SendPacketDirectly(scheduler.currentTime, pkg);
                return true;
            }

            if (this.Id != pkg.Src && CheckTags(pkg) == false)
                return false;

            //是否为某个swHub
            int nextId = FindNextNodeInSWHubRouteTable(dst, pkg.Tags);
            if (nextId > 0)
            {
                PingedSWHub entity = this.cachedPingedSWHubs[dst][nextId];
                pkg.Prev = this.Id;
                pkg.Next = nextId;
                pkg.PrevType = pkg.NextType = NodeType.READER;
                //pkg.TTL = Math.Max(entity.hops + 1, pkg.TTL);
                this.retryOnSendingFailture = true;
                SendPacketDirectly(scheduler.currentTime, pkg);
                this.retryOnSendingFailture = false;
                return true;
            }

            //have to flood...
            SendAODVData(pkg);
            return true;
        }

        public void SendSWData(Packet pkg)
        {
            int dst = pkg.Dst;

            if (pkg.seqInited == false && this.Id == pkg.Src)//未定该数据包的id
                initPacketSeq(pkg);
            Reader node = global.readers[pkg.Prev];
            if (pkg.Type != PacketType.DATA && pkg.Type != PacketType.SW_REQUEST)
                throw new Exception("not DATA or type!");
            //Check Route Table
            if (ExistInNeighborTable(dst))
            {
                SWHubCandidate c = GetSWHubCandidate(dst);
                //当节点运动的时候，可能没有接收到HubCandidates数据，此时放弃直接转发
                if (c != null)
                {
                    //throw new Exception(string.Format("Neighor reader{0} not exist in reader{1}'s HubCandidates", dst, this.Id));
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
                //pkg.TTL = Math.Max(entity.hops + 1, pkg.TTL);
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
                    foreach (int swHub in this.cachedPingedSWHubs.Keys)
                    {
                        Dictionary<int, PingedSWHub> p = this.cachedPingedSWHubs[swHub];
                        int nextId1 = GetNextNodeOfLestHopsFromSwHub(p, pkg.Tags, global.aggressivelyLookForSwHub);
                        if (nextId1 < 0)
                        {
                            nextId1 = swHub;
                            Console.WriteLine("Next node not found for hub Reader{0}", swHub);
                        }
                        else
                            SendSmallWorldRequest(swHub, nextId1, pkg);
                    }
                }
                else
                {
                    int nearestSWHub = GetNearestSWHub(pkg.Tags);
                    if (nearestSWHub > 0)
                    {
                        Dictionary<int, PingedSWHub> p = this.cachedPingedSWHubs[nearestSWHub];
                        int nextId1 = GetNextNodeOfLestHopsFromSwHub(p, pkg.Tags, global.aggressivelyLookForSwHub);
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
            SendAODVRequest(Node.BroadcastNode, this.Id, dst, pkg.TTL - 1, pkg.Tags);
            AddPendingAODVData(pkg);
        }


        public override void AddPendingAODVData(Packet pkg)
        {
            int dst = pkg.Dst;
            uint tags = pkg.Tags;
            string key = dst + "-" + tags;
            if (!this.pendingAODVData.ContainsKey(key))
            {
                this.pendingAODVData.Add(key, new List<PacketCacheEntry>());
            }
            bool found = false;
            foreach (PacketCacheEntry e in this.pendingAODVData[key])
            {
                if (e.pkg == pkg)
                    found = true;
            }
            if (found == false)
                this.pendingAODVData[key].Add(new PacketCacheEntry(pkg, scheduler.currentTime));
        }

        int GetLestHopsFromSwHub(Dictionary<int, PingedSWHub> p, uint tags)
        {
            int hx = 999;
            foreach (int nextId in p.Keys)
            {
                PingedSWHub h = p[nextId];
                if (global.debug)
                    Console.WriteLine("debug nextId:{0}, hops:{1}, local time:{2}, remote time:{3} tags:{4}", nextId, h.hops, h.localUpdateTime, h.remoteUpdateTime, h.tags);

                //这里要考虑beacon的跳数对延迟的影响
                if (h.hops < hx && IsAllowedTags(h.tags, tags)
                    && IsFreshRecord(h.hops, h.remoteUpdateTime, global.beaconInterval, global.beaconInterval * h.hops)
                    && IsFreshRecord(h.hops, h.localUpdateTime, global.beaconInterval / 2, Math.Min(4, global.beaconInterval)))
                {
                    hx = h.hops;
                }
            }
            return hx;
        }

        int GetNextNodeOfLestHopsFromSwHub(Dictionary<int, PingedSWHub> p, uint tags, bool aggressivelyLookForSwHub)
        {
            int hx = 999;
            int next = -1;
            foreach (int n in p.Keys)
            {
                PingedSWHub h = p[n];

                //这里要考虑beacon的跳数对延迟的影响
                if (h.hops < hx && IsAllowedTags(h.tags, tags)
                    && IsFreshRecord(h.hops, h.remoteUpdateTime, global.beaconInterval, global.beaconInterval * h.hops)
                    && IsFreshRecord(h.hops, h.localUpdateTime, global.beaconInterval / 2, Math.Min(4, global.beaconInterval)))
                {
                    hx = h.hops;
                    next = n;
                }
            }
            if (aggressivelyLookForSwHub == false)//无需强制返回结果
                return next;
            if (next > 0)
                return next;
            //比较时间没有结果，那就不比较了
            foreach (int n in p.Keys)
            {
                PingedSWHub h = p[n];

                //这里要考虑beacon的跳数对延迟的影响
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
                if(global.debug)
                    Console.WriteLine("debug p:{0}, hx:{1}", swHub, hx);
                if (hx < mh)
                {
                    mh = hx;
                    ns = swHub;
                }
            }
            return ns;
        }


        public int FindNextNodeInSWHubRouteTable(int dst, uint tags)
        {
            if (!this.cachedPingedSWHubs.ContainsKey(dst))
                return -2;
            foreach (int nextId in this.cachedPingedSWHubs[dst].Keys)
            {
                PingedSWHub p = this.cachedPingedSWHubs[dst][nextId];
                //这里要考虑beacon的跳数对延迟的影响
                if (IsAllowedTags(p.tags, tags)
                    //&& IsFreshRecord(p.hops, p.localUpdateTime, global.beaconInterval / 2, Math.Min(4, global.beaconInterval)))
                    && IsFreshRecord(p.hops, p.remoteUpdateTime, global.beaconInterval, global.beaconInterval * p.hops)
                    && IsFreshRecord(p.hops, p.localUpdateTime, global.beaconInterval / 2, Math.Min(4, global.beaconInterval)))
                    return nextId;
            }
            return -2;
        }

        bool IsAllowedTags(uint selfTags, uint tags)
        {
            return (selfTags | tags) == selfTags;
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
            Console.WriteLine("packetSeq:{0}", this.packetSeq);
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

        /*
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
                if (scheduler.currentTime - p.localUpdateTime >= 5 || CaculateTagNum(p.tags) < CaculateTagNum((uint)pkg.Data))
                {
                    p.localUpdateTime = scheduler.currentTime;
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
        }*/


        public void SendSmallWorldRequest(int dstId, int nextId, Packet pkg)
        {
            //这里借用AODVRequest字段存放数据包的目的地
            Packet pkg1 = (Packet)pkg.Clone();
            pkg1.SWRequest = new SWRequestField(pkg.Src, pkg.Dst, pkg.SrcSenderSeq, pkg.Type, global.swTTL);
            pkg1.Dst = dstId;
            pkg1.Next = nextId;
            pkg1.Src = this.Id;
            pkg1.Type = PacketType.SW_REQUEST;
            pkg1.TTL = global.outerSWTTL;
            this.retryOnSendingFailture = true;
            SendData(pkg1);
            this.retryOnSendingFailture = false;
        }

        public void RecvSmallWorldRequest(Packet pkg)
        {
            if (CheckTags(pkg) == false)
            {
                Console.WriteLine("reader{0} rejects tag{1} from reader{2}", this.Id, pkg.Tags, pkg.Prev);
                return;
            }
            string origPkgId = NodeType.READER.ToString() + pkg.SWRequest.origSrc + "-" + NodeType.READER.ToString() + pkg.SWRequest.origDst + ":" + pkg.SWRequest.origSenderSeq;
            if (global.debug)
                Console.WriteLine("debug: RecvSmallWorldRequest orig packet Id:{0}, ttl:{1}", origPkgId, pkg.TTL);
            

            if (pkg.Dst != this.Id)
            {
                this.retryOnSendingFailture = true;
                SendSWData(pkg);
                this.retryOnSendingFailture = false;
                return;
            }
            pkg.Src = pkg.SWRequest.origSrc;
            pkg.Dst = pkg.SWRequest.origDst;
            pkg.Type = pkg.SWRequest.origType;
            pkg.SrcSenderSeq = pkg.SWRequest.origSenderSeq;
            pkg.TTL = pkg.SWRequest.ttl;
            Console.WriteLine("swrequest ttl:{0}", pkg.TTL);

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
            for (int i = 0; i < max; i++){
                if(this.swHubCandidates[i].nodeId == this.Id)
                    throw new Exception("this.swHubCandidates[i].nodeId is equal to reader"+this.Id);
                result.Add(this.swHubCandidates[i]);
            }
            if(max >0 && result[max - 1].tagEntity.allowedTagNum <= this.availTagEntity.allowedTagNum){
                result.RemoveAt(max - 1);
                result.Add(new SWHubCandidate(this.Id, this.availTagEntity, 0));
            }
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

            //swHub部分
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
            if (this.isSwHub == false
                && global.currentSWHubNumber < global.swHubRatio * global.readerNum && global.currentSWHubNumber < global.maxSwHubs
                && this.Neighbors.Count > global.minSwHubNeighbors
                && n > 0 && this.availTagEntity.allowedTagNum >= this.swHubCandidates[n - 1].tagEntity.allowedTagNum
                && this.availTagEntity.allowedTagNum >= global.minSwHubAvailTagThrethold
                )//自己可以是swhub
            {
                Console.WriteLine("Reader{0} set itself as a swHub", this.Id);
                this.isSwHub = true;
                global.currentSWHubNumber++;
            }

            Dictionary<int, PingedSWHub> p = new Dictionary<int, PingedSWHub>();
            foreach (int dst in this.cachedPingedSWHubs.Keys)
            {
                PingedSWHub nextEntity = getNearestMaxTagFromPingedSwHub(this.cachedPingedSWHubs[dst]);
                if (nextEntity != null)
                    p.Add(dst, new PingedSWHub(nextEntity.hops, nextEntity.tags & this.availTagEntity.allowTags, scheduler.currentTime, nextEntity.remoteUpdateTime));
            }

            if (this.isSwHub == true)
                p.Add(this.Id, new PingedSWHub(0, this.availTagEntity.allowTags, scheduler.currentTime, scheduler.currentTime));

            pkg.Data = new HFBeaconData(this.forwardStrategies, GetNearMaxCandidates(), p);
            SendPacketDirectly(time, pkg);

            float nextBeacon = 0;
            if (scheduler.currentTime < global.beaconWarming)
                nextBeacon = (float)(Utility.P_Rand(10 * (global.beaconWarmingInterval + 0.4)) / 10);//0.5是为了设定最小值
            else if (this.Speed != null && this.Speed.Count > 0 && this.Speed[0] > 1f) //当节点运动时，beacon应频繁些
                nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval/4) / 10);
            else
                nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            Event.AddEvent(new Event(scheduler.currentTime + nextBeacon, EventType.SND_BCN, this, null));
        }

        public PingedSWHub getNearestMaxTagFromPingedSwHub(Dictionary<int, PingedSWHub> p)
        {
            uint tags = 0;
            List<PingedSWHub> list = new List<PingedSWHub>();
            //从p中获得最大的tags列表
            foreach (int n in p.Keys)
            {
                PingedSWHub h = p[n];
                /*
                bool s = (scheduler.currentTime - h.time < global.beaconInterval && scheduler.currentTime > global.beaconWarming)
                    || (scheduler.currentTime - h.time < global.beaconWarmingInterval && scheduler.currentTime <= global.beaconWarming);
                if( s || h.hops >global.innerSWTTL)
                 * */
                if (h.hops > global.innerSWTTL)
                    continue;
                if((h.tags | tags) == h.tags)
                {
                    if (h.tags != tags)
                    {
                        list.Clear();
                    }
                    tags = h.tags;
                    list.Add(h);
                }
            }
            //从列表中找到最近的节点
            PingedSWHub nextEntity = null;
            int mhop = 999;
            foreach (PingedSWHub entity in list)
            {
                if (entity.hops < mhop)
                {
                    mhop = entity.hops;
                    nextEntity = entity;
                }
            }
            return nextEntity;
        }

        public override void RecvBeacon(Packet pkg)
        {
            Scheduler scheduler = Scheduler.getInstance();
            Reader node = global.readers[pkg.Prev];

            if (pkg.Prev == Id && pkg.PrevType == type)
                return;

            HFNeighbor nb = null;
            if (Neighbors.ContainsKey(node.Id))
                nb = (HFNeighbor)Neighbors[node.Id];
            if (nb != null)
            {
                nb.lastBeacon = scheduler.currentTime;
            }
            else
            {
                //Add as a neighbor
                AddNeighbor(node);
            }

            string key = pkg.Src.ToString();
            if (!this.routeTable.ContainsKey(key))
                this.routeTable.Add(key, new RouteEntity(pkg.Prev, pkg.Prev, 1, scheduler.currentTime, scheduler.currentTime));
            else
            {
                this.routeTable[key].hops = 1;
                this.routeTable[key].next = pkg.Prev;
                this.routeTable[key].remoteLastUpdatedTime = scheduler.currentTime;
                this.routeTable[key].localLastUpdatedTime = scheduler.currentTime;
            }

            if (pkg.Beacon != null)//避免其他情况
            {
                //if (nb == null || nb.lastBeacon < 0)
                if (nb == null)
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

                Dictionary<int, PingedSWHub> pingedSwHubs = data.PingedSwHubs;
                foreach (int dst in pingedSwHubs.Keys)
                {
                    if (this.Id == dst)
                        continue;
                    PingedSWHub hub = pingedSwHubs[dst];
                    if (hub.hops > global.outerSWTTL) //太远了也忽略
                        continue;
                    if (!this.cachedPingedSWHubs.ContainsKey(dst))
                        this.cachedPingedSWHubs.Add(dst, new Dictionary<int, PingedSWHub>());
                    if (!this.cachedPingedSWHubs[dst].ContainsKey(pkg.Prev))
                        this.cachedPingedSWHubs[dst].Add(pkg.Prev, new PingedSWHub(hub.hops + 1, hub.tags, scheduler.currentTime, hub.remoteUpdateTime));
                    else
                    {
                        this.cachedPingedSWHubs[dst][pkg.Prev].hops = hub.hops + 1;
                        this.cachedPingedSWHubs[dst][pkg.Prev].tags = hub.tags;
                        this.cachedPingedSWHubs[dst][pkg.Prev].localUpdateTime = scheduler.currentTime;
                        this.cachedPingedSWHubs[dst][pkg.Prev].remoteUpdateTime = hub.remoteUpdateTime;
                    }
                }
            }
        }

        public override Neighbor AddNeighbor(Reader nb)
        {
            if (!this.Neighbors.ContainsKey(nb.Id))
                this.Neighbors.Add(nb.Id, new HFNeighbor(nb));
            if (!this.routeTable.ContainsKey(nb.Id.ToString()))
                this.routeTable.Add(nb.Id.ToString(), new RouteEntity(nb.Id, nb.Id, 1, scheduler.currentTime, scheduler.currentTime));
            return this.Neighbors[nb.Id];
        }


        private bool CheckTags(Packet pkg)
        {
            uint tags = pkg.Tags;
            foreach (ForwardStrategy f in forwardStrategies)//one found.
            {
                if(f.Action == ForwardStrategyAction.REFUSE && (tags & f.Tags) != 0)
                {
                    Console.WriteLine("{0:F4} [{1}] {2}{3} drop from {4}{5} due to FORWARD_STRATEGIES", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    return false;
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
            if (
                global.currentSWHubNumber < global.swHubRatio * global.readerNum 
                && global.currentSWHubNumber < global.maxSwHubs
                //&& this.Neighbors.Count > global.minSwHubNeighbors
                && n > 0 && this.availTagEntity.allowedTagNum >= this.swHubCandidates[n - 1].tagEntity.allowedTagNum
                && this.availTagEntity.allowedTagNum >= global.minSwHubAvailTagThrethold
                && this.isSwHub == false)//自己可以是swhub
            {
                Console.WriteLine("Reader{0} set itself as a swHub", this.Id);
                this.isSwHub = true;
                global.currentSWHubNumber++;
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

                if (pkg.seqInited == false) //如果packetSeq小于0，则说明未定该数据包的id
                    this.packetSeq++;

                for (int i = 0; i < list.Count; i++)
                {
                    Packet pkg1 = pkg.Clone() as Packet;
                    pkg1.DelPacketNode = list[0].Id;
                    if (pkg.seqInited == false)
                    {
                        pkg1.PrevSenderSeq = this.packetSeq;
                        if (pkg.Src == Id)
                            pkg1.SrcSenderSeq = pkg1.PrevSenderSeq;
                    }

                    //Console.WriteLine("+packet count: {0}->{1} {2}_{3}", pkg.Prev, pkg.Next, global.readers[pkg.Prev].packetCounter,pkg.PacketSeq);
                    recv_time = global.processDelay + (float)(Utility.Distance(this, list[i]) / global.lightSpeed);
                    Event.AddEvent(
                        new Event(time + recv_time, EventType.RECV, list[i], pkg1));
                }
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

                        if (pkg.seqInited == false) //如果packetSeq小于0，则说明未定该数据包的id
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
                            if (pkg.seqInited == false)
                            {
                                pkg1.PrevSenderSeq = this.packetSeq;
                                if (pkg.Src == Id)
                                    pkg1.SrcSenderSeq = pkg1.PrevSenderSeq;
                            }
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
