using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Diagnostics;

namespace AdHocBaseApp
{
    public enum NodeType
    {
        OBJECT = 1,
        READER,
        QUERIER,
        SERVER,
        CA,
        ORG,
        TRUST_MANAGER
    }


    public class ObjectEntity
    {
        public int tagId;
        public int orgId;
        public float time;
        public int key;
        public ObjectEntity(int tagId, int orgId, float time)
        {
            this.tagId = tagId;
            this.orgId = orgId;
            this.time = time;
        }
    }


    [Serializable]
    public struct AODVReply
    {
        public int dst;
        public int hops;
        public double lastTime;

        public AODVReply(int dst, int hops, double lastTime)
        {
            this.dst = dst;
            this.hops = hops;
            this.lastTime = lastTime;
        }
    }

    public class PendingAODVRequestCacheEntry
    {
        public double firstTime = 0;
        public HashSet<int> prevs;
        public PendingAODVRequestCacheEntry()
        {
            this.firstTime = Scheduler.getInstance().currentTime;
            this.prevs = new HashSet<int>();
        }
    }

    public class PacketCacheEntry
    {
        public Packet pkg;
        public float firstTime;
        public PacketCacheEntry(Packet pkg, float firstTime)
        {
            this.pkg = pkg;
            this.firstTime = firstTime;
        }
    }

    public delegate Reader ReaderConstructor(int id, int org);


    public class Reader : MobileNode
    {
        public int OrgId;
        public bool IsGateway;

        public bool LandmarkReader;
        public bool SendBeaconFlag;
        public bool Transmitter;

        public Dictionary<int, RouteEntity> routeTable;
        public Dictionary<int, Dictionary<int, PendingAODVRequestCacheEntry>> pendingAODVRequests;
        public Dictionary<int, List<PacketCacheEntry>> pendingAODVData;
        public Dictionary<int, Neighbor> Neighbors;
        public Dictionary<int, GatewayEntity> gatewayEntities;
        public Dictionary<int, ObjectEntity> NearbyObjectCache;
        public HashSet<string> receivedPackets;
        private Global global;

        protected bool retryOnSendingFailture = false;

        public int packetCounter = 0;//计数当前节点发送的数据包，用于计算csma/ca的

        public static Reader ProduceReader(int id, int org)
        {
            return new Reader(id, org);
        }

        public Reader(int id, int org)
            : base(id)
        {
            this.global = Global.getInstance();
            this.OrgId = org;
            this.SendBeaconFlag = true;
            this.type = NodeType.READER;
            this.Neighbors = new Dictionary<int, Neighbor>();
            this.routeTable = new Dictionary<int, RouteEntity>();
            this.pendingAODVRequests = new Dictionary<int, Dictionary<int, PendingAODVRequestCacheEntry>>();
            this.pendingAODVData = new Dictionary<int, List<PacketCacheEntry>>();
            this.NearbyObjectCache = new Dictionary<int, ObjectEntity>();
            this.gatewayEntities = new Dictionary<int, GatewayEntity>();
            this.IsGateway = false;
            this.LandmarkReader = false;
            this.receivedPackets = new HashSet<string>();

            Event.AddEvent(new Event(global.startTime + global.checkNearObjInterval,
                EventType.CHK_NEAR_OBJ, this, null));
            Event.AddEvent(new Event(global.startTime + global.checkNeighborInterval,
                EventType.CHK_NB, this, null));
            Event.AddEvent(new Event(global.startTime + global.checkPendingPacketInterval,
                EventType.CHK_PEND_PKT, this, null));
        }



        public bool HasNeighbor(int id)
        {
            if (Neighbors.ContainsKey(id))
                return true;
            else
                return false;
        }

        public void SetAsGateway()
        {
            this.IsGateway = true;
            if (!this.gatewayEntities.ContainsKey(Id))
                this.gatewayEntities.Add(Id, new GatewayEntity(Id, Id, 0));
        }

        public virtual Neighbor AddNeighbor(Reader nb)
        {
            if (!this.Neighbors.ContainsKey(nb.Id))
                this.Neighbors.Add(nb.Id, new Neighbor(nb));
            if (!this.routeTable.ContainsKey(nb.Id))
                this.routeTable.Add(nb.Id, new RouteEntity(nb.Id, nb.Id, 1, scheduler.currentTime, scheduler.currentTime));
            return this.Neighbors[nb.Id];
        }

