﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;

namespace HeterogeneousForward
{

    public class SortPingedSWHub : IComparer<PingedSWHub>
    {
        int getTagNum(uint tag)
        {
            int n = 0;
            while (tag != 0)
            {
                if ((tag ^ 1) == 1)
                    n++;
                tag = tag >> 1;
            }
            return n;
        }
        int IComparer<PingedSWHub>.Compare(PingedSWHub x, PingedSWHub y)
        {
            int t1 = getTagNum(x.tags);
            int t2 = getTagNum(y.tags);
            if (t1 == t2)
                return x.hops - y.hops;
            return t1 - t2;
        }
    }

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
        public List<PingedSWHub> PingedSwHubs;
        public HFBeaconData(List<ForwardStrategy> f, List<SWHubCandidate> n, List<PingedSWHub> p)
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
        public int dst;
        public int next;
        public double localUpdateTime;
        public double remoteUpdateTime;

        public PingedSWHub(int dst, int prev, int hops, uint tags, double localUpdateTime, double remoteUpdateTime)
        {
            this.dst = dst;
            this.next = prev;
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
        Dictionary<int, Dictionary<string, PingedSWHub>> cachedPingedSWHubs = null;
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
            this.cachedPingedSWHubs = new Dictionary<int, Dictionary<string, PingedSWHub>>();
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
            if (global.routeMethod == RouteMethod.AODV)
            {
                base.RecvAODVRequest(pkg);
                return;
            }

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
            if (global.routeMethod == RouteMethod.AODV)
            {
                base.RecvAODVReply(pkg);
                return;
            }
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
            if (global.routeMethod == RouteMethod.AODV)
            {
                base.SendAODVData(pkg, dst);
                return;
            }
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
            SendAODVRequest(Node.BroadcastNode, this.Id, dst, pkg.TTL - 1, pkg.Tags);
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
            if (this.Id != pkg.Src && CheckTags(pkg) == false && pkg.Dst != this.Id)
            {
                Console.WriteLine("reader{0} rejects tag{1} from reader{2}", this.Id, pkg.Tags, pkg.Prev);
                return false;
            }

            if (global.routeMethod == RouteMethod.AODV)
            {
                return base.RoutePacket(pkg);
            }
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
                Console.WriteLine("{0:F4} [{1}] {2}{3}->{4}{5}, total: {6}, pkgId: {7}", scheduler.currentTime, "RECV_DATA", pkg.SrcType, pkg.Src, this.type, this.Id, scheduler.currentTime - pkg.beginSentTime, pkg.getId());
                //
                //SendPacketDirectly(scheduler.currentTime, pkg);
                return true;
            }

            //if (this.Id != pkg.Src && CheckTags(pkg) == false)
            //    return false;

            //是否为某个swHub
            PingedSWHub nextEntity = FindNextNodeInSWHubRouteTable(dst, pkg.Tags);
            if (nextEntity != null)
            {
                pkg.Prev = this.Id;
                pkg.Next = nextEntity.next;
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
            if (pkg.Type != PacketType.DATA && pkg.Type != PacketType.SW_DATA)
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
            PingedSWHub entity = FindNextNodeInSWHubRouteTable(dst, pkg.Tags);
            if (entity != null) 
            {
                if(global.debug)
                    Console.WriteLine("pick up the next hop {0}->{1} hops:{2}, tags:{3}, local: {4}, remote:{5}", entity.next, entity.dst, entity.hops, entity.tags, entity.localUpdateTime, entity.remoteUpdateTime);
                pkg.Prev = this.Id;
                pkg.Next = entity.next;
                pkg.PrevType = pkg.NextType = NodeType.READER;
                //pkg.TTL = Math.Max(entity.hops + 1, pkg.TTL);
                this.retryOnSendingFailture = true;
                SendPacketDirectly(scheduler.currentTime, pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            //如果没找到，则发往所有的swHub
            if ( pkg.Type != PacketType.PING_REQUEST && pkg.Type!= PacketType.PING_RESPONSE && pkg.Type!= PacketType.SW_DATA
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
                        Dictionary<string, PingedSWHub> p = this.cachedPingedSWHubs[swHub];
                        //swTTL应该保持在swrequest字段中
                        if (pkg.SWRequest == null)
                            throw new Exception("pkg SWRequest is null!");
                        PingedSWHub entity1 = GetNextNodeOfLestHopsFromSwHub(p, pkg.Tags, global.aggressivelyLookForSwHub);
                        if (entity1 == null)
                        {
                            Console.WriteLine("Next node not found for hub Reader{0}", swHub);
                        }
                        else
                            SendSmallWorldRequest(swHub, entity1.next, pkg, entity1.hops, pkg.SWRequest.swTTL);
                    }
                }
                else
                {
                    int nearestSWHub = GetNearestSWHub(pkg.Tags);
                    if (nearestSWHub > 0)
                    {
                        Dictionary<string, PingedSWHub> p = this.cachedPingedSWHubs[nearestSWHub];
                        if (pkg.SWRequest == null)
                            throw new Exception("pkg SWRequest is null!");
                        PingedSWHub entity1 = GetNextNodeOfLestHopsFromSwHub(p, pkg.Tags, global.aggressivelyLookForSwHub);
                        if (entity1 != null)
                            SendSmallWorldRequest(nearestSWHub, entity1.next, pkg, entity1.hops, pkg.SWRequest.swTTL);
                        else
                            Console.WriteLine("Next node not found for hub Reader{0}", nearestSWHub);
                    }
                    else
                        Console.WriteLine("nearestSWHub is empty, abort... ");

                }
            }

            //同时也发送aodv请求
            Console.WriteLine("{0:F4} [{1}] {2}{3} tries to send to {4}{5} but no route", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.DstType, pkg.Dst);
            //这里如果为了增加成功率，可以设ttl为global.ttl，而非团内的半径
            pkg.TTL = global.innerSWTTL;
            //pkg.TTL = global.TTL;
            SendAODVRequest(Node.BroadcastNode, this.Id, dst, pkg.TTL - 1, pkg.Tags);
            AddPendingAODVData(pkg);
        }


