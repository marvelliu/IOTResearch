using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    public class SortPingedClusterHead : IComparer<PingedClusterHead>
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
        int IComparer<PingedClusterHead>.Compare(PingedClusterHead x, PingedClusterHead y)
        {
            int t1 = getTagNum(x.tags);
            int t2 = getTagNum(y.tags);
            if (t1 == t2)
                return x.hops - y.hops;
            return t1 - t2;
        }
    }

    [Serializable]
    public class CBRequest
    {
        public int src;
        public int dst;
        public int clusterHops;
        public HashSet<int> visitedHeads;

        public CBRequest(int src, int dst, int hops)
        {
            this.src = src;
            this.dst = dst;
            this.clusterHops = hops;
            this.visitedHeads = new HashSet<int>();
        }

    }


    [Serializable]
    public struct CBReply
    {
        public int dst;
        public int hops;
        public double lastTime;

        public CBReply(int dst, int hops, double lastTime)        
        {
            this.dst = dst;
            this.hops = hops;
            this.lastTime = lastTime;
        }
    }


    [Serializable]
    class CBBeaconData
    {
        public List<ForwardStrategy> ForwardStrategies;
        public List<PingedClusterHead> PingedClusterHeads;
        public bool isClusterHead;
        public CBBeaconData(List<ForwardStrategy> f, bool isClusterHead, List<PingedClusterHead> PingedClusterHeads)
        {
            this.ForwardStrategies = f;
            this.isClusterHead = isClusterHead;
            this.PingedClusterHeads = PingedClusterHeads;
        }
    }


    [Serializable]
    public class PingedClusterHead
    {
        public int headId;
        public int hops;
        public uint tags;
        public int next;
        public double localUpdateTime;
        public double remoteUpdateTime;

        public PingedClusterHead(int headId, int prev, int hops, uint tags, double localUpdateTime, double remoteUpdateTime)
        {
            this.headId = headId;
            this.next = prev;
            this.hops = hops;
            this.tags = tags;
            this.localUpdateTime = localUpdateTime;
            this.remoteUpdateTime = remoteUpdateTime;
        }
    }

    class CBNeighbor : Neighbor
    {
        public ForwardStrategy[] ClaimedForwardStrategy;
        public bool isClusterHead = false;
        public TagEntity availTagEntity = null;

        public CBNeighbor(Reader node, bool isClusterHead)
            : base(node)
        {
            this.ClaimedForwardStrategy = null;
            this.isClusterHead = isClusterHead;
        }

        public CBNeighbor(Reader node)
            : base(node)
        {
            this.ClaimedForwardStrategy = null;
        }
    }

    
    public class PendingCBRequestCacheEntry
    {
        public double firstTime = 0;
        public HashSet<int> prevs;
        public PendingCBRequestCacheEntry()
        {
            this.firstTime = Scheduler.getInstance().currentTime;
            this.prevs = new HashSet<int>();
        }
    }


    class CBReader: HFReader
    {
        new Dictionary<string, RouteEntity> routeTable;
        CBReader clusterHead = null;
        bool isClusterHead = false;
        Dictionary<int, Dictionary<string, PingedClusterHead>> cachedPingedClusterHeads = null;
        
        public Dictionary<string, Dictionary<int, PendingCBRequestCacheEntry>> pendingCBRequests;
        public Dictionary<string, List<PacketCacheEntry>> pendingCBData;

        
        new public static CBReader ProduceReader(int id, int org)
        {
            return new CBReader(id, org);
        }

        public CBReader(int id, int org)
            : base(id, org)
        {
            this.global = (HFGlobal)Global.getInstance();
            this.forwardStrategies = new List<ForwardStrategy>();

            this.routeTable = new Dictionary<string, RouteEntity>();
            this.pendingCBRequests = new Dictionary<string, Dictionary<int, PendingCBRequestCacheEntry>>();
            this.pendingCBData = new Dictionary<string, List<PacketCacheEntry>>();
            this.cachedPingedClusterHeads = new Dictionary<int, Dictionary<string, PingedClusterHead>>();
        }

        public override bool IsHub()
        {
            return this.isClusterHead;
        }

        //这个函数中，允许tag数是最重要的，其次标准是距离
        public List<PingedClusterHead> getNearestEntitiesOfMaxTagFromPingedClusterHead(Dictionary<string, PingedClusterHead> p, int maxcount)
        {
            List<PingedClusterHead> list = p.Values.ToList();

            //从list中获得最大的tags列表
            list.Sort(new SortPingedClusterHead());
            if (list.Count == 0)
                return list;
            List<PingedClusterHead> result = new List<PingedClusterHead>();
            int n = 0;
            uint prevtags = 0;
            foreach (PingedClusterHead entity in list)
            {
                if (n == maxcount)
                    break;

                //与前一项一样，则忽略
                if (prevtags != 0 && entity.tags == prevtags)
                    continue;

                result.Add(entity);
                prevtags = entity.tags;
                n++;

            }
            return result;
        }

        public override void SendBeacon(float time)
        {

            if (this.availTagEntity == null)
                this.availTagEntity = CalculateTagEntity(this.forwardStrategies);

            Packet pkg = new Packet();
            pkg.Type = PacketType.BEACON;
            pkg.Src = pkg.Prev = Id;
            pkg.Dst = pkg.Next = -1;//Broadcast
            pkg.TTL = 1;

            pkg.Beacon = new BeaconField();

            List<PingedClusterHead> p = new List<PingedClusterHead>();
            foreach (int head in this.cachedPingedClusterHeads.Keys)
            {
                if (global.debug)
                {
                    foreach (KeyValuePair<string, PingedClusterHead> pair in this.cachedPingedClusterHeads[head])
                        Console.WriteLine("{0}, hops:{1}, tag:{2}", pair.Key, pair.Value.hops, pair.Value.tags);
                }

                List<PingedClusterHead> nextEntities = getNearestEntitiesOfMaxTagFromPingedClusterHead(this.cachedPingedClusterHeads[head], 3);
                foreach (PingedClusterHead cachedPingedClusterHead in nextEntities)
                {
                    p.Add(new PingedClusterHead(head, this.Id, cachedPingedClusterHead.hops,
                        cachedPingedClusterHead.tags & this.availTagEntity.allowTags, scheduler.currentTime, cachedPingedClusterHead.remoteUpdateTime));
                }
            }

            /*
            foreach (KeyValuePair<int, Neighbor> k in this.Neighbors)
            {
                int nbId = k.Key;
                CBNeighbor nb = (CBNeighbor)k.Value;
                if (minNbId > nbId)
                {
                    minNbId = nbId;
                }
            }*/


            bool isHead = this.isClusterHead;
            CBNeighbor maxNb = null;
            int maxNbTagNum = -1;
            //计算自己允许的tag数，计算一次即可
            if (this.availTagEntity == null)
            {
                this.availTagEntity = CalculateTagEntity(this.forwardStrategies);
            }
            if (!isHead)
            {
                if (scheduler.currentTime > 5)//这个时候大家都已经发出beacon了
                {
                    //如果自己可允许的tag比其他节点多，且大于一个阈值，则将自己作为hub
                    foreach (Neighbor nx in this.Neighbors.Values)
                    {
                        CBNeighbor nb = (CBNeighbor)nx;
                        int nbId = nb.node.Id;
                        if (nb.ClaimedForwardStrategy == null)
                            continue;
                        //去掉超时的邻居
                        if (GetRouteEntityFromRouteTable(nbId, new HashSet<int>()) == null)
                            continue;
                        if (nb.availTagEntity == null)
                            nb.availTagEntity = CalculateTagEntity(nb.ClaimedForwardStrategy);

                        if ((nb.availTagEntity.allowedTagNum > maxNbTagNum)
                            || (nb.availTagEntity.allowedTagNum == maxNbTagNum && nb.isClusterHead))
                        {
                            maxNb = nb;
                            maxNbTagNum = nb.availTagEntity.allowedTagNum;
                        }
                    }
                }


                if (maxNb == null)
                    isHead = false;
                else
                    isHead = (this.availTagEntity.allowedTagNum > maxNbTagNum
                    || (this.availTagEntity.allowedTagNum == maxNbTagNum && !maxNb.isClusterHead));
            }

            //if (this.Id < minNbId)
            if (isHead)
            {
                if (!this.isClusterHead)
                {
                    this.isClusterHead = true;
                    this.clusterHead = this;
                    Console.WriteLine("{0:F4} Reader{1} selected itself as a cluster head", scheduler.currentTime, this.Id);
                }
                p.Add(new PingedClusterHead(this.Id, this.Id, 0, this.availTagEntity.allowTags, scheduler.currentTime, scheduler.currentTime));
                pkg.Data = new CBBeaconData(this.forwardStrategies, true, p);
            }
            else
                pkg.Data = new CBBeaconData(this.forwardStrategies, false, p);

            SendPacketDirectly(time, pkg);

            nextBeacon = 0;
            if (scheduler.currentTime < global.beaconWarming)
                nextBeacon = (float)(Utility.P_Rand(10 * (global.beaconWarmingInterval + 0.4)) / 10);//0.5是为了设定最小值
            //TODO 这里运动节点多的话，可以
            else if (global.smartBeacon == true) //当节点运动时，beacon应频繁些
            {
                bool s = false;
                if (this.Speed != null && this.Speed.Count > 0 && this.Speed[0] > 0.1f)
                    s = true;

                foreach (int node in this.Neighbors.Keys)
                {
                    if (global.readers[node].Speed != null && global.readers[node].Speed.Count > 0 && global.readers[node].Speed[0] > 0.1f)
                    {
                        s = true;
                        break;
                    }
                }
                if (s == true)
                    nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval / 4) / 10);
                else
                    nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            }
            else
                nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            Event.AddEvent(new Event(scheduler.currentTime + nextBeacon, EventType.SND_BCN, this, null));
        }


        public override void RecvBeacon(Packet pkg)
        {
            Scheduler scheduler = Scheduler.getInstance();
            Reader nbNode = global.readers[pkg.Prev];

            if (pkg.Prev == Id && pkg.PrevType == type)
                return;

            CBNeighbor nb = null;
            if (Neighbors.ContainsKey(nbNode.Id))
                nb = (CBNeighbor)Neighbors[nbNode.Id];
            if (nb == null)
            {
                //Add as a neighbor
                AddNeighbor(nbNode);
                nb = (CBNeighbor)Neighbors[nbNode.Id];
            }
            nb.lastBeacon = scheduler.currentTime;

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
                if (nb == null)
                    return;
                CBBeaconData data = (CBBeaconData)pkg.Data;
                List<ForwardStrategy> fs = data.ForwardStrategies;
                //Console.WriteLine("[BEACON] reader{0} adds reader{1} ClaimedForwardStrategy count: {2}", this.Id, node.Id, fs.Count);
                nb.ClaimedForwardStrategy = new ForwardStrategy[fs.Count];
                fs.CopyTo(nb.ClaimedForwardStrategy);
                nb.isClusterHead = data.isClusterHead;

                if (nb.isClusterHead &&
                     (this.clusterHead == null || 
                     (this.clusterHead.Id != nbNode.Id && GetRouteEntityFromRouteTable(this.clusterHead.Id, new HashSet<int>()) == null))
                    )
                {
                    this.clusterHead = (CBReader)nb.node;
                }

                /*
                int nbId = nb.node.Id;
                if (nb.isClusterHead)
                {
                    if (!this.cachedPingedClusterHeads.ContainsKey(nbId))
                        this.cachedPingedClusterHeads.Add(nbId, new Dictionary<string, PingedClusterHead>());
                    uint tags = this
                    string key1 = pkg.Prev + "-" + head.tags;
                    if(this.cachedPingedClusterHeads[nbId].ContainsKey(key1))
                        this.cachedPingedClusterHeads[nbId][key1] = new PingedClusterHead(
                }*/


                List<PingedClusterHead> pingedClusterHeads = data.PingedClusterHeads;
                foreach (PingedClusterHead head in pingedClusterHeads)
                {
                    if (head.hops > global.clusterRadius) //太远了也忽略
                        continue;
                    if (head.headId == this.Id)
                        continue;

                    if (!this.cachedPingedClusterHeads.ContainsKey(head.headId))
                        this.cachedPingedClusterHeads.Add(head.headId, new Dictionary<string, PingedClusterHead>());

                    string key1 = pkg.Prev + "-" + head.tags;
                    
                    if (!this.cachedPingedClusterHeads[head.headId].ContainsKey(key1))
                        this.cachedPingedClusterHeads[head.headId].Add(key1, new PingedClusterHead(
                            head.headId, pkg.Prev, head.hops + 1, head.tags, scheduler.currentTime, head.remoteUpdateTime));
                    else if (scheduler.currentTime-this.cachedPingedClusterHeads[head.headId][key1].localUpdateTime>0.5
                        || head.hops + 1<this.cachedPingedClusterHeads[head.headId][key1].hops)
                    {
                        this.cachedPingedClusterHeads[head.headId][key1].hops = head.hops + 1;
                        this.cachedPingedClusterHeads[head.headId][key1].tags = head.tags;
                        this.cachedPingedClusterHeads[head.headId][key1].localUpdateTime = scheduler.currentTime;
                        this.cachedPingedClusterHeads[head.headId][key1].remoteUpdateTime = head.remoteUpdateTime;
                    }
                }
            }
        }
        

        private bool CheckTags(Packet pkg)
        {
            uint tags = pkg.Tags;
            foreach (ForwardStrategy f in forwardStrategies)//one found.
            {
                if (f.Action == ForwardStrategyAction.REFUSE && (tags & f.Tags) != 0)
                {
                    Console.WriteLine("{0:F4} [{1}] {2}{3} drop from {4}{5} due to FORWARD_STRATEGIES", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    return false;
                }
            }
            return true;
        }


        public override Neighbor AddNeighbor(Reader nb)
        {
            if (!this.Neighbors.ContainsKey(nb.Id))
                this.Neighbors.Add(nb.Id, new CBNeighbor(nb));
            if (!this.routeTable.ContainsKey(nb.Id.ToString()))
                this.routeTable.Add(nb.Id.ToString(), new RouteEntity(nb.Id, nb.Id, 1, scheduler.currentTime, scheduler.currentTime));
            return this.Neighbors[nb.Id];
        }


        public PingedClusterHead GetAllowedRouteEntity(Dictionary<string, PingedClusterHead> p, uint tags)
        {
            foreach (string key in p.Keys)
            {
                PingedClusterHead h = p[key];
                if (!this.Neighbors.ContainsKey(h.next))
                    continue;
                if (IsAllowedTags(h.tags, tags)
                    && IsFreshRecord(h.hops, h.remoteUpdateTime, global.beaconInterval, global.beaconInterval * h.hops)
                    && IsFreshRecord(1, this.Neighbors[h.next].lastBeacon, global.beaconInterval, Math.Min(4, global.beaconInterval))
                    )
                    return h;
            }
            return null;
        }

        public override RouteEntity GetRouteEntityFromRouteTable(int dst, HashSet<int> exclusiveSet)
        {
            string key = dst.ToString();

            if (routeTable.ContainsKey(key) && !exclusiveSet.Contains(routeTable[key].next) && (
                        scheduler.currentTime - routeTable[key].localLastUpdatedTime < Math.Min(0.5, global.beaconInterval)
                        || IsFreshRecord(routeTable[key].hops, routeTable[key].remoteLastUpdatedTime)
                        ))
                return routeTable[key];
            else
                return null;
        }


        public RouteEntity GetRouteEntityFromRouteTable(int dst, uint tags, HashSet<int> exclusiveSet)
        {
            string key = dst + "-" + tags;

            if (routeTable.ContainsKey(key) && !exclusiveSet.Contains(routeTable[key].next) && (
                        scheduler.currentTime - routeTable[key].localLastUpdatedTime < Math.Min(0.5, global.beaconInterval)
                        || IsFreshRecord(routeTable[key].hops, routeTable[key].remoteLastUpdatedTime)
                        ))
                return routeTable[key];
            else if (this.Neighbors.ContainsKey(dst) && routeTable.ContainsKey(dst.ToString()))
                return routeTable[dst.ToString()];
                return null;
        }

        
        public void AddPendingCBData(Packet pkg)
        {
            int dst = pkg.Dst;
            uint tags = pkg.Tags;
            string key = dst + "-" + tags;
            if (!this.pendingCBData.ContainsKey(key))
            {
                this.pendingCBData.Add(key, new List<PacketCacheEntry>());
            }
            foreach (PacketCacheEntry e in this.pendingCBData[key])
            {
                if (e.pkg == pkg)
                    return;
            }
            this.pendingCBData[key].Add(new PacketCacheEntry(pkg, scheduler.currentTime));
        }

        
        protected void AddPendingCBRequest(int src, int prev, int dst, bool updateFirstTime, uint tags)
        {
            string key = dst + "-" + tags;
            if (!this.pendingCBRequests.ContainsKey(key))
            {
                this.pendingCBRequests.Add(key, new Dictionary<int, PendingCBRequestCacheEntry>());
            }

            if (!this.pendingCBRequests[key].ContainsKey(src))
                this.pendingCBRequests[key].Add(src, new PendingCBRequestCacheEntry());
            else if (updateFirstTime == true)
                this.pendingCBRequests[key][src].firstTime = scheduler.currentTime;

            if (!this.pendingCBRequests[key][src].prevs.Contains(prev))
                this.pendingCBRequests[key][src].prevs.Add(prev);
        }
        

        public override bool SendData(Packet pkg)
        {
            return RoutePacket(pkg);            
        }

        
        //RoutePacket只是路由普通的数据包，并非专门发往swhub的
        public override bool RoutePacket(Packet pkg)
        {
            //Console.WriteLine("packetSeq:{0}", this.packetSeq);
            if (global.routeMethod != RouteMethod.CBRP)
            {
                throw new Exception("Unknown RouteMethod " + global.routeMethod);
            }

            if (this.Id != pkg.Src && CheckTags(pkg) == false && pkg.Dst != this.Id)
            {
                Console.WriteLine("reader{0} rejects tag{1} from reader{2}", this.Id, pkg.Tags, pkg.Prev);
                return false;
            }
            // to itself
            if (pkg.Dst == Id && pkg.DstType == NodeType.READER && pkg.Type == PacketType.DATA)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3}->{4}{5}, total: {6}, pkgId: {7}", scheduler.currentTime, "RECV_DATA", pkg.SrcType, pkg.Src, this.type, this.Id, scheduler.currentTime - pkg.beginSentTime, pkg.getId());
                //
                //SendPacketDirectly(scheduler.currentTime, pkg);
                return true;
            }
            if (pkg.Prev != Id && !Neighbors.ContainsKey(pkg.Prev))//not itself
                return false;

            if (pkg.SrcSenderSeq < 0)//未定该数据包的id
                InitPacketSeq(pkg);

            string pkgId = pkg.getId();
            if (global.debug)
                Console.WriteLine("debug RoutePacket pkgId:{0}", pkgId);
            if (!this.receivedPackets.Contains(pkgId))
                this.receivedPackets.Add(pkgId);
            else
                return true;


            RouteEntity entity = GetRouteEntityFromRouteTable(pkg.Dst, pkg.Tags, new HashSet<int>() { pkg.Prev, pkg.Src});
            if (entity != null)//有路由项
            {
                Packet pkg1 = pkg.Clone() as Packet;
                pkg1.Prev = Id;
                pkg1.Next = entity.next;
                pkg1.PrevType = pkg.NextType = NodeType.READER;
                pkg1.TTL = Math.Max(entity.hops + 1, pkg.TTL);
                SendPacketDirectly(scheduler.currentTime, pkg1);
                return true;
            }
            else //发送源路由请求
            {
                //先缓存
                AddPendingCBData(pkg);
                SendCBRequest(pkg, this.isClusterHead, global.clusterHops);
                return true;
            }
        }

        public void SendCBRequest(Packet pkg, bool isHead, int reqHops)
        {
            if (isHead)
            {
                foreach (int nbHead in this.cachedPingedClusterHeads.Keys)
                {
                    Dictionary<string, PingedClusterHead> p = this.cachedPingedClusterHeads[nbHead];

                    PingedClusterHead h = GetAllowedRouteEntity(p, pkg.Tags);
                    if (h == null)
                        continue;
                    CBRequest req = null;
                    if (pkg.Data != null)
                    {
                        req = (CBRequest)pkg.Data;
                        if (req.visitedHeads.Contains(nbHead))
                            continue;
                    }


                    Reader dst = global.readers[nbHead];

                    Packet pkg1 = new Packet(this, dst, PacketType.CB_REQUEST);
                    CBRequest req1 = new CBRequest(this.Id, pkg.Dst, reqHops);
                    if (req != null)
                    {
                        Utility.CopyHashSet(req1.visitedHeads, req.visitedHeads);
                        req1.visitedHeads.Add(this.Id);
                    }
                    pkg1.Data = req1;
                    pkg1.Next = h.next;
                    pkg1.TTL = global.CBTTL;
                    pkg1.Tags = pkg.Tags;
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                }
            }
            else
            {
                Reader dst = this.clusterHead;
                Packet pkg1 = new Packet(this, dst, PacketType.CB_REQUEST);
                pkg1.TTL = global.CBTTL;
                pkg1.Tags = pkg.Tags;
                pkg1.Data = new CBRequest(this.Id, pkg.Dst, reqHops);
                SendPacketDirectly(scheduler.currentTime, pkg1);
            }
        }


        public void RecvCBRequest(Packet pkg)
        {

            if (CheckTags(pkg) == false)
            {
                Console.WriteLine("reader{0} rejects tag{1} from reader{2}", this.Id, pkg.Tags, pkg.Prev);
                return;
            }
            //如果目的地不是本节点，则无需转发。
            if (pkg.Next != this.Id)
                return;

            CBRequest req = (CBRequest)pkg.Data;
            int src = req.src;
            int dst = req.dst;
            uint tags = pkg.Tags;
            int clusterHops = req.clusterHops;
            if (clusterHops < 0)
                return;

            string key = pkg.Src + "-" + tags;
            if (!this.routeTable.ContainsKey(key))
                this.routeTable.Add(key, new RouteEntity(pkg.Src, pkg.Prev, global.CBTTL - pkg.TTL, scheduler.currentTime, scheduler.currentTime));
            else
            {
                this.routeTable[key].hops = global.CBTTL - pkg.TTL;
                this.routeTable[key].next = pkg.Prev;
                this.routeTable[key].remoteLastUpdatedTime = scheduler.currentTime;
                this.routeTable[key].localLastUpdatedTime = scheduler.currentTime;
            }
            

            Reader srcNode = global.readers[req.src];
            if (src == this.Id)
                return;

            if (!Neighbors.ContainsKey(pkg.Prev))
                return;

            //中间节点
            if (pkg.Dst != this.Id)
            {
                int next = -1;
                RouteEntity e = GetRouteEntityFromRouteTable(pkg.Dst, pkg.Tags, new HashSet<int>() { pkg.Prev, pkg.Src });
                if (e == null && this.cachedPingedClusterHeads.ContainsKey(pkg.Dst))
                {
                    Dictionary<string, PingedClusterHead> p = this.cachedPingedClusterHeads[pkg.Dst];
                    //这里就不强制找head了
                    PingedClusterHead e1 = GetNextNodeOfLestHopsFromClusterHeads(p, pkg.Tags, false);
                    if(e1 != null)
                        next = e1.next;
                }
                else
                    next = e.next;

                if (next != -1)
                {
                    Packet pkg1 = pkg.Clone() as Packet;
                    pkg1.Next = next;
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                }
                else
                    Console.WriteLine("No route to dest {0} with tags {1}", pkg.Dst, pkg.Tags);
                return;
            }

            key = dst + "-" + tags;
            if (pendingCBRequests.ContainsKey(key))
            {
                foreach (PendingCBRequestCacheEntry ce in pendingCBRequests[key].Values)
                {
                    if (scheduler.currentTime - ce.firstTime < 1.5)
                        return;
                }
            }
            AddPendingCBRequest(src, pkg.Prev, dst, true, tags);


            //CBRequest是查找到目的地的请求
            //先找目的地是否在路由表中
            RouteEntity entity = GetRouteEntityFromRouteTable(dst, tags, new HashSet<int>() { pkg.Prev, pkg.Src });
            if(entity != null)
            {
                RouteEntity entity1 = GetRouteEntityFromRouteTable(srcNode.Id, tags, new HashSet<int>());
                SendCBReply(dst, srcNode, entity1.hops, entity1.next, Math.Max(3, entity.hops+1), entity.remoteLastUpdatedTime, tags);
                return;
            }


            //如果自己也没有到目的地的路由项，则转发请求

            if (this.isClusterHead)
            {
                //如果是簇首，则转发给周围的簇首
                foreach (int nbHead in this.cachedPingedClusterHeads.Keys)
                {
                    if (req.visitedHeads.Contains(nbHead))
                        continue;

                    Dictionary<string, PingedClusterHead> p = this.cachedPingedClusterHeads[nbHead];
                    PingedClusterHead h = GetAllowedRouteEntity(p, pkg.Tags);
                    if (h == null)
                        continue;
                    if (req.clusterHops <= 0)
                        return;
                    Reader next = global.readers[h.next];
                    Reader dest = global.readers[nbHead];
                    Packet pkg1 = new Packet(this, dest, PacketType.CB_REQUEST);
                    pkg1.Next = h.next;
                    pkg1.Tags = tags;
                    CBRequest req1 = new CBRequest(this.Id, req.dst, req.clusterHops - 1);
                    if (req != null)
                    {
                        Utility.CopyHashSet(req1.visitedHeads, req.visitedHeads);
                        req1.visitedHeads.Add(this.Id);
                    }
                    pkg1.Data = req1;
                    pkg1.TTL = global.CBTTL;
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                }
            }
            else //普通节点，直接转发给目的簇head
            {
                entity = GetRouteEntityFromRouteTable(pkg.Dst, pkg.Tags, new HashSet<int>() { pkg.Prev, pkg.Src });
                if (entity == null)
                {
                    Console.WriteLine("No route to {0} by tags {1}", pkg.Dst, pkg.Tags);
                    return;
                }
                else
                {
                    Packet pkg1 = pkg.Clone() as Packet;
                    pkg1.Prev = this.Id;
                    pkg1.Next = entity.next;
                    pkg1.TTL = pkg1.TTL - 1;
                    pkg1.Tags = tags;
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                    return;
                }
            }
        }
        public virtual void SendCBData(Packet pkg)
        {
            int dst = pkg.Dst;
            uint tags = pkg.Tags;
            Reader node = global.readers[pkg.Prev];
            //Check Route Table

            RouteEntity entity = GetRouteEntityFromRouteTable(dst, tags, new HashSet<int>() { pkg.Prev, pkg.Src });
            if (entity != null)
            {
                //Console.WriteLine("{0}-{1}", entity.hops, entity.time);
                Packet pkg1 = pkg.Clone() as Packet;
                pkg1.Prev = Id;
                pkg1.Next = entity.next;
                pkg1.PrevType = pkg.NextType = NodeType.READER;
                pkg1.TTL = Math.Max(entity.hops + 1, pkg.TTL);
                SendPacketDirectly(scheduler.currentTime, pkg1);
                return;
            }
            //Not found...

            Console.WriteLine("{0:F4} [{1}] {2}{3} tries to send {4}{5} but no route", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.DstType, pkg.Dst);
            SendCBRequest(pkg, this.isClusterHead, global.clusterHops);
            AddPendingCBData(pkg);
       }

        public void RecvCBReply(Packet pkg)
        {
            CBReader.CalculateNB(pkg.Tags);     
            Reader prevNode = global.readers[pkg.Prev];

            if (!Neighbors.ContainsKey(prevNode.Id))
                return;


            CBReply reply = (CBReply)pkg.Data;
            uint tags = pkg.Tags;
            int dst = reply.dst;
            string key = dst + "-" + tags;
            
            if (!routeTable.ContainsKey(key))
            {
                routeTable.Add(key, new RouteEntity(reply.dst, prevNode.Id, reply.hops + 1, reply.lastTime, scheduler.currentTime));
            }
            else
            {
                RouteEntity entity = (RouteEntity)routeTable[key];
                if (reply.hops < entity.hops || reply.lastTime - entity.remoteLastUpdatedTime > 1)
                {
                    entity.hops = reply.hops + 1;
                    entity.next = prevNode.Id;
                    entity.remoteLastUpdatedTime = reply.lastTime;
                    entity.localLastUpdatedTime = scheduler.currentTime;
                }
            }

            //中间节点
            if (pkg.Dst != this.Id)
            {
                int next = -1;
                RouteEntity e = GetRouteEntityFromRouteTable(pkg.Dst, pkg.Tags, new HashSet<int>() { pkg.Prev, pkg.Src });
                if (e == null && this.cachedPingedClusterHeads.ContainsKey(pkg.Dst))
                {
                    Dictionary<string, PingedClusterHead> p = this.cachedPingedClusterHeads[pkg.Dst];
                    //这里就不强制找head了
                    PingedClusterHead e1 = GetNextNodeOfLestHopsFromClusterHeads(p, pkg.Tags, false);
                    if (e1 != null)
                        next = e1.next;
                }
                else
                    next = e.next;

                if (next != -1)
                {
                    Packet pkg1 = pkg.Clone() as Packet;
                    pkg1.Next = next;
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                }
                else
                    Console.WriteLine("No route to dest {0} with tags {1}", pkg.Dst, pkg.Tags);
            }

            if (this.pendingCBRequests.ContainsKey(key))
            {
                foreach (int src in pendingCBRequests[key].Keys)
                {
                    HashSet<int> prevs = (HashSet<int>)pendingCBRequests[key][src].prevs;
                    RouteEntity entity1 = GetRouteEntityFromRouteTable(src, tags, new HashSet<int>());
                    SendCBReply(reply.dst, global.readers[src], Math.Max(3, entity1.hops), entity1.next, reply.hops + 1, reply.lastTime, pkg.Tags);
                    prevs.Clear();
                }
                pendingCBRequests.Remove(key);
            }
            //Send pending datas...
            if (this.pendingCBData.ContainsKey(key))
            {
                List<PacketCacheEntry> entries = (List<PacketCacheEntry>)pendingCBData[key];
                foreach (PacketCacheEntry entry in entries)
                {
                    Packet pkg1 = entry.pkg;
                    if (routeTable.ContainsKey(key))
                        pkg1.TTL = Math.Max(pkg1.TTL, routeTable[key].hops + 1);
                    SendCBData(pkg1);
                }
                pendingCBData.Remove(key);
            }
        }

        public void SendCBReply(int finalDst, Reader prevHeadNode, int prevHeadHops, int prevHeadNext, int hops, double lastTime, uint tags)
        {
            Packet pkg = new Packet(this, prevHeadNode, PacketType.CB_REPLY);
            pkg.TTL = prevHeadHops;
            pkg.Next = prevHeadNext;
            pkg.Data = new CBReply(finalDst, hops, lastTime);
            pkg.Tags = tags;
            SendPacketDirectly(scheduler.currentTime, pkg);
        }

        PingedClusterHead GetNextNodeOfLestHopsFromClusterHeads(Dictionary<string, PingedClusterHead> p, uint tags, bool aggressivelyLookForSwHub)
        {
            int hx = 999;
            int next = -1;
            PingedClusterHead minh = null;
            foreach (string key in p.Keys)
            {
                PingedClusterHead h = p[key];

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
                PingedClusterHead h = p[key];

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


        static public void CalculateNB(uint tags)
        {
            Global global = Global.getInstance();
            float total = 0;
            float count = 0;
            for (int i = 0; i < global.readers.Length; i++)
            {
                CBReader r = (CBReader)(global.readers[i]) ;
                if(r.isClusterHead == false || !r.IsAllowedTags(tags))
                    continue;
                total += r.cachedPingedClusterHeads.Count;
                count ++;
            }
            float nb = total / count;
            Console.WriteLine("count:{0}", nb);
        }

        public override void ProcessPacket(Packet pkg)
        {
            //I send the packet myself, ignore
            if (pkg.Prev == Id && pkg.PrevType == type)
            {
                return;
            }
            //if(pkg.Type == PacketType.DATA)
            //    CalculateNB();

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
                case PacketType.CB_REQUEST:
                    RecvCBRequest(pkg);
                    break;
                case PacketType.CB_REPLY:
                    RecvCBReply(pkg);
                    break;
                //Some codes are hided in the base class.
                default:
                    base.ProcessPacket(pkg);
                    return;
            }
        }
    }
}