        public void CheckPacketCount(Packet pkg)
        {
            //decrease the packet counter
            if (pkg.PrevType == NodeType.READER)
            {
                if (pkg.Next == Node.BroadcastNode.Id)
                {
                    if (pkg.DelPacketNode == Id)
                    {
                        global.readers[pkg.Prev].packetCounter--;
                        //Console.WriteLine("-packet count: {0}->{1} {2}_{3}", pkg.Prev, pkg.Next, global.readers[pkg.Prev].packetCounter, pkg.PacketSeq);
                    }
                }
                else if (pkg.Next == Id)
                {
                    global.readers[pkg.Prev].packetCounter--;
                    //Console.WriteLine("-packet count: {0}->{1} {2}_{3}", pkg.Prev, pkg.Next, global.readers[pkg.Prev].packetCounter, pkg.PacketSeq);
                }
                if (global.readers[pkg.Prev].packetCounter < 0)
                    throw new Exception("currentPackets <0");
            }
        }



        public void SendPacket(float time, Packet pkg)
        {
            Event.AddEvent(
                        new Event(time, EventType.RECV, this, pkg));
        }

        public void initPacketSeq(Packet pkg)
        {
            if (pkg.seqInited == false)
            {
                this.packetSeq++;
                pkg.PrevSenderSeq = this.packetSeq;
                if (this.Id == pkg.Src)
                    pkg.SrcSenderSeq = this.packetSeq;
                pkg.seqInited = true;
            }
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
                Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}({6}->{7})", time, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()), pkg.Src, pkg.Dst);

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
                    if (pkg1.seqInited == false)
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
                            this.routeTable.Remove(pkg.Dst);
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
                            if (pkg1.seqInited == false)
                            {
                                pkg1.PrevSenderSeq = this.packetSeq;
                                if (pkg.Src == Id)
                                    pkg1.SrcSenderSeq = pkg1.PrevSenderSeq;
                            }
                            //Console.WriteLine("[DEBUG] recv reader{0}-{1}", list[i].id, pkg1.PacketSeq);

                            recv_time += (float)(Utility.Distance(this, (MobileNode)list[i]) / global.lightSpeed);

                            /*
                            if (list[i].id == pkg.Next && fail == false)
                            {
                                PointShape nextPoint1 = scheduler.NextReaderPosition(nextNode, recv_time);
                                PointShape nextPoint2 = scheduler.NextReaderPosition(this, recv_time);
                                if (Utility.Distance(nextPoint1.x, nextPoint1.y, nextPoint2.x, nextPoint2.y) > global.nodeMaxDist)
                                {
                                    Console.WriteLine("{0:F4} [{1}] {2}{3} Drop data of {4}{5} due to sending failture.", scheduler.CurrentTime, pkg.Type, this.type, this.id, pkg.NextType, pkg.Next);
                                    this.Neighbors.Remove(pkg.Next);
                                    this.routeTable.Remove(pkg.Dst);
                                    if (retryOnSendingFailture == true || (pkg.Type != PacketType.BEACON && pkg.Type != PacketType.AODV_REPLY && pkg.Type != PacketType.AODV_REQUEST))
                                    {
                                        Event.AddEvent(new Event(scheduler.CurrentTime + 0.2f, EventType.SND_DATA, this, pkg));
                                        Console.WriteLine("retry");
                                        return;
                                    }
                            
                                }
                            }
                             * */