        public override void AddPendingAODVData(Packet pkg)
        {
            if (global.routeMethod == RouteMethod.AODV)
            {
                base.AddPendingAODVData(pkg);
                return;
            }

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

        int GetLestHopsFromSwHub(Dictionary<string, PingedSWHub> p, uint tags)
        {
            int hx = 999;
            foreach (string key in p.Keys)
            {
                PingedSWHub h = p[key];
                int nextId = h.next;
                if (global.debug)
                    Console.WriteLine("debug nextId:{0}, hops:{1}, local time:{2}, remote time:{3} tags:{4}", nextId, h.hops, h.localUpdateTime, h.remoteUpdateTime, h.tags);

                //这里要考虑beacon的跳数对延迟的影响
                if (h.hops < hx && IsAllowedTags(h.tags, tags)
                    && IsFreshRecord(h.hops, h.remoteUpdateTime, global.beaconInterval, global.beaconInterval * h.hops)
                    && IsFreshRecord(1, h.localUpdateTime, global.beaconInterval, Math.Min(4, global.beaconInterval)))
                {
                    hx = h.hops;
                }
            }
            return hx;
        }

        PingedSWHub GetNextNodeOfLestHopsFromSwHub(Dictionary<string, PingedSWHub> p, uint tags, bool aggressivelyLookForSwHub)
        {
            int hx = 999;
            int next = -1;
            PingedSWHub minh = null;
            foreach (string key in p.Keys)
            {
                PingedSWHub h = p[key];

                //这里要考虑beacon的跳数对延迟的影响
                if (h.hops < hx && IsAllowedTags(h.tags, tags)
                    && IsFreshRecord(h.hops, h.remoteUpdateTime, global.beaconInterval, global.beaconInterval * h.hops)
                    && IsFreshRecord(1, h.localUpdateTime, global.beaconInterval, Math.Min(4, global.beaconInterval))
                    )
                {
                    hx = h.hops;
                    next = h.next;
                    minh = h;
                }
            }
            if (aggressivelyLookForSwHub == false)//无需强制返回结果
                return minh;
            if (next > 0)
                return minh;
            //比较时间没有结果，那就不比较了
            minh = null;
            foreach (string key in p.Keys)
            {
                PingedSWHub h = p[key];

                //这里要考虑beacon的跳数对延迟的影响
                if (h.hops < hx && IsAllowedTags(h.tags, tags))
                {
                    hx = h.hops;
                    next = h.next;
                    minh = h;
                }
            }
            return minh;
        }


        int GetNearestSWHub(uint tags)
        {
            int mh = 999;
            int ns = 0;

            if (this.cachedPingedSWHubs.Count == 0)
                return -1;

            foreach (int swHub in this.cachedPingedSWHubs.Keys)
            {
                Dictionary<string, PingedSWHub> p = this.cachedPingedSWHubs[swHub];

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


        public PingedSWHub FindNextNodeInSWHubRouteTable(int dst, uint tags)
        {
            if (!this.cachedPingedSWHubs.ContainsKey(dst))
                return null;
            List<PingedSWHub> temp = new List<PingedSWHub>();
            foreach (string key in this.cachedPingedSWHubs[dst].Keys)
            {
                PingedSWHub p = this.cachedPingedSWHubs[dst][key];
                //这里要考虑beacon的跳数对延迟的影响
                if (IsAllowedTags(p.tags, tags)
                    //&& IsFreshRecord(p.hops, p.localUpdateTime, global.beaconInterval / 2, Math.Min(4, global.beaconInterval)))
                    && IsFreshRecord(p.hops, p.remoteUpdateTime, global.beaconInterval, global.beaconInterval * p.hops)
                    && IsFreshRecord(1 , p.localUpdateTime, global.beaconInterval , Math.Min(4, global.beaconInterval)))
                    temp.Add(p) ;
            }
            int minhop = 999;
            PingedSWHub minhub = null;
            foreach (PingedSWHub hub in temp)
            {
                if (hub.hops < minhop)
                {
                    minhop = hub.hops;
                    minhub = hub;
                }
            }
            return minhub;
        }

        public bool IsAllowedTags(uint selfTags, uint tags)
        {
            return (selfTags | tags) == selfTags;
        }


        public bool IsAllowedTags(uint tags)
        {
            if (this.forwardStrategies != null)
            {
                if(this.availTagEntity == null)
                    this.availTagEntity = CaculateTagEntity(this.forwardStrategies);
                return (this.availTagEntity.allowTags | tags) == this.availTagEntity.allowTags;
            }
            else
                return false;
        }

        public bool IsAllowedAllTags()
        {
            unchecked
            {
                uint mask = ((uint)-1) >> (sizeof(uint) * 8 - global.tagNameNum);
                return this.IsAllowedTags(mask);
            }
                 
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

        public int[][] ComputeSmallWorldTopology()
        {
            int totalV = 0;
            int totalTaggedV = 0;
            //有向的
            int rewired = 0;
            int[][] taggedHops = new int[global.readerNum][];
            int[][] hops = new int[global.readerNum][];
            int taggedmaxhop = 0;
            double tagged_avghop = 0;
            int hubs = 0;

            for (int i = 0; i < global.readerNum; i++)
            {
                taggedHops[i] = new int[global.readerNum];
                hops[i] = new int[global.readerNum];
                for (int j = 0; j < global.readerNum; j++)
                {
                    taggedHops[i][j] = 999;
                    hops[i][j] = 999;
                }
            }

            for (int i = 0; i < global.readerNum; i++)
            {
                HFReader r1 = (HFReader)global.readers[i];
                for (int j = 0; j < global.readerNum; j++)
                {
                    HFReader r2 = (HFReader)global.readers[j];
                    if (Utility.Distance(r1, r2) <= global.nodeMaxDist)
                    {
                        if (r1.Id == r2.Id)
                        {
                            taggedHops[i][j] = 0;
                            hops[i][j] = 0;
                            continue;
                        }
                        totalV++;
                        hops[i][j] = 1;
                        if (r1.IsAllowedTags(global.currentSendingTags)
                            && r2.IsAllowedTags(global.currentSendingTags)
                            && global.currentSendingTags != 0)
                        {
                            totalTaggedV++;
                            //添加邻居节点
                            taggedHops[i][j] = 1;
                        }
                    }
                }

                if (r1.isSwHub)
                {
                    hubs++;
                    foreach (int swHub in r1.cachedPingedSWHubs.Keys)
                    {

                        if (Utility.Distance(r1, (MobileNode)Node.getNode(swHub, NodeType.READER)) <= global.nodeMaxDist)
                        {
                            continue;
                        }
                        totalV++;
                        hops[i][swHub] = 1;

                        Dictionary<string, PingedSWHub> p = r1.cachedPingedSWHubs[swHub];
                        PingedSWHub entity = GetNextNodeOfLestHopsFromSwHub(p, global.currentSendingTags, global.aggressivelyLookForSwHub);
                        if (entity == null)//没有到对方的路径
                            continue;
                        //swHub之间的路径就是rewire的结果
                        rewired++;
                        totalTaggedV++;

                        //添加快捷路径
                        taggedHops[i][swHub] = 1;
                        if(global.printTopology)
                            Console.WriteLine("{0}->{1}", i, swHub);
                    }
                }
            }

            for (int p = 0; p < 15; p++)//循环15轮
            {
                for (int i = 0; i < global.readerNum; i++)
                {
                    HFReader r1 = (HFReader)global.readers[i];


                    for (int j = 0; j < global.readerNum; j++)
                    {
                        HFReader r2 = (HFReader)global.readers[j];
                        List<int> list = new List<int>();
                        for (int q = 0; q < hops[j].Length; q++)
                        {
                            if (hops[j][q] == 1)
                                list.Add(q);
                        }
                        for (int k = 0; k < list.Count; k++)
                        {
                            HFReader r3 = (HFReader)global.readers[list[k]];
                            //r3是r2的邻居，且r3允许标签，且r1-r2-r3的跳数小于原r1到r3的跳数
                            if (hops[i][j] + 1 < hops[i][r3.Id])
                            {
                                hops[i][r3.Id] = hops[i][j] + 1;
                            }
                        }
                    }


                    if (!r1.IsAllowedTags(global.currentSendingTags))
                        continue;
                    for (int j = 0; j < global.readerNum; j++)
                    {
                        HFReader r2 = (HFReader)global.readers[j];
                        if (!r2.IsAllowedTags(global.currentSendingTags))
                            continue;

                        List<int> list = new List<int>();
                        for (int q = 0; q < taggedHops[j].Length; q++)
                        {
                            if (taggedHops[j][q] ==1)
                                list.Add(q);
                        }

                        for (int k = 0; k < list.Count; k++)
                        {
                            HFReader r3 = (HFReader)global.readers[list[k]];
                            //r3是r2的邻居，且r3允许标签，且r1-r2-r3的跳数小于原r1到r3的跳数
                            if (r3.IsAllowedTags(global.currentSendingTags) && taggedHops[i][j] + 1 < taggedHops[i][r3.Id])
                            {
                                taggedHops[i][r3.Id] = taggedHops[i][j] + 1;
                                //Console.WriteLine("[{0}=>{1}: {2}], hop:{3}", i, r3.Id, j, hops[i][j] + 1);
                            }
                        }
                    }
                }
            }

            // 求最长路径和平均路径长度
            int n = 0;
            int fail = 0;
            double avghop = 0;
            double maxhop = 0;
            for (int i = 0; i < global.readerNum; i++)
            {
                HFReader r1 = (HFReader)global.readers[i];
                for (int j = i + 1; j < global.readerNum; j++)
                {
                    HFReader r2 = (HFReader)global.readers[j];
                    if (hops[i][j] > 900)
                    {
                        if (global.printTopology)
                            Console.WriteLine("{0}->{1}", i, j);
                        fail++;
                    }

                    if (hops[i][j] > maxhop)
                        maxhop = hops[i][j];
                    avghop += hops[i][j];
                    n++;
                }
            }
            avghop = avghop / n;

            n = 0;
            fail = 0;
            for (int i = 0; i < global.readerNum; i++)
            {
                HFReader r1 = (HFReader)global.readers[i];
                if (!r1.IsAllowedTags(global.currentSendingTags))
                    continue;
                for (int j = i + 1; j < global.readerNum; j++)
                {
                    HFReader r2 = (HFReader)global.readers[j];
                    if (!r2.IsAllowedTags(global.currentSendingTags))
                        continue;
                    if (taggedHops[i][j] > 900)
                    {
                        if (global.printTopology)
                            Console.WriteLine("failed: {0}->{1}", i, j);
                        fail++;
                        continue;
                    }

                    if (taggedHops[i][j] > taggedmaxhop)
                        taggedmaxhop = taggedHops[i][j];
                    tagged_avghop += taggedHops[i][j];
                    n++;
                }
            }
            tagged_avghop = tagged_avghop / n;


            // 求集聚度
            double c = 0;
            n = 0;
            for (int i = 0; i < global.readerNum; i++)
            {
                HFReader r1 = (HFReader)global.readers[i];
                List<HFReader> nbs = new List<HFReader>();
                int triplets = 0;
                int connectedvertices = 0;
                n++;
                for (int j = 0; j < global.readerNum; j++)
                {
                    HFReader r2 = (HFReader)global.readers[j];
                    if (hops[i][j] == 1)
                        nbs.Add(r2);
                }
                HashSet<string> set = new HashSet<string>();
                for (int j = 0; j < nbs.Count; j++)
                {
                    HFReader r2 = nbs[j];
                    for (int k = j + 1; k < nbs.Count; k++)
                    {
                        HFReader r3 = nbs[k];
                        for (int l = k + 1; l < nbs.Count; l++)
                        {
                            HFReader r4 = nbs[l];
                            if (hops[r2.Id][r3.Id] == 1 && !set.Contains(r2.Id + ":" + r3.Id))
                            {
                                triplets++;
                                set.Add(r2.Id + ":" + r3.Id);
                            }
                            if (hops[r2.Id][r4.Id] == 1 && !set.Contains(r2.Id + ":" + r4.Id))
                            {
                                triplets++;
                                set.Add(r2.Id + ":" + r4.Id);
                            }
                            if (hops[r3.Id][r4.Id] == 1 && !set.Contains(r3.Id + ":" + r4.Id))
                            {
                                triplets++;
                                set.Add(r3.Id + ":" + r4.Id);
                            }
                            if (hops[r3.Id][r2.Id] == 1 && !set.Contains(r3.Id + ":" + r2.Id))
                            {
                                triplets++;
                                set.Add(r3.Id + ":" + r2.Id);
                            }
                            if (hops[r4.Id][r2.Id] == 1 && !set.Contains(r4.Id + ":" + r2.Id))
                            {
                                triplets++;
                                set.Add(r4.Id + ":" + r2.Id);
                            }
                            if (hops[r4.Id][r3.Id] == 1 && !set.Contains(r4.Id + ":" + r3.Id))
                            {
                                triplets++;
                                set.Add(r4.Id + ":" + r3.Id);
                            }
                        }
                    }
                }
                connectedvertices = nbs.Count*(nbs.Count-1);
                if (connectedvertices == 0)
                    continue;
                c += (double)triplets / connectedvertices;
            }
            c = c / n;

            double taggedc = 0;
            n = 0;
            for (int i = 0; i < global.readerNum; i++)
            {
                HFReader r1 = (HFReader)global.readers[i];
                if (!r1.IsAllowedTags(global.currentSendingTags))
                    continue;
                List<HFReader> nbs = new List<HFReader>();
                int triplets = 0;
                int connectedvertices = 0;
                n++;
                for (int j = 0; j < global.readerNum; j++)
                {
                    HFReader r2 = (HFReader)global.readers[j];
                    if (!r2.IsAllowedTags(global.currentSendingTags))
                        continue;
                    if (hops[i][j] == 1)
                        nbs.Add(r2);
                }
                HashSet<string> set = new HashSet<string>();
                for (int j = 0; j < nbs.Count-2; j++)
                {
                    HFReader r2 = nbs[j];
                    for (int k = j + 1; k < nbs.Count-1; k++)
                    {
                        HFReader r3 = nbs[k];
                        for (int l = k + 1; l < nbs.Count; l++)
                        {
                            HFReader r4 = nbs[l];
                            if (hops[r2.Id][r3.Id] == 1 && !set.Contains(r2.Id + ":" + r3.Id))
                            {
                                triplets++;
                                set.Add(r2.Id + ":" + r3.Id);
                            }
                            if (hops[r2.Id][r4.Id] == 1 && !set.Contains(r2.Id + ":" + r4.Id))
                            {
                                triplets++;
                                set.Add(r2.Id + ":" + r4.Id);
                            }
                            if (hops[r3.Id][r4.Id] == 1 && !set.Contains(r3.Id + ":" + r4.Id))
                            {
                                triplets++;
                                set.Add(r3.Id + ":" + r4.Id);
                            }
                            if (hops[r3.Id][r2.Id] == 1 && !set.Contains(r3.Id + ":" + r2.Id))
                            {
                                triplets++;
                                set.Add(r3.Id + ":" + r2.Id);
                            }
                            if (hops[r4.Id][r2.Id] == 1 && !set.Contains(r4.Id + ":" + r2.Id))
                            {
                                triplets++;
                                set.Add(r4.Id + ":" + r2.Id);
                            }
                            if (hops[r4.Id][r3.Id] == 1 && !set.Contains(r4.Id + ":" + r3.Id))
                            {
                                triplets++;
                                set.Add(r4.Id + ":" + r3.Id);
                            }
                        }
                    }
                }

                connectedvertices = nbs.Count * (nbs.Count - 1);
                if (connectedvertices == 0)
                    continue;
                taggedc += (double)triplets / connectedvertices;
            }
            taggedc = taggedc / n;
            if (global.printTopology)
            {
                Console.WriteLine("SW totalV\ttotalTaggedV\ttagged avghop\trewired\tfail\thubs\tavghop\tc\ttaggedc\tmaxhop\ttaggedmaxhop");
                Console.WriteLine("SWresult\t{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}", totalV, totalTaggedV, tagged_avghop, rewired, fail, hubs, avghop, c, taggedc, maxhop, taggedmaxhop);
            }
            return taggedHops;
        }


        public void ComputeAODVTopology()
        {
            int totalV = 0;
            int totalTaggedV = 0;
            int[][] hops = new int[global.readerNum][];
            int maxhop = 0;
            double avghop = 0;

            for (int i = 0; i < global.readerNum; i++)
            {
                hops[i] = new int[global.readerNum];
                for (int j = 0; j < global.readerNum; j++)
                    hops[i][j] = 999;
            }

            for (int i = 0; i < global.readerNum; i++)
            {
                HFReader r1 = (HFReader)global.readers[i];
                for (int j = 0; j < global.readerNum; j++)
                {
                    HFReader r2 = (HFReader)global.readers[j];
                    if (Utility.Distance(r1, r2) <= global.nodeMaxDist)
                    {
                        totalV++;
                        if (r1.IsAllowedTags(global.currentSendingTags)
                            && r2.IsAllowedTags(global.currentSendingTags)
                            && global.currentSendingTags != 0)
                        {
                            totalTaggedV++;
                            //添加邻居节点
                            hops[i][j] = 1;
                        }
                    }
                }
            }

            for (int i = 0; i < global.readerNum; i++)
            {
                HFReader r1 = (HFReader)global.readers[i];
                if (!r1.IsAllowedTags(global.currentSendingTags))
                    continue;
                for (int p = 0; p < 15; p++)//循环15轮
                {
                    for (int j = 0; j < global.readerNum; j++)
                    {
                        HFReader r2 = (HFReader)global.readers[j];
                        if (!r2.IsAllowedTags(global.currentSendingTags))
                            continue;
                        int h = hops[i][j];
                        List<Reader> list = r2.GetAllNearReaders(global.nodeMaxDist, true);
                        for (int k = 0; k < list.Count; k++)
                        {
                            HFReader r3 = (HFReader)list[k];
                            //r3是r2的邻居，且r3允许标签，且r1-r2-r3的跳数小于原r1到r3的跳数
                            if (r3.IsAllowedTags(global.currentSendingTags) && h + 1 < hops[i][r3.Id])
                                hops[i][r3.Id] = h + 1;
                        }
                    }
                }
            }

            int n = 0;
            // 求最长路径和平均路径长度
            for (int i = 0; i < global.readerNum; i++)
            {
                HFReader r1 = (HFReader)global.readers[i];
                if (!r1.IsAllowedTags(global.currentSendingTags))
                    continue;
                for (int j = i + 1; j < global.readerNum; j++)
                {
                    HFReader r2 = (HFReader)global.readers[j];
                    if (!r2.IsAllowedTags(global.currentSendingTags))
                        continue;
                    if (hops[i][j] > 900)
                        Console.WriteLine("{0}->{1}", i, j);
                    if (hops[i][j] > maxhop)
                        maxhop = hops[i][j];
                    avghop += hops[i][j];
                    n++;
                }
            }
            avghop = avghop / n;
            Console.WriteLine("AODV totalV\ttotalTaggedV\tavghop\tmaxhop");
            Console.WriteLine("{0}\t{1}\t{2}\t{3}", totalV, totalTaggedV, avghop, maxhop);
        }

        public override bool SendData(Packet pkg)
        {
            //Console.WriteLine("packetSeq:{0}", this.packetSeq);
            if (global.routeMethod == RouteMethod.AODV)
            {
                /*
                if (this.Id == pkg.Src)
                {
                    ComputeSmallWorldTopology();
                    ComputeAODVTopology();
                }*/
                RoutePacket(pkg);
            }
            else if (global.routeMethod == RouteMethod.SW_AODV)
            {
                //使用带tag的aodv方法
                SendAODVData(pkg);
            }
            else if (global.routeMethod == RouteMethod.SmallWorld)//否则为smallworld方法
            {
                if (this.Id == pkg.Src)
                {
                    if(pkg.SWRequest == null) //初始化swttl
                        pkg.SWRequest = new SWRequestField(-1, -1, -1, PacketType.SW_DATA, global.swTTL);
                    
                    if (pkg.inited == false && pkg.Type == PacketType.DATA)
                    {
                        pkg.inited = true;
                        //ComputeAODVTopology();
                        //不做事情了，直接返回
                        if (global.printTopology == true)
                            return true;
                        else if(global.printIdealSucc == true)
                        {
                            int h = 0;
                            if (((HFReader)global.readers[pkg.Src]).isSwHub && ((HFReader)global.readers[pkg.Src]).isSwHub)
                                h = 1;
                            else if (((HFReader)global.readers[pkg.Dst]).isSwHub || ((HFReader)global.readers[pkg.Src]).isSwHub)
                                h = global.innerSWTTL + 1;
                            else
                                h = 2 * global.innerSWTTL + 1;

                            int[][] taggedHops = ComputeSmallWorldTopology();

                            Reader dnode = (Reader)Node.getNode(pkg.Dst, NodeType.READER);
                            foreach (Neighbor nb in dnode.Neighbors.Values)
                            {
                                if (taggedHops[pkg.Src][nb.node.Id] + 1 <= h)
                                {
                                    Console.WriteLine("{0:F4} [IDEAL_SUCC], {1}->{2}", scheduler.currentTime, pkg.Src, pkg.Dst);
                                    break;
                                }
                            }
                        }
                    }
                    SendSWData(pkg);
                }
                else
                    RoutePacket(pkg);
            }
            else
                throw new Exception("Unknown RouteMethod " + global.routeMethod);
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


        public void SendSmallWorldRequest(int dstId, int nextId, Packet pkg, int ttl, int swttl)
        {
            //这里借用AODVRequest字段存放数据包的目的地
            Packet pkg1 = (Packet)pkg.Clone();
            pkg1.SWRequest = new SWRequestField(pkg.Src, pkg.Dst, pkg.SrcSenderSeq, pkg.Type, swttl);
            pkg1.Dst = dstId;
            pkg1.Next = nextId;
            pkg1.Src = this.Id;
            pkg1.Type = PacketType.SW_DATA;
            pkg1.TTL = Math.Max(global.outerSWTTL+1, ttl);
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

            // to itself，还原
            pkg.Src = pkg.SWRequest.origSrc;
            pkg.Dst = pkg.SWRequest.origDst;
            pkg.Type = pkg.SWRequest.origType;
            pkg.SrcSenderSeq = pkg.SWRequest.origSenderSeq;
            pkg.TTL = pkg.SWRequest.swTTL;
            Console.WriteLine("swrequest ttl:{0}", pkg.TTL);

            string packetId = pkg.getId();
            if (this.receivedSWPackets.Contains(packetId))
                return;
            else
                this.receivedSWPackets.Add(packetId);

            //原数据包是不是给它的？
            if (pkg.Dst == Id && pkg.DstType == NodeType.READER)
            {
                string pkgId = pkg.getId();
                if (!this.receivedPackets.Contains(pkgId))
                {
                    this.receivedPackets.Add(pkgId);
                    Console.WriteLine("{0:F4} [RECV_DATA] {1} recv data {2}{3}->{4}{5}, total: {6}", this.Id, pkg.Type, pkg.SrcType, pkg.Src, this.type, this.Id, scheduler.currentTime - pkg.beginSentTime);
                }
                return;
            }

            pkg.SWRequest.swTTL--;
            if(pkg.SWRequest.swTTL >=0)          //向swHub及自己区域内发送
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
                
            uint mask = (uint)(1 << (global.tagNameNum-1)+1)-1;
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
                && (double)global.currentSWHubNumber / (double)global.readerNum < global.maxSwHubRatio 
                && (global.currentSWHubNumber < global.maxSwHubs || global.printTopology == true)
                && this.Neighbors.Count > global.minSwHubNeighbors
                && n > 0 && this.availTagEntity.allowedTagNum >= this.swHubCandidates[n - 1].tagEntity.allowedTagNum
                && this.availTagEntity.allowedTagNum >= global.minSwHubAvailTagThrethold
                )//自己可以是swhub
            {
                Console.WriteLine("Reader{0} set itself as a swHub", this.Id);
                this.isSwHub = true;
                global.currentSWHubNumber++;
            }

            List<PingedSWHub> p = new List<PingedSWHub>();
            foreach (int dst in this.cachedPingedSWHubs.Keys)
            {
                if (global.debug && dst == 1)
                {
                    foreach (KeyValuePair<string, PingedSWHub> pair in this.cachedPingedSWHubs[dst])
                        Console.WriteLine("{0}, hops:{1}, tag:{2}", pair.Key, pair.Value.hops, pair.Value.tags);
                }
                List<PingedSWHub> nextEntities = getNearestEntitiesOfMaxTagFromPingedSwHub(this.cachedPingedSWHubs[dst], 3);
                foreach (PingedSWHub nextEntity in nextEntities)
                {
                    if (nextEntity != null && (nextEntity.tags & this.availTagEntity.allowTags) != 0)
                    {
                        if (global.debug && dst == 1)
                        {
                            Console.WriteLine("beacon: {0}->{1}\t{2}\t{3}\t{4}", this.Id, dst, nextEntity.hops, nextEntity.tags, nextEntity.tags & this.availTagEntity.allowTags);
                        }
                        p.Add(new PingedSWHub(dst, this.Id, nextEntity.hops, nextEntity.tags & this.availTagEntity.allowTags, scheduler.currentTime, nextEntity.remoteUpdateTime));
                    }
                }
            }

            if (this.isSwHub == true)
                p.Add(new PingedSWHub(this.Id, this.Id, 0, this.availTagEntity.allowTags, scheduler.currentTime, scheduler.currentTime));

            pkg.Data = new HFBeaconData(this.forwardStrategies, GetNearMaxCandidates(), p);
            SendPacketDirectly(time, pkg);

            float nextBeacon = 0;
            if (scheduler.currentTime < global.beaconWarming)
                nextBeacon = (float)(Utility.P_Rand(10 * (global.beaconWarmingInterval + 0.4)) / 10);//0.5是为了设定最小值
             //TODO 这里运动节点多的话，可以
            else if (global.smartBeacon == true) //当节点运动时，beacon应频繁些
            {
                bool s = false;
                if (this.Speed != null && this.Speed.Count > 0 && this.Speed[0] > 0.1f)
                    s = true;
                
                foreach(int node in this.Neighbors.Keys)
                {
                    if (global.readers[node].Speed != null && global.readers[node].Speed.Count > 0 && global.readers[node].Speed[0] > 0.1f)
                    {
                        s = true;
                        break;
                    }
                }
                if(s == true)
                    nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval / 4) / 10);
                else
                    nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            }
            else
                nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            Event.AddEvent(new Event(scheduler.currentTime + nextBeacon, EventType.SND_BCN, this, null));
        }



        //这个函数中，允许tag数是最重要的，其次标准是距离
        public List<PingedSWHub> getNearestEntitiesOfMaxTagFromPingedSwHub(Dictionary<string, PingedSWHub> p, int maxcount)
        {
            List<PingedSWHub> list = p.Values.ToList();

            //从list中获得最大的tags列表
            list.Sort(new SortPingedSWHub());
            if (list.Count == 0)
                return list;
            List<PingedSWHub> result = new List<PingedSWHub>();
            int n = 0;
            uint prevtags = 0;
            foreach (PingedSWHub entity in list)
            {
                if (n == maxcount)
                    break;                
                    



                //与前一项一样，则忽略
                if(prevtags != 0 && entity.tags == prevtags)
                    continue;

                result.Add(entity);
                prevtags = entity.tags;
                n++;

            }
            return result;


            /*
            uint tags = 0;
            foreach (int n in p.Keys)
            {
                PingedSWHub h = p[n];
                
                //bool s = (scheduler.currentTime - h.time < global.beaconInterval && scheduler.currentTime > global.beaconWarming)
                //    || (scheduler.currentTime - h.time < global.beaconWarmingInterval && scheduler.currentTime <= global.beaconWarming);
                //if( s || h.hops >global.innerSWTTL)
                 
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
            //从list中找到最近的节点
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
             * */
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

                List<PingedSWHub> pingedSwHubs = data.PingedSwHubs;
                foreach (PingedSWHub hub in pingedSwHubs)
                {
                    int dst = hub.dst;
                    if (this.Id == dst)
                        continue;
                    if (hub.hops > global.outerSWTTL) //太远了也忽略
                        continue;
                    if (!this.cachedPingedSWHubs.ContainsKey(dst))
                        this.cachedPingedSWHubs.Add(dst, new Dictionary<string, PingedSWHub>());

                    string key1 = pkg.Prev + "-" + hub.tags;
                    if (!this.cachedPingedSWHubs[dst].ContainsKey(key1))
                        this.cachedPingedSWHubs[dst].Add(key1, new PingedSWHub(dst, pkg.Prev, hub.hops + 1, hub.tags, scheduler.currentTime, hub.remoteUpdateTime));
                    else
                    {
                        this.cachedPingedSWHubs[dst][key1].hops = hub.hops + 1;
                        this.cachedPingedSWHubs[dst][key1].tags = hub.tags;
                        this.cachedPingedSWHubs[dst][key1].localUpdateTime = scheduler.currentTime;
                        this.cachedPingedSWHubs[dst][key1].remoteUpdateTime = hub.remoteUpdateTime;
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
                //global.currentSWHubNumber < global.swHubRatio * global.readerNum  &&
                 global.currentSWHubNumber < global.maxSwHubs
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
                            foreach(Dictionary<string, PingedSWHub> p in this.cachedPingedSWHubs.Values)
                            {
                                List<string> delKeys = new List<string>();
                                //删除所有下一跳为pkg.Next的项
                                foreach (KeyValuePair<string, PingedSWHub> pair in p)
                                {
                                    PingedSWHub entity=pair.Value;
                                    if (entity.next == pkg.Next)
                                        delKeys.Add(pair.Key);
                                }
                                foreach (string delKey in delKeys)
                                    p.Remove(delKey);
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
                case PacketType.SW_DATA:
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
