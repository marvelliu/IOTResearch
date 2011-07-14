using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace NewTrustArch
{
    public class RSUEntity
    {
        public int id;
        public int hops;
        public RSUEntity(int id, int hops)
        {
            this.id = id;
            this.hops = hops;
        }
    }


    public enum ReaderType
    {
        NORMAL,
        DROP_PACKET,
        MODIFY_PACKET,
        DROP_COMMAND,
        MODIFY_COMMAND,
        //...
    }

    public class IOTReader : Reader
    {

        new public static IOTReader ProduceReader(int id, int org)
        {
            return new IOTReader(id, org);
        }

        public IOTReader(int id, int org)
            : base(id, org)
        {
            this.global = (IOTGlobal)Global.getInstance();
            this.ReaderCache = new Dictionary<int, RSUEntity>();
            this.wiredNodeCache = new List<int>();
            this.orgMonitorMapping = new Dictionary<int, List<int>>();
            this.cachedMonitorNodes = new Dictionary<int, HashSet<int>>();
            this.readerType = ReaderType.NORMAL;
            this.observedPhenomemons = new HashSet<IOTPhenomemon>();
            this.neighborSpeedPhenomemons = new Dictionary<int, IOTPhenomemon>();

            IOTPhenomemon p = new IOTPhenomemon(IOTPhenomemonType.MOVE_FAST, id);
            this.neighborSpeedPhenomemons.Add(id, p);
            this.observedPhenomemons.Add(p);

            CheckRoutine();
        }

        public ReaderType readerType;


        private IOTGlobal global;

        //TODO 定时清空Phenomemon
        private int totalReceivedPackets;
        private IOTPhenomemon bandwidthPhenomemon;
        private Dictionary<int, IOTPhenomemon> neighborSpeedPhenomemons;

        private Dictionary<int, RSUEntity> ReaderCache;
        private List<int> wiredNodeCache;

        //普通节点观察到的现象
        private HashSet<IOTPhenomemon> observedPhenomemons;
        //普通节点中org和monitor的对应
        private Dictionary<int, List<int>> orgMonitorMapping;

        private Dictionary<int, HashSet<int>> cachedMonitorNodes;

        //如果是monitor
        //所需监控的机构数据
        private HashSet<int> assignedMonitorOrgs;
        //缓存的其他节点的事件
        private List<IOTEventTrustResult> cachedEventTrustResult;

        public int networkId;

        public void AssignMonitor(int org)
        {
            if (!this.assignedMonitorOrgs.Contains(org))
                this.assignedMonitorOrgs.Add(org);
        }
        public void RevokeMonitor(int org)
        {
            if (this.assignedMonitorOrgs.Contains(org))
                this.assignedMonitorOrgs.Remove(org);
        }

        public override void Recv(AdHocBaseApp.Packet pkg)
        {
            if (pkg.PrevType == NodeType.OBJECT || pkg.PrevType == NodeType.READER)
            {
                Node node = Node.getNode(pkg.Prev, pkg.PrevType);
                double dist = pkg.PrevType == NodeType.OBJECT ? global.objectMaxDist : global.nodeMaxDist;
                if (Utility.Distance(this, (MobileNode)node) > dist)
                {
                    if (pkg.Next == Id)
                        Console.WriteLine("{0:F4} [{1}] {2}{3} Drop data of {4}{5} due to out of space.", scheduler.currentTime, pkg.Type, this.type, this.Id, node.type, node.Id);
                    CheckPacketCount(pkg);
                    return;
                }
            }
            //Check the Phenomemon
            if (pkg.PrevType == NodeType.READER)
                AddReceivePacketPhenomemon(pkg);
            if (pkg.PrevType == NodeType.OBJECT)
                AddReceiveObjectPhenomemon(pkg);
            //TODO: Other type

            if ((pkg.Next != Id && pkg.Next != Node.BroadcastNode.Id) || pkg.NextType != NodeType.READER)
                return;
            //I send the packet myself, ignore
            if (pkg.Prev == Id && pkg.PrevType == type)
                return;

            //中间节点恶意抛弃？
            if (pkg.PrevType == NodeType.OBJECT)
            {
                if (readerType == ReaderType.DROP_PACKET && pkg.Dst != Id)
                {
                    if (pkg.Type == PacketType.DATA || pkg.Type == PacketType.COMMAND)
                    {
                        Console.WriteLine("{0:F4} [{1}] {2}{3} Drop data of {4}{5} due to bad node. packet ident:{6}---{7}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev, pkg.PrevSenderSeq, pkg.SrcSenderSeq);
                        if (pkg.PrevType == NodeType.READER)
                            CheckPacketCount(pkg);
                        return;
                    }
                }
            }

            switch (pkg.Type)
            {
                //Readers
                case PacketType.BEACON:
                    //Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.CurrentTime, pkg.Type, this.type, this.id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvBeacon(pkg);
                    break;
                //Objects
                case PacketType.TAG_HEADER:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvTagHeaderResponse(pkg);
                    break;
                case PacketType.AUTHORIZATION:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvAuthorization(pkg);
                    break;
                case PacketType.COMMAND:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvCommand(pkg);
                    break;
                //Some codes are hided in the base class.
                default:
                    base.Recv(pkg);
                    return;
            }
            pkg.TTL -= 1;
            if (pkg.TTL < 0)
                Drop(pkg);
        }


        override public void SendBeacon(float time)
        {
            Packet pkg = new Packet();
            pkg.Type = PacketType.BEACON;
            pkg.Src = pkg.Prev = Id;
            pkg.Dst = pkg.Next = -1;//Broadcast
            pkg.TTL = 1;

            pkg.Beacon = new BeaconField();
            if (this.gatewayEntities.Count > 0)
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
            else
                nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            Event.AddEvent(new Event(scheduler.currentTime + nextBeacon, EventType.SND_BCN, this, null));
        }


        override public void RecvBeacon(Packet pkg)
        {
            Scheduler scheduler = Scheduler.getInstance();
            Reader node = global.readers[pkg.Src];

            if (pkg.Prev == Id && pkg.PrevType == type)
                return;

            Neighbor nb = null;
            if (Neighbors.ContainsKey(node.Id))
            {
                nb = (Neighbor)Neighbors[node.Id];
            }
            if (nb != null)
            {
                nb.lastBeacon = scheduler.currentTime;
            }
            else
            {
                //Add as a neighbor
                AddNeighbor(node);
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

        //Get all near reader rather than objects.
        override public void NotifyObjects()
        {
            List<ObjectNode> list = GetAllNearObjects(this, global.objectMaxDist);

            foreach (ObjectNode obj in list)
            {
                if (!this.NearbyObjectCache.ContainsKey(obj.Id))
                {
                    this.NearbyObjectCache.Add(obj.Id, new ObjectEntity(obj.Id, -1, scheduler.currentTime));
                }
                else
                {
                    if (scheduler.currentTime - this.NearbyObjectCache[obj.Id].time < 1)//TODO 1 is fixed...
                        continue;
                    this.NearbyObjectCache[obj.Id].time = scheduler.currentTime;
                }
                Packet pkg = new Packet(this, obj, PacketType.TAG_HEADER); //Request the object's tag header.
                SendPacketDirectly(scheduler.currentTime, pkg);
            }
        }


        public override void CheckNearObjects()
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
            foreach (int o in temp)
            {
                this.NearbyObjectCache.Remove(o);
                //Console.WriteLine("Node " + id + " remove obj " + o);
            }
            Event.AddEvent(new Event(scheduler.currentTime + global.checkNearObjInterval, EventType.CHK_NEAR_OBJ, this, null));
        }


        public void RecvTagHeaderResponse(Packet pkg)
        {
            //终端节点收到标签的请求
            if (pkg.PrevType == NodeType.OBJECT)
            {
                int tagId = pkg.ObjectTagHeader.tagId;
                int orgId = pkg.ObjectTagHeader.orgId;
                if (!this.NearbyObjectCache.ContainsKey(tagId))
                {
                    throw new Exception("no such a tag before, design eror?");
                } 
                if (this.NearbyObjectCache[tagId].orgId < 0)
                {
                    //首先添加缓存
                    Console.WriteLine("{0:F4} [NEW_TAG_FOUND] {1}{2} of ORG{3} found by READER{4}", scheduler.currentTime, pkg.PrevType, tagId, orgId, Id);
                    //this.NearbyObjectCache.Add(tagId, new ObjectEntity(tagId, orgId, scheduler.CurrentTime));
                    this.NearbyObjectCache[tagId].key = -1;
                    this.NearbyObjectCache[tagId].orgId = orgId;
                    this.NearbyObjectCache[tagId].time = scheduler.currentTime;

                }
                if (this.NearbyObjectCache[tagId].key >= 0)
                {
                    Packet pkg1 = new Packet(this, global.objects[pkg.Src], PacketType.DATA_AVAIL);
                    pkg1.Authorization = new AuthorizationField(new int[1] { tagId }, new int[1] { this.NearbyObjectCache[tagId].key });
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                    return;
                }

                //如果缓存中没有该节点，或尚未授权，则发请求到相应机构
                //TODO make sure the request does not send too soon


                //send request to the org
                Packet pkg2 = new Packet(this, global.orgs[orgId], PacketType.TAG_HEADER);
                pkg2.ObjectTagHeader = new ObjectTagHeaderField(
                    pkg.ObjectTagHeader.tagId, pkg.ObjectTagHeader.orgId);                
                pkg2.ObjectTagHeader.networkId = networkId;
                RoutePacket(pkg2);
                return;

            }
            if (this.IsGateway == true)
            {
                pkg.Next = pkg.Dst;
                pkg.NextType = pkg.DstType;
                SendPacketDirectly(scheduler.currentTime, pkg);
                return;
            }
            else
            {
                RoutePacket(pkg);
                return;
            }
        }

        public void RecvSetMonitor(Packet pkg)
        {
            int monitorNode = pkg.SetMonitorResponse.monitorNode;
            int monitorOrg = pkg.SetMonitorResponse.monitorOrg;
            int monitorNetwork = pkg.SetMonitorResponse.monitorNetwork;
            //机构发向某节点的setmonitor
            if (pkg.Dst != Node.BroadcastNode.Id)
            {
                if (pkg.Dst == Id) // i am dst
                {
                    if (this.assignedMonitorOrgs == null) // the first time
                    {
                        Console.WriteLine("{0:F4} [{1}] {2}{3} is selected as a monitor of {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.SrcType, pkg.Src);
                        this.assignedMonitorOrgs = new HashSet<int>();
                        this.cachedEventTrustResult = new List<IOTEventTrustResult>();
                        CheckEvents();
                    }
                    if (!this.assignedMonitorOrgs.Contains(pkg.Src))
                        this.assignedMonitorOrgs.Add(pkg.Src);
                    else
                        return;
                    Packet pkg1 = new Packet(this, Node.BroadcastNode, PacketType.SET_MONITOR);
                    pkg1.SetMonitorResponse = new SetMonitorResponseField(monitorNode, monitorOrg, monitorNetwork);
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                    return;
                }
                else  //not dst, forward
                    RoutePacket(pkg);
            }
            else
            {
                if (this.orgMonitorMapping.ContainsKey(monitorOrg) && this.orgMonitorMapping[monitorOrg].Contains(monitorNode))
                    return;
                else
                {
                    if (!this.orgMonitorMapping.ContainsKey(monitorOrg))
                        this.orgMonitorMapping.Add(monitorOrg, new List<int>());
                    this.orgMonitorMapping[monitorOrg].Add(monitorNode);
                    Packet pkg1 = new Packet(this, Node.BroadcastNode, PacketType.SET_MONITOR);
                    pkg1.SetMonitorResponse = new SetMonitorResponseField(monitorNode, monitorOrg, monitorNetwork);
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                    return;
                }
            }
        }

        public void RecvAuthorization(Packet pkg)
        {
            if (pkg.Dst == Id) // i am dst
            {
                AuthorizationField f = pkg.Authorization;
                for (int i = 0; i < f.tags.Length; i++)
                {
                    int tagId = f.tags[i];
                    int key = f.keys[i];

                    if (!this.NearbyObjectCache.ContainsKey(tagId))
                    {
                        Console.WriteLine("OBJECT{0} is out of READER{1}", tagId, Id);
                        continue;
                    }
                    Console.WriteLine("{0:F4} {1}{2} is granted to read TAG{3}.", scheduler.currentTime, this.type, this.Id, tagId);
                    this.NearbyObjectCache[tagId].key = key;
                    Packet pkg1 = new Packet(this,
                        global.objects[tagId], PacketType.DATA_AVAIL);
                    int[] tags = new int[1] { tagId };
                    int[] keys = new int[1] { key };
                    pkg1.Authorization = new AuthorizationField(tags, keys);
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                }
                return;
            }
            //forward
            RoutePacket(pkg);
        }

        //将接收到的数据包添加到观察到的现象中
        public void AddReceivePacketPhenomemon(Packet pkg)
        {
            IOTPhenomemon p;
            this.totalReceivedPackets++;
            //忽略广播包(从实际来看，发送广播包的一般是节点本身的行为，不需要考虑其对数据包的恶意操作)
            if (pkg.Next == Node.BroadcastNode.Id)
                return;

            //记录发送现象
            if (pkg.Next != Node.BroadcastNode.Id)
            {
                p = new IOTPhenomemon(IOTPhenomemonType.SEND_PACKET, pkg.Prev, scheduler.currentTime, pkg);
                p.likehood = global.sendLikehood;
                this.observedPhenomemons.Add(p);
                if(global.debug)
                    Console.WriteLine("[Debug] reader{0} add a RECV phenomemon of reader{1}", Id, pkg.Next);
            }

            //数据包到达目的地，忽略

            //记录接收现象
            if (pkg.Next != pkg.Dst)
            {
                p = new IOTPhenomemon(IOTPhenomemonType.RECV_PACKET, pkg.Next, scheduler.currentTime, pkg);
                p.likehood = global.recvLikehood;
                this.observedPhenomemons.Add(p);
                if(global.debug)
                    Console.WriteLine("[Debug] reader{0} add a SEND phenomemon of reader{1}", Id, pkg.Prev);
            }
        }

        
        //将接收到的数据包添加到观察到的现象中
        public void AddReceiveObjectPhenomemon(Packet pkg)
        {
            IOTPhenomemon p;
            this.totalReceivedPackets++;
            //忽略广播包(从实际来看，发送广播包的一般是节点本身的行为，不需要考虑其对数据包的恶意操作)
            if (pkg.Next == Node.BroadcastNode.Id)
                return;

            //记录发送现象
            if (pkg.Next != Node.BroadcastNode.Id)
            {
                p = new IOTPhenomemon(IOTPhenomemonType.SEND_PACKET, pkg.Prev, scheduler.currentTime, pkg);
                p.likehood = global.sendLikehood;
                this.observedPhenomemons.Add(p);
                //Console.WriteLine("[Debug] reader{0} add a RECV phenomemon of reader{1}", id, pkg.Next);
            }

            //数据包到达目的地，忽略

            //记录接收现象
            if (pkg.Next != pkg.Dst)
            {
                p = new IOTPhenomemon(IOTPhenomemonType.RECV_PACKET, pkg.Next, scheduler.currentTime, pkg);
                p.likehood = global.recvLikehood;
                this.observedPhenomemons.Add(p);
                //Console.WriteLine("[Debug] reader{0} add a SEND phenomemon of reader{1}", id, pkg.Prev);
            }
        }

        //普通节点检查现象
        public void CheckRoutine()
        {
            //Console.WriteLine("Reader{0} check routing.", id);
            //d-s理论的现象不需要放进去了

            //float time = scheduler.CurrentTime + global.checkPhenomemonTimeout;
            //Event.AddEvent(new Event(time, EventType.CHK_RT_TIMEOUT, this, null));
            //Console.WriteLine("Reader{0} check routing done.", id);
        }

        void CheckNetworkBandwidth()
        {
            if (this.bandwidthPhenomemon == null)
                this.bandwidthPhenomemon = new IOTPhenomemon(IOTPhenomemonType.BANDWIDTH_BUSY, this.Id);

            this.bandwidthPhenomemon.start = this.bandwidthPhenomemon.end;
            this.bandwidthPhenomemon.end = scheduler.currentTime;
            this.bandwidthPhenomemon.likehood = Math.Min(this.totalReceivedPackets / global.totalPacketThreahold + global.SmallValue, 0.9);
            if (!this.observedPhenomemons.Contains(this.bandwidthPhenomemon))
                this.observedPhenomemons.Add(this.bandwidthPhenomemon);
            this.totalReceivedPackets = 0;
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
                    IOTPhenomemon p = new IOTPhenomemon(IOTPhenomemonType.MOVE_FAST, node);
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
            List<IOTPhenomemon> temp1 = new List<IOTPhenomemon>();
            List<IOTPhenomemon> temp2 = new List<IOTPhenomemon>();
            foreach (IOTPhenomemon p in this.observedPhenomemons)
            {
                if (p.type == IOTPhenomemonType.RECV_PACKET && scheduler.currentTime - p.start > sendTimeout)
                {
                    IOTPhenomemon foundSend = null, foundNotSend = null;
                    foreach (IOTPhenomemon p1 in this.observedPhenomemons)
                    {
                        if (p1.pkg == null)
                            continue;
                        //找到该节点对该数据包的操作
                        if (Packet.IsSamePacket(p1.pkg, p.pkg) &&
                            p1.nodeId == p.nodeId)
                        {
                            if (p1.type == IOTPhenomemonType.SEND_PACKET)
                            {
                                foundSend = p1;
                                continue;
                            }
                            else if (p1.type == IOTPhenomemonType.NOT_SEND_PACKET)
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
                        IOTPhenomemon p2 = new IOTPhenomemon(IOTPhenomemonType.NOT_SEND_PACKET, p.nodeId, p.start, scheduler.currentTime, p.pkg);
                        p2.likehood = global.checkTimeoutPhenomemonLikehood;
                        temp1.Add(p2);
                    }
                }
            }
            foreach (IOTPhenomemon p in temp1)
            {
                this.observedPhenomemons.Add(p);
            }
            foreach (IOTPhenomemon p in temp2)
            {
                this.observedPhenomemons.Remove(p);
            }
        }

        void ClearOutdatedPhenomemons()
        {
            float timeThrehold = 2*global.checkPhenomemonTimeout;

            List<IOTPhenomemon> temp = new List<IOTPhenomemon>();
            foreach (IOTPhenomemon p in this.observedPhenomemons)
            {
                if (scheduler.currentTime - p.start > timeThrehold)
                {
                    temp.Add(p);
                    if (p.type == IOTPhenomemonType.MOVE_FAST)
                        this.neighborSpeedPhenomemons.Remove(p.nodeId);
                }
            }
            foreach (IOTPhenomemon p in temp)
            {
                this.observedPhenomemons.Remove(p);
            }
            //Console.WriteLine("Clear outdated phenomemon done.");
        }

        public void RecvEventReport(Packet pkg)
        {
            if (pkg.Dst != Id)
            {
                RoutePacket(pkg);
                return;
            }

            if (this.assignedMonitorOrgs == null || this.assignedMonitorOrgs.Count == 0)
            {
                Console.WriteLine("Reader{0} is not a monitor node.", Id);
                return;
            }

            MemoryStream ms = new MemoryStream(pkg.TrustReport.result);
            BinaryFormatter formatter = new BinaryFormatter();
            List<IOTEventTrustResult> result = (List<IOTEventTrustResult>)formatter.Deserialize(ms);
            foreach (IOTEventTrustResult r in result)
                this.cachedEventTrustResult.Add(r);
        }

        public void RecvMonitorResponse(Packet pkg)
        {
            GetMonitorResponseField f = pkg.GetMonitorResponse;
            if (this.IsGateway)
            {
                if (!this.cachedMonitorNodes.ContainsKey(f.monitorOrg))
                    this.cachedMonitorNodes.Add(f.monitorOrg, new HashSet<int>());
                if (this.cachedMonitorNodes[f.monitorOrg].Contains(f.monitorNode))
                    this.cachedMonitorNodes[f.monitorNode].Add(f.monitorNode);
            }
            RoutePacket(pkg);
        }
        public void RecvMonitorRequest(Packet pkg)
        {
            int networkId = pkg.GetMonitorRequest.network;
            if (this.networkId != networkId)
            {
                Console.WriteLine("Fatal: wrong network: {0} - {1}", this.networkId, networkId);
                return;
            }
            GetMonitorRequestField f = pkg.GetMonitorRequest;
            int[] orgs = f.orgs;
            int requestOrg = f.requestOrg;
            if (this.IsGateway)
            {
                //如果自己是网关，则查找
                foreach (int o in orgs)
                {
                    if (this.cachedMonitorNodes.ContainsKey(o))
                    {
                        Packet pkg1 = new Packet(this, global.orgs[requestOrg], PacketType.GET_MONITOR_RESPONSE);
                        pkg1.GetMonitorResponse = new GetMonitorResponseField(o, this.cachedMonitorNodes[o].First(), networkId);
                        SendPacketDirectly(scheduler.currentTime, pkg1);
                        return;
                    }
                }
            }

            //如果自己网关，但没有缓存信息，或者自己是普通节点，检查自己的机构
            for (int i = 0; i < orgs.Length; i++)
            {
                if (orgs[i] == this.OrgId)
                {
                    Packet pkg1 = new Packet(this, global.orgs[requestOrg], PacketType.GET_MONITOR_RESPONSE);
                    pkg1.GetMonitorResponse = new GetMonitorResponseField(this.Id, this.OrgId, networkId);
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                    return;
                }
            }
            //如果自己的机构不在列表中，则广播其他邻居
            Packet pkg2 = new Packet();
            pkg.Src = pkg.Prev = Id;
            pkg.Dst = pkg.Next = -1;//Broadcast
            pkg.GetMonitorRequest = new GetMonitorRequestField(f.orgs, f.network, f.requestOrg);
            SendPacketDirectly(scheduler.currentTime, pkg2);
        }

        public void RecvCommand(Packet pkg)
        {
            if (pkg.Dst == Id)
            {
                CommandField cmd = pkg.Command;
                CommandField cmd1 = new CommandField(cmd.tag, cmd.operation);
                if (this.readerType == ReaderType.DROP_COMMAND)
                    return;
                if (this.readerType == ReaderType.MODIFY_COMMAND)
                    cmd1.operation = -1; // -1表示被修改了
                Packet pkg1 = new Packet(this, global.objects[cmd.tag], PacketType.COMMAND);
                SendPacketDirectly(scheduler.currentTime, pkg1);
            }
            else
                RoutePacket(pkg);
        }

        //监控节点定时检查自己缓存的事件，推导出恶意节点
        public void CheckEvents()
        {
            //不用d-s就不需要了
            /*
            Console.WriteLine("{0:F4} Monitor reader{0} check events.", scheduler.CurrentTime, id);
            if (this.cachedEventTrustResult != null && this.cachedEventTrustResult.Count > 0)
            {
                Dictionary<int, List<IOTNodeTrustResult>> orgNodeTrustResults = IOTNodeTrust.DeduceAllNodeTrusts(id, this.cachedEventTrustResult, global.checkEventTimeout);

                foreach (KeyValuePair<int, List<IOTNodeTrustResult>> k in orgNodeTrustResults)
                {
                    int org = k.Key;
                    List<IOTNodeTrustResult> nodeTrusts = k.Value;

                    byte[] buf = new byte[global.BufSize * nodeTrusts.Count];
                    MemoryStream ms = new MemoryStream(buf);
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(ms, nodeTrusts);
                    byte[] tmp = new byte[ms.Position];
                    Array.Copy(buf, tmp, ms.Position);

                    Packet pkg = new Packet(this, global.orgs[org], PacketType.NODE_REPORT);
                    //pkg.TrustReport = new TrustReportField(org, data, bw.BaseStream.Position);
                    pkg.TrustReport = new TrustReportField(org, tmp, tmp.Length);
                    SendPacketDirectly(scheduler.CurrentTime, pkg);
                }
                //clear the cache
                this.cachedEventTrustResult.Clear();
            }

            float time = scheduler.CurrentTime + global.checkEventTimeout;
            Event.AddEvent(new Event(time, EventType.CHK_EVENT_TIMEOUT, this, null));
             */
        }

        public static ReaderType ParseReaderType(string st)
        {
            if (st == "DROP_PACKET")
                return ReaderType.DROP_PACKET;
            else if (st == "MODIFY_PACKET")
                return ReaderType.MODIFY_PACKET;
            else if (st == "DROP_COMMAND")
                return ReaderType.DROP_COMMAND;
            else if (st == "MODIFY_COMMAND")
                return ReaderType.MODIFY_COMMAND;
            else if (st == "NORMAL")
                return ReaderType.NORMAL;
            else
                throw new Exception("no such a reader type: " + st);
        }

        public static void SetReaderTypes()
        {
            Console.WriteLine("SetReaderTypes start");
            IOTGlobal g = (IOTGlobal)Global.getInstance();
            Dictionary<ReaderType, int>[] r = new Dictionary<ReaderType, int>[g.orgNum];
            for (int i = 0; i < r.Length; i++)
            {
                r[i] = new Dictionary<ReaderType, int>();
            }
            int[] badOrgs = g.badOrgs;
            double[] badNodeRates = g.badNodeRates;
            ReaderType[] badNodeTypes = g.badNodeTypes;
            int badOrgNum = g.badOrgNum;
            for (int i = 0; i < badOrgNum; i++)
            {
                int badOrg = badOrgs[i];
                int badNodeNum = (int)(badNodeRates[i] * g.orgs[badOrg].Nodes.Count);
                ReaderType badNodeType = badNodeTypes[i];
                if (r[badOrg].ContainsKey(badNodeType))
                {
                    Console.WriteLine("{0}-{1}-{2} already exists, overwrite.", badOrg, badNodeNum, badNodeType);
                    r[badOrg][badNodeType] = badNodeNum;
                    continue;
                }
                else
                    r[badOrg].Add(badNodeType, badNodeNum);
            }
            for (int org = 0; org < r.Length; org++)
            {
                foreach (KeyValuePair<ReaderType, int> k in r[org])
                {
                    ReaderType type = k.Key;
                    int nodes = k.Value;
                    for (int j = 0; j < nodes; j++)
                    {
                        int n = 0;
                        IOTReader reader  = null;
                        do
                        {
                            n = (int)Utility.U_Rand(g.orgs[org].Nodes.Count);
                            reader = (IOTReader)g.orgs[org].Nodes[n];
                        } while (reader.readerType != ReaderType.NORMAL || reader.IsGateway);
                        //这里假定网关不是恶意节点，否则就抛弃所有数据包了……
                        reader.readerType = type;
                        Console.WriteLine("Reader{0} is set as a bad node: {1}", reader.Id, type);
                    }
                }
            }
        }


    }
}