                            Event.AddEvent(
                                new Event(time + recv_time, EventType.RECV,
                                    list[i], pkg1));
                        }
                        break;
                    case NodeType.SERVER:
                        recv_time = global.processDelay + global.internetDelay;
                        Event.AddEvent(
                            new Event(time + recv_time, EventType.RECV,
                                global.server, pkg));
                        break;
                    case NodeType.ORG:
                        recv_time = global.processDelay + global.internetDelay;
                        Event.AddEvent(
                            new Event(time + recv_time, EventType.RECV,
                                global.orgs[pkg.Next], pkg));
                        break;
                    case NodeType.QUERIER:
                        recv_time = global.processDelay + global.internetDelay;
                        Event.AddEvent(
                            new Event(time + recv_time, EventType.RECV,
                                global.queriers[pkg.Next], pkg));
                        break;
                    case NodeType.OBJECT:
                        Node node = global.objects[pkg.Next];
                        recv_time = global.processDelay + (float)(Utility.Distance(this, (MobileNode)node) / global.lightSpeed);
                        Event.AddEvent(
                            new Event(time + recv_time, EventType.RECV,
                                node, pkg));
                        break;
                    default:
                        Console.WriteLine("Error Type!");
                        break;
                }
            }
        }


        virtual public void SendBeacon(float time)
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
            SendPacketDirectly(time, pkg);
            float nextBeacon = 0;
            if (scheduler.currentTime < global.beaconWarming)
                nextBeacon = (float)(Utility.P_Rand(10 * (global.beaconWarmingInterval + 0.4)) / 10);//0.5是为了设定最小值
            else if (this.Speed[0] > 1f) //当节点运动时，beacon应频繁些
                nextBeacon = (float)(Utility.P_Rand(4 * global.beaconInterval) / 4);
            else
                nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            Event.AddEvent(new Event(scheduler.currentTime + nextBeacon, EventType.SND_BCN, this, null));
        }

        public bool IsFreshRecord(int hops, double time)
        {
            return ((hops >1 && scheduler.currentTime - time < Math.Min(5, global.beaconInterval))
                || (hops == 1 && scheduler.currentTime - time < global.beaconInterval / 2));
        }

        public bool IsFreshRecord2(int hops, double time)
        {
            return ((hops > 1 && scheduler.currentTime - time < Math.Min(4, global.beaconInterval))
                || (hops == 1 && scheduler.currentTime - time < global.beaconInterval / 2));
        }

        public bool IsFreshRecord(int hops, double time, double oneHopTime, double multiHopTime)
        {
            return ((hops > 1 && scheduler.currentTime - time < multiHopTime)
                || (hops == 1 && scheduler.currentTime - time < oneHopTime));
        }

        virtual public void RecvBeacon(Packet pkg)
        {
            Scheduler scheduler = Scheduler.getInstance();
            Reader node = global.readers[pkg.Prev];

            if (pkg.Prev == Id && pkg.PrevType == type)
                return;

            Neighbor nb = null;
            if (Neighbors.ContainsKey(node.Id))
                nb = (Neighbor)Neighbors[node.Id];
            if (nb != null)
            {
                nb.lastBeacon = scheduler.currentTime;
            }
            else
            {
                //Add as a neighbor
                AddNeighbor(node);
            }

            if (!this.routeTable.ContainsKey(pkg.Prev))
                this.routeTable.Add(pkg.Prev, new RouteEntity(pkg.Prev, pkg.Prev, 1, scheduler.currentTime, scheduler.currentTime));
            else
            {
                this.routeTable[pkg.Prev].hops = 1;
                this.routeTable[pkg.Prev].next = pkg.Prev;
                this.routeTable[pkg.Prev].remoteLastUpdatedTime = scheduler.currentTime;
                this.routeTable[pkg.Prev].localLastUpdatedTime = scheduler.currentTime;                
            }


            if (pkg.Beacon != null)
            {
                if (pkg.Beacon.gatewayEntities != null)
                {
                    for (int i = 0; i < pkg.Beacon.gatewayEntities.Length; i++)
                    {
                        GatewayEntity g = pkg.Beacon.gatewayEntities[i];
                        if (!this.gatewayEntities.ContainsKey(g.gateway))
                        {
                            this.gatewayEntities.Add(g.gateway, new GatewayEntity(g.gateway, g.next, g.hops + 1));
                            Console.WriteLine("{0:F4} [{1}] {2}{3} add a gateway of {4} hops {5}.", scheduler.currentTime, pkg.Type, this.type, this.Id, g.gateway, g.hops);
                        }
                        else if (this.gatewayEntities[g.gateway].hops > g.hops + 1)
                        {
                            this.gatewayEntities[g.gateway].hops = g.hops + 1;
                            this.gatewayEntities[g.gateway].next = g.next;
                            Console.WriteLine("{0:F4} [{1}] {2}{3} update a gateway of {4} hops {5}.", scheduler.currentTime, pkg.Type, this.type, this.Id, g.gateway, g.hops);
                        }
                        else if (this.gatewayEntities[g.gateway].next == g.next)//update in case of the next hop moves
                            this.gatewayEntities[g.gateway].hops = g.hops + 1;
                    }
                }
            }
        }

        public virtual void CheckPendingPackets()
        {
            foreach (int dst in this.pendingAODVData.Keys)
            {
                List<PacketCacheEntry> entries = this.pendingAODVData[dst];
                List<PacketCacheEntry> temp = new List<PacketCacheEntry>();
                //删除过期数据包
                foreach (PacketCacheEntry entry in entries)
                {
                    if (scheduler.currentTime - entry.firstTime > global.checkPendingPacketInterval)
                        temp.Add(entry);
                }
                foreach (PacketCacheEntry entry in temp)
                    entries.Remove(entry);
            }
        }

        public virtual void CheckNeighbors()
        {
            List<int> temp = new List<int>();
            foreach (Neighbor nb in Neighbors.Values)
            {
                if (scheduler.currentTime - nb.lastBeacon > global.checkNeighborInterval)
                {
                    temp.Add(nb.node.Id);
                }
            }
            foreach (int t in temp)
            {
                Neighbors.Remove(t);
                routeTable.Remove(t);
                //Console.WriteLine("Node " + id + " remove neighbor " + t);
            }
        }

        public virtual void CheckNearObjects()
        {
            List<int> temp = new List<int>();
            foreach (int k in this.NearbyObjectCache.Keys)
            {
                ObjectEntity e = this.NearbyObjectCache[k];
                if (scheduler.currentTime - e.time > global.checkNearObjInterval)
                {
                    temp.Add(k);
                }
            }
            foreach (int k in temp)
            {
                this.NearbyObjectCache.Remove(k);
                //Console.WriteLine("Node " + id + " remove obj " + o);
            }
            Event.AddEvent(new Event(scheduler.currentTime + global.checkNearObjInterval, EventType.CHK_NEAR_OBJ, this, null));
        }

        public virtual bool RoutePacket(Packet pkg)
        {
            Node node;
            int dst = pkg.Dst;

            string pkgId = pkg.getId();
            if (global.debug)
                Console.WriteLine("pkgId:{0}", pkgId);
            if (!this.receivedPackets.Contains(pkgId))
                this.receivedPackets.Add(pkgId);
            else
                return true;
            if (pkg.PrevType == NodeType.READER)
            {
                if (pkg.Prev != Id)//itself
                {
                    node = global.readers[pkg.Prev];
                    if (!Neighbors.ContainsKey(node.Id))
                        return false;
                }
            }
            else if (pkg.PrevType == NodeType.OBJECT)//from the object. 
            {
                node = global.objects[pkg.Prev];
                if (!NearbyObjectCache.ContainsKey(node.Id))
                    return false;
            }
            else if (this.IsGateway == false)//Server, Querier, Org or CA
            {
                return false;
            }


            // to itself
            if (pkg.Dst == Id && pkg.DstType == NodeType.READER)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3}->{4}{5}, total: {6}", scheduler.currentTime, "RECV_DATA", pkg.SrcType, pkg.Src, this.type, this.Id, scheduler.currentTime - pkg.beginSentTime);
                return true;
            }

            if (this.IsGateway == false)//the node is not a gateway
            {
                //to server, then get the nearest gateway.
                if (pkg.DstType == NodeType.SERVER || pkg.DstType == NodeType.ORG)
                {
                    if (this.gatewayEntities.Count == 0)
                    {
                        Console.WriteLine("{0:F4} [{1}] {2}{3} Drop due to no gateway", scheduler.currentTime, pkg.Type, this.type, this.Id);
                        return false;
                    }
                    GatewayEntity gateway = FindNearestGateway();

                    //gateway cache? save time
                    if (global.CheckGatewayCache && gateway != null)
                    {
                        pkg.Next = gateway.next;
                        pkg.NextType = NodeType.READER;
                        SendPacketDirectly(scheduler.currentTime, pkg);
                        return true;
                    }
                    dst = gateway.gateway;
                }

            }
            else //the node is a gateway
            {
                if (pkg.DstType != NodeType.READER)//To server, object etc
                {
                    pkg.Prev = this.Id;
                    pkg.PrevType = NodeType.READER;
                    pkg.Next = pkg.Dst;
                    pkg.NextType = pkg.DstType;
                    SendPacketDirectly(scheduler.currentTime, pkg);
                    return true;
                }
            }

            //have to flood...
            SendAODVData(pkg, dst);
            return true;
        }

        protected GatewayEntity FindNearestGateway()
        {
            //Find a gateway with minimun hops
            int minhops = -1, ming = -1;
            foreach (int g in this.gatewayEntities.Keys)
            {
                if (minhops < 0)
                {
                    minhops = this.gatewayEntities[g].hops;
                    ming = g;
                }
                else if (this.gatewayEntities[g].hops < minhops)
                {
                    minhops = this.gatewayEntities[g].hops;
                    ming = g;
                }
            }
            if (this.gatewayEntities.ContainsKey(ming))
                return this.gatewayEntities[ming];
            else
                return null;
        }

        public bool ExistInNeighborTable(int dst)
        {
            return this.Neighbors.ContainsKey(dst)
                && scheduler.currentTime - this.Neighbors[dst].lastBeacon < global.beaconInterval;
        }

        public virtual RouteEntity GetRouteEntityFromRouteTable(int dst)
        {
            if (routeTable.ContainsKey(dst)
                    && (
                        scheduler.currentTime - routeTable[dst].localLastUpdatedTime < Math.Min(0.5, global.beaconInterval)
                        || IsFreshRecord(routeTable[dst].hops, routeTable[dst].remoteLastUpdatedTime)
                        )
                    )
                return routeTable[dst];
            return null;
        }

        public virtual void SendAODVData(Packet pkg, int dst)
        {
            Reader node = global.readers[pkg.Prev];
            //Check Route Table

            RouteEntity entity = GetRouteEntityFromRouteTable(dst);
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
            /*
            foreach (int nbId in this.Neighbors.Keys.ToList())
            {
                if (nbId == node.Id)
                    continue;
                SendAODVRequest(this.Neighbors[nbId].node, dst, pkg.Tags);
            }*/

            Console.WriteLine("{0:F4} [{1}] {2}{3} tries to send {4}{5} but no route", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.DstType, pkg.Dst);
            SendAODVRequest(Node.BroadcastNode, this.Id, dst, pkg.TTL - 1);
            AddPendingAODVData(pkg);
        }

        public override bool SendData(Packet pkg)
        {
            RoutePacket(pkg);
            return true;
        }

        public void SendAODVData(Packet pkg)
        {
            SendAODVData(pkg, pkg.Dst);
        }

        public virtual void AddPendingAODVData(Packet pkg)
        {
            int dst = pkg.Dst;
            if (!this.pendingAODVData.ContainsKey(dst))
            {
                this.pendingAODVData.Add(dst, new List<PacketCacheEntry>());
            }
            bool found = false;
            foreach (PacketCacheEntry e in this.pendingAODVData[dst])
            {
                if (e.pkg == pkg)
                    found = true;
            }
            if (found == false)
                this.pendingAODVData[dst].Add(new PacketCacheEntry(pkg, scheduler.currentTime));
        }


        public void SendAODVRequest(Node node, int src, int dst, int hops)
        {
            Packet pkg = new Packet(this, node, PacketType.AODV_REQUEST);
            pkg.AODVRequest = new AODVRequestField(src, dst, hops);
            pkg.TTL = 1;
            pkg.Data = dst;
            SendPacketDirectly(scheduler.currentTime, pkg);
        }

        public virtual void RecvAODVRequest(Packet pkg)
        {
            Reader node = global.readers[pkg.Prev];

            int src = pkg.AODVRequest.src;
            int dst = pkg.AODVRequest.dst;
            int hops = pkg.AODVRequest.hops;

            //Console.WriteLine("ttl:{0}, hops:{1}", pkg.TTL, hops);
            if (!Neighbors.ContainsKey(node.Id))
                return;

            if (pendingAODVRequests.ContainsKey(dst)
                && pendingAODVRequests[dst].ContainsKey(src)
                && scheduler.currentTime - pendingAODVRequests[dst][src].firstTime < 1.5)
            {
                //不需要告诉前节点
                //AddPendingAODVRequest(src, node.Id, dst, false);
                return;
            }

            if (src == this.Id)
                return;

            if (this.Id == dst)
            {
                SendAODVReply(dst, node, 0, scheduler.currentTime);
                return;
            }

            //在快速运动的环境下需要加入超时机制
            RouteEntity entity = GetRouteEntityFromRouteTable(dst);
            if (entity != null)
            {
                if (entity.next != node.Id)//避免陷入死循环
                {
                    SendAODVReply(dst, node, entity.hops, routeTable[dst].remoteLastUpdatedTime);
                    return;
                }
            }
            //Not found...
            /*
            foreach (int nbId in this.Neighbors.Keys.ToList())
            {
                if (nbId == node.Id)
                    continue;
                SendAODVRequest(this.Neighbors[nbId].node, dst, pkg.Tags);
            }
             */
            //SendAODVRequest(Node.BroadcastNode, dst, pkg.Tags);
            if (hops > 0)
            {
                //Console.WriteLine("hops:{0}", hops);
                SendAODVRequest(Node.BroadcastNode, src, dst, hops - 1);
                AddPendingAODVRequest(src, node.Id, dst, true);
            }
        }

        protected void AddPendingAODVRequest(int src, int prev, int dst, bool updateFirstTime)
        {
            if (!this.pendingAODVRequests.ContainsKey(dst))
            {
                this.pendingAODVRequests.Add(dst, new Dictionary<int, PendingAODVRequestCacheEntry>());
            }

            if (!this.pendingAODVRequests[dst].ContainsKey(src))
                this.pendingAODVRequests[dst].Add(src, new PendingAODVRequestCacheEntry());
            else if (updateFirstTime == true)
                this.pendingAODVRequests[dst][src].firstTime = scheduler.currentTime;

            if (!this.pendingAODVRequests[dst][src].prevs.Contains(prev))
                this.pendingAODVRequests[dst][src].prevs.Add(prev);
        }

        public virtual void SendAODVReply(int dst, Reader node, int hops, double lastTime)
        {
            Packet pkg = new Packet(this, node, PacketType.AODV_REPLY);
            pkg.TTL = 1;
            pkg.Data = new AODVReply(dst, hops, lastTime);
            SendPacketDirectly(scheduler.currentTime, pkg);
        }

        public virtual void RecvAODVReply(Packet pkg)
        {
            //Console.WriteLine("ttl:{0} hops: {1}", pkg.TTL, ((AODVReply)pkg.Data).hops);
            Reader node = global.readers[pkg.Prev];

            //Console.WriteLine("{0:F4} [{1}] {2}{3} Recv AODV Reply from {4}{5}", scheduler.CurrentTime, pkg.Type, this.type, this.id, pkg.PrevType, pkg.Prev);  
            if (!Neighbors.ContainsKey(node.Id))
                return;

            AODVReply reply = (AODVReply)pkg.Data;
            if (!routeTable.ContainsKey(reply.dst))
            {
                routeTable.Add(reply.dst, new RouteEntity(reply.dst, node.Id, reply.hops + 1, reply.lastTime, scheduler.currentTime));
                //Console.WriteLine("{0}--{1}", routeTable[reply.dst].dst, routeTable[reply.dst]);
            }
            else
            {
                RouteEntity entity = (RouteEntity)routeTable[reply.dst];
                if (reply.hops < entity.hops || reply.lastTime - entity.remoteLastUpdatedTime > 1)
                {
                    entity.hops = reply.hops + 1;
                    entity.next = node.Id;
                    entity.remoteLastUpdatedTime = reply.lastTime;
                    entity.localLastUpdatedTime = scheduler.currentTime;   
                }
                //Console.WriteLine("{0}--{1}", routeTable[reply.dst].dst, routeTable[reply.dst]);
            }
            //Console.WriteLine("dist:{0}", Utility.Distance((Reader)(Reader.getNode(pkg.Prev, NodeType.READER)), this));
            //Console.WriteLine("{0}--{1}", routeTable[reply.dst].dst, routeTable[reply.dst]);
            if (pendingAODVRequests.ContainsKey(reply.dst))
            {
                foreach (int src in pendingAODVRequests[reply.dst].Keys)
                {
                    HashSet<int> prevs = (HashSet<int>)pendingAODVRequests[reply.dst][src].prevs;
                    foreach (int prev in prevs)
                    {
                        SendAODVReply(reply.dst, global.readers[prev], reply.hops + 1, reply.lastTime);
                    }
                    prevs.Clear();
                }
                pendingAODVRequests.Remove(reply.dst);
            }
            //Send pending datas...
            if (pendingAODVData.ContainsKey(reply.dst))
            {
                List<PacketCacheEntry> entries = (List<PacketCacheEntry>)pendingAODVData[reply.dst];
                foreach (PacketCacheEntry entry in entries)
                {
                    Packet pkg1 = entry.pkg;
                    //Console.WriteLine("---------{0}", routeTable[reply.dst]);
                    //Console.WriteLine("+++++++++{0}--{1}", routeTable[reply.dst].dst, routeTable[reply.dst]);
                    if (routeTable.ContainsKey(reply.dst))
                        pkg1.TTL = Math.Max(pkg1.TTL, routeTable[reply.dst].hops + 1);
                    SendAODVData(pkg1);
                }
            }
            pendingAODVData.Remove(reply.dst);
        }

        public virtual void SendPingRequest(int nodeId)
        {
            this.retryOnSendingFailture = true;
            Reader node = global.readers[nodeId];
            Packet pkg = new Packet(this, node, PacketType.PING_REQUEST);
            pkg.Data = 0;//hops
            SendAODVData(pkg);
            this.retryOnSendingFailture = false;
        }


        public virtual void SendPingRequest(int nodeId, int ttl)
        {
            this.retryOnSendingFailture = true;
            Reader node = global.readers[nodeId];
            Packet pkg = new Packet(this, node, PacketType.PING_REQUEST);
            pkg.TTL = ttl;
            pkg.Data = 0;//hops
            SendAODVData(pkg);
            this.retryOnSendingFailture = false;
        }

        public virtual void RecvPingRequest(Packet pkg)
        {
            Reader node = global.readers[pkg.Prev];
            pkg.Data = (int)pkg.Data + 1;
            if (pkg.Dst == this.Id && pkg.DstType == this.type)
            {
                this.retryOnSendingFailture = true;
                Packet pkg1 = new Packet(this, node, PacketType.PING_RESPONSE);
                pkg1.TTL = global.TTL;
                pkg1.Data = 0;
                SendAODVData(pkg1);
                this.retryOnSendingFailture = false;
            }
            else
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
            }
        }


        public virtual void RecvPingResponse(Packet pkg)
        {
            Reader node = global.readers[pkg.Prev];
            pkg.Data = (int)pkg.Data + 1;
            if (pkg.Dst == this.Id && pkg.DstType == this.type)
            {
                //Console.WriteLine("ping recv.");
            }
            else
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
            }
        }


        public List<Reader> GetAllNearReaders(float distance, bool dataReaders)
        {
            Global global = Global.getInstance();
            List<Reader> list = new List<Reader>();
            for (int i = 0; i < global.readers.Length; i++)
            {
                //ignore itself
                if (global.readers[i].Id == Id)
                    continue;

                double x = global.readers[i].X - this.X;
                double y = global.readers[i].Y - this.Y;
                if (x < distance && x > 0 - distance
                    && y < distance && x > 0 - distance
                    && Utility.Distance((MobileNode)(global.readers[i]), (MobileNode)this) < distance
                    )
                    list.Add(global.readers[i]);

            }
            list.Add(this);
            return list;
        }

        public static List<ObjectNode> GetAllNearObjects(MobileNode node, float distance)
        {
            Global global = Global.getInstance();
            List<ObjectNode> list = new List<ObjectNode>();
            for (int i = 0; i < global.objects.Length; i++)
            {
                double x = global.objects[i].X - node.X;
                double y = global.objects[i].Y - node.Y;
                if (x < distance && x > 0 - distance
                    && y < distance && x > 0 - distance
                    && Utility.Distance((MobileNode)(global.objects[i]), node) < distance
                    )
                    list.Add(global.objects[i]);
            }
            return list;
        }


        //Get all near reader rather than objects.
        public virtual void NotifyObjects()
        {
            List<ObjectNode> list = GetAllNearObjects(this, global.objectMaxDist);

            foreach (ObjectNode obj in list)
            {
                if (this.LandmarkReader == true)
                {
                    Packet pkg = new Packet(this, obj, PacketType.LANDMARK);
                    pkg.LandmarkNotification = new LandmarkNotificationField(this.x, this.y);
                    SendPacketDirectly(scheduler.currentTime, pkg);
                }
                else
                {
                    if (!this.NearbyObjectCache.ContainsKey(obj.Id))
                        this.NearbyObjectCache.Add(obj.Id, new ObjectEntity(obj.Id, -1, scheduler.currentTime));
                    else
                    {
                        if (scheduler.currentTime - this.NearbyObjectCache[obj.Id].time < 3)//TODO 3 is fixed...
                            continue;
                        this.NearbyObjectCache[obj.Id].time = scheduler.currentTime;
                    }

                    Packet pkg = new Packet(this, obj, PacketType.DATA_AVAIL);
                    SendPacketDirectly(scheduler.currentTime, pkg);
                }
            }
        }

        public void Drop(Packet pkg)
        {
            pkg = null;
        }

        public double GetCurrentSpeed()
        {
            double speed = 0;
            if (this.Speed != null && this.Speed.Count > 0)
                speed = this.Speed[0];
            return speed;
        }


        //receiving a query request? 
        //if no optimization, do it like AODV
        public void RecvQueryRequest(Packet pkg)
        {
            RecvAODVRequest(pkg);
        }
        //the same like upper function
        public void RecvQueryReply(Packet pkg)
        {
            RecvAODVReply(pkg);
        }

        public static Reader getNode(int n)
        {
            Global global = Global.getInstance();
            Reader node = null;
            if (n >= global.readerNum)
            {
                Console.WriteLine("Warning: parse READER{0} bigger than readerNum({1})", n, global.readerNum);
                node = null;
            }
            else if (n < Reader.BroadcastNode.Id)
                node = null;
            else if (n == Reader.BroadcastNode.Id)
                node = (Reader)Reader.BroadcastNode;
            else
                node = global.readers[n];

            return node;
        }


        public override void Recv(Packet pkg)
        {
            pkg.seqInited = false;
            //只有reader才需要检查，但是里面函数处理了
            CheckPacketCount(pkg);

            if (pkg.PrevType == NodeType.OBJECT || pkg.PrevType == NodeType.READER)
            {
                //不检查了，只在发送的时候检查
                /*
                Node node = Node.getNode(pkg.Prev, pkg.PrevType);
                double dist = pkg.PrevType == NodeType.OBJECT ? global.objectMaxDist : global.nodeMaxDist;
                if (Utility.Distance(this, (MobileNode)node) > dist)
                {
                    //if (pkg.Next == id)
                    //    Console.WriteLine("{0:F4} [{1}] {2}{3} Drop data of {4}{5} due to out of space.", scheduler.CurrentTime, pkg.Type, this.type, this.id, node.type, node.Id);

                    return;
                }*/
            }

            if ((pkg.Next != Id && pkg.Next != Node.BroadcastNode.Id) || pkg.NextType != NodeType.READER)
            {
                return;
            }

            if (pkg.Type == PacketType.AODV_REQUEST)
                Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}({6}->{7}->{8})", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev, pkg.Src, pkg.Dst, pkg.AODVRequest.dst);
            else if (pkg.Type == PacketType.AODV_REPLY)
                Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}({6}->{7}->{8})", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev, pkg.Src, pkg.Dst, ((AODVReply)pkg.Data).dst);
            else if (pkg.Type != PacketType.BEACON)
                Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}({6}->{7}->{8})", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev, pkg.Src, pkg.Prev, pkg.Dst);

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
            switch (pkg.Type)
            {
                case PacketType.BEACON:
                    RecvBeacon(pkg);
                    break;
                case PacketType.DATA:
                case PacketType.LOCATION_UPDATE:
                    if (pkg.Src == this.Id)
                    {
                        Console.WriteLine("reader{0} recv a data packet to itself", this.Id);
                        return;
                    }
                    RoutePacket(pkg);
                    break;
                case PacketType.AODV_REQUEST:
                    RecvAODVRequest(pkg);
                    break;
                case PacketType.AODV_REPLY:
                    RecvAODVReply(pkg);
                    break;
                case PacketType.PING_REQUEST:
                    RecvPingRequest(pkg);
                    break;
                case PacketType.PING_RESPONSE:
                    RecvPingResponse(pkg);
                    break;
                default:
                    Debug.Assert(false, "Unknow Type " + pkg.Type);
                    break;
            }
        }
    }
}
