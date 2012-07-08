using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace VANETs
{
    public class RSUEntity
    {
        public int id;
        public int hops;
        public int nbs;
        public Certificate cert;
        public bool isWired;

        public RSUEntity(int id, int hops, int nbs, Certificate cert, bool isWired)
        {
            this.id = id;
            this.hops = hops;
            this.nbs = nbs;
            this.cert = cert;
            this.isWired = isWired;
        }
    }

    public class CertificateCache
    {
        public Certificate cert;
        //为了不频繁更新，设置为整型数
        public int time;
        public int authenticatedRSUId;
        public CertificateCache(Certificate cert, int time, int authenticatedRSUId)
        {
            this.cert = cert;
            this.time = time;
            this.authenticatedRSUId = authenticatedRSUId;
        }
    }    

    public class VANETReader : Reader
    {
        private VANETGlobal global;

        new public static VANETReader ProduceReader(int id, int org)
        {
            return new VANETReader(id, org);
        }

        public VANETReader(int id, int org)
            : base(id, org)
        {
            this.global = (VANETGlobal)Global.getInstance();
            int[] key = new int[32];
            key[0] = (int)NodeType.READER;
            key[1] = id;
            key[2] = 0;
            this.IssuedCertificate = new Certificate(id, key, Certificate.RootCA.CAId, Certificate.RootCA.CAPubKey);
            this.RSUCache = new Dictionary<int, RSUEntity>();
            this.CertificateCache = new Dictionary<string, CertificateCache>();
            this.NeighborBackbones = new Dictionary<int, int>();
            this.pendingCerterficatingObjects = new Dictionary<int, float>();
        }

        public bool IsWired = false;
        public Certificate IssuedCertificate;
        private Dictionary<int, RSUEntity> RSUCache;
        //for backbone node
        private float lastSentRSUNewBackboneRequest = -1;
        //for ordinary node
        private float lastSentRSUJoinRequest = -1;
        private Dictionary<string, CertificateCache> CertificateCache;
        public Dictionary<int, int> NeighborBackbones;
        public Dictionary<int, float> pendingCerterficatingObjects;

        HashSet<int> prefetchingCertIds = new HashSet<int>();



        public void RecvNewBackboneRequest(Packet pkg)
        {
            if (pkg.Next != this.Id)
                return;
            if (pkg.Dst == Id)
            {
                VANETNewBackboneRequestField req = pkg.VANETNewBbReq;
                //if (req.backboneCert.CAId != Certificate.RootCA.CAId || req.backboneCert.CAPubKey != Certificate.RootCA.CAPubKey)
                if (req.backboneCert.CAId != Certificate.RootCA.CAId)
                {
                    Console.WriteLine("Auth error! NewBackboneRequest CA is ({0},{1})", req.backboneCert.CAId, req.backboneCert.CAPubKey);
                    return;
                }

                Console.WriteLine("{0:F4} [{1}] {2}{3} is selected as a new gateway", scheduler.currentTime, "NEW_NETWORK", this.type, this.Id);
                this.IsGateway = true;
                VANETServer server = VANETServer.getInstance();
                server.BackboneNodeDB.Add(this);
                server.BackboneNodeMapping[this.Id] = server.BackboneNodeDB.Count;
                this.gatewayEntities[-1] = new GatewayEntity(this.Id, this.Id, 0); //key is default 0

                Packet pkg1 = new Packet(this, global.readers[pkg.Src], PacketType.RSU_NEW_BACKBONE_RESPONSE);
                pkg1.VANETNewBbRsp = new VANETNewBackboneResponseField(this.IssuedCertificate);
                RoutePacket(pkg1);
                return;
            }
            else
                RoutePacket(pkg);
        }


        public void RecvNewBackboneResponse(Packet pkg)
        {

            if (pkg.Next != this.Id)
                return;
            if (pkg.Dst == Id)
            {
                //TODO
                return;
            }
            else
                RoutePacket(pkg);
        }

        public Reader GetNewBackboneHead()
        {
            //Reader r = global.readers[this.wiredNodeCache[count - 1]];
            int index = 0;
            List<RSUEntity> sortedWiredNodeCache = new List<RSUEntity>();
            foreach (RSUEntity e in this.RSUCache.Values)
            {
                if (e.isWired)
                    sortedWiredNodeCache.Add(e);
            }
            if (sortedWiredNodeCache.Count == 0)
                return null;

            if (global.vanetNetworkGenMethod == NetworkGenMethods.Random)
                index = (int)Utility.U_Rand(sortedWiredNodeCache.Count);
            else if (global.vanetNetworkGenMethod == NetworkGenMethods.MaxHops)
            {
                sortedWiredNodeCache.Sort(VANETComparision.HopComparior);
                index = sortedWiredNodeCache.Count - 1;
            }
            else if (global.vanetNetworkGenMethod == NetworkGenMethods.HalfHops)
            {
                sortedWiredNodeCache.Sort(VANETComparision.HopComparior);
                index = (sortedWiredNodeCache.Count - 1)/2;
            }
            else if (global.vanetNetworkGenMethod == NetworkGenMethods.MaxNeigbhors)
            {
                //选出2/3个hop最长的节点中邻居最大的节点
                sortedWiredNodeCache.Sort(VANETComparision.HopComparior);
                int mnb = sortedWiredNodeCache[sortedWiredNodeCache.Count - 1].nbs;
                index = sortedWiredNodeCache.Count - 1;
                for (int i = sortedWiredNodeCache.Count; i < sortedWiredNodeCache.Count*2/3; i--)
                {
                    if (mnb < sortedWiredNodeCache[i].nbs)
                    {
                        mnb = sortedWiredNodeCache[i].nbs;
                        index = i;
                    }
                }
                //sortedWiredNodeCache.Sort(VANETComparision.NeighborComparior);
                //index = sortedWiredNodeCache.Count - 1;
            }
            return global.readers[sortedWiredNodeCache[index].id];
        }

        public void RecvRSUJoin(Packet pkg)
        {
            if (pkg.Next != this.Id)
                return;
            if (pkg.Dst == Id)
            {
                VANETRSUJoinField join = pkg.VANETRSUJoin;
                if (this.RSUCache.ContainsKey(join.id))
                    return;
                this.RSUCache.Add(join.id, new RSUEntity(join.id, join.hops, join.nbs, join.cert, join.isWired));

                if (this.RSUCache.Count > global.vanetNetworkSize)
                {
                    if (this.lastSentRSUNewBackboneRequest > 0 && scheduler.currentTime - this.lastSentRSUNewBackboneRequest < 2)//timeout is 3.
                        return;

                    Reader r = GetNewBackboneHead();
                    if (r == null)
                    {
                        Console.WriteLine("Unable to find a new backbone head");
                        return;
                    }

                    Packet pkg1 = new Packet(this, global.readers[r.Id], PacketType.RSU_NEW_BACKBONE_REQUEST);
                    pkg1.VANETNewBbReq = new VANETNewBackboneRequestField(this.IssuedCertificate);
                    if (pkg1.TTL < this.RSUCache[r.Id].hops)
                        pkg1.TTL = this.RSUCache[r.Id].hops+1;
                    RoutePacket(pkg1);
                    this.lastSentRSUNewBackboneRequest = scheduler.currentTime;
                    this.RSUCache.Remove(r.Id);
                }
            }
            else
                RoutePacket(pkg);
        }

        override public void SendBeacon(float time)
        {
            Packet pkg = new Packet();
            pkg.Type = PacketType.BEACON;
            pkg.Src = pkg.Prev = Id;
            pkg.Dst = pkg.Next = -1;//Broadcast
            pkg.TTL = 1;

            pkg.VANETBeacon = new VANETBeaconField();
            if (this.gatewayEntities.Count > 0)
            {
                pkg.VANETBeacon.backboneEntity = new GatewayEntity(this.gatewayEntities[-1].gateway, this.Id, this.gatewayEntities[-1].hops);
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

            //添加该邻居
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

            if (!this.routeTable.ContainsKey(node.Id))
                this.routeTable.Add(node.Id, new RouteEntity(pkg.Prev, pkg.Prev, 1, scheduler.currentTime, scheduler.currentTime));
            else
            {
                this.routeTable[node.Id].hops = 1;
                this.routeTable[node.Id].next = pkg.Prev;
                this.routeTable[node.Id].remoteLastUpdatedTime = scheduler.currentTime;
                this.routeTable[node.Id].localLastUpdatedTime = scheduler.currentTime;
            }

            if (pkg.VANETBeacon.backboneEntity != null)
                NeighborBackbones[node.Id] = pkg.VANETBeacon.backboneEntity.gateway;

            int tempbackbone = -1;
            if (gatewayEntities.Count > 0)
                tempbackbone = this.gatewayEntities[-1].gateway;
            if (pkg.VANETBeacon != null && pkg.VANETBeacon.backboneEntity != null)
            {
                GatewayEntity entity = pkg.VANETBeacon.backboneEntity;
                if (this.gatewayEntities.Count == 0)
                {
                    this.gatewayEntities[-1] = new GatewayEntity(entity.gateway, entity.next, entity.hops + 1);
                    Console.WriteLine("{0:F4} [{1}] {2}{3} add a gateway of {4} hops {5}.", scheduler.currentTime, pkg.Type, this.type, this.Id, entity.gateway, entity.hops);                   

                }
                else if (entity.gateway != this.gatewayEntities[-1].gateway && this.gatewayEntities[-1].hops > entity.hops + 1)
                {
                    this.gatewayEntities[-1].gateway = entity.gateway;
                    this.gatewayEntities[-1].hops = entity.hops + 1;
                    this.gatewayEntities[-1].next = entity.next;
                    Console.WriteLine("{0:F4} [{1}] {2}{3} update a gateway of {4} hops {5}.", scheduler.currentTime, pkg.Type, this.type, this.Id, entity.gateway, entity.hops);
                }

                if (!this.routeTable.ContainsKey(entity.gateway))
                    this.routeTable.Add(entity.gateway, new RouteEntity(entity.gateway, entity.next, entity.hops + 1, scheduler.currentTime, scheduler.currentTime));
                else if (this.routeTable[entity.gateway].hops > entity.hops + 1)
                {
                    this.routeTable[entity.gateway].hops = entity.hops + 1;
                    this.routeTable[entity.gateway].next = entity.next;
                }

                //Send a confirm to the gateway
                if (this.lastSentRSUJoinRequest > 0 && tempbackbone == this.gatewayEntities[-1].gateway)
                    return;
                if (tempbackbone != -1 && tempbackbone != this.gatewayEntities[-1].gateway)
                    Console.WriteLine("{0:F4} [{1}] {2}{3} changed gateway", scheduler.currentTime, "CHANGE_NETWORK", this.type, this.Id);
                Packet pkg1 = new Packet(this, global.readers[this.gatewayEntities[-1].gateway], PacketType.RSU_JOIN);
                pkg1.Next = pkg.Prev;
                pkg1.VANETRSUJoin = new VANETRSUJoinField(Id, this.gatewayEntities[-1].hops, this.Neighbors.Count, IssuedCertificate, this.IsWired);
                SendPacketDirectly(scheduler.currentTime, pkg1);
                this.lastSentRSUJoinRequest = scheduler.currentTime;
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
                    //this.NearbyObjectCache.Add(obj.Id, scheduler.CurrentTime);
                    this.NearbyObjectCache.Add(obj.Id, new ObjectEntity(obj.Id, -1, scheduler.currentTime));
                }
                else
                {
                    if (scheduler.currentTime - this.NearbyObjectCache[obj.Id].time < 1)//TODO 1 is fixed...
                        continue;
                    this.NearbyObjectCache[obj.Id].time = scheduler.currentTime;
                }
                //认证过程还在进行中
                if (this.pendingCerterficatingObjects.ContainsKey(obj.Id))
                    continue;
                Packet pkg = new Packet(this, obj, PacketType.CERTIFICATE_REQ);
                pkg.VANETCertificate = new Certificate(obj.Id);
                pkg.Data = scheduler.currentTime;
                SendPacketDirectly(scheduler.currentTime, pkg);
                this.pendingCerterficatingObjects.Add(obj.Id, scheduler.currentTime);
            }
        }



        public void RecvCertificate(Packet pkg)
        {

            if (pkg.Next != this.Id)
                return;

            //rsu收到obu的证书，进行验证
            if (pkg.SrcType != NodeType.OBJECT)
            {
                Console.WriteLine("Wrong prev type!");
                return;
            }

            string key = pkg.VANETCertificate.getStrPubKey();

            Certificate c = new Certificate(pkg.VANETCertificate.Id, pkg.VANETCertificate.PubKey, pkg.VANETCertificate.CAId, pkg.VANETCertificate.CAPubKey);
            float delay = GetCheckCertificateDelay(c);

            //如果本地缓存中没有证书，则向ca请求；不是由本节点认证的话，直接验证（delay=0）
            if (delay > 0.0001f)
            {
                CertificateArg arg = new CertificateArg(c, CertificateMethod.REMOTE_AUTH);
                Console.WriteLine("---------------------------------");
                Event.AddEvent(new Event(scheduler.currentTime + delay, EventType.CHK_CERT, this, arg));
                return;
            }

            if(this.CertificateCache[key].authenticatedRSUId != this.Id)
            {
                CertificateArg arg = new CertificateArg(c, CertificateMethod.LOCAL);
                Console.WriteLine("---------------------------------");
                Event.AddEvent(new Event(scheduler.currentTime + delay, EventType.CHK_CERT, this, arg));
            }
            else//否则直接通过
            {
                //认证完毕之后删除
                float starttime = this.pendingCerterficatingObjects[c.Id];
                this.pendingCerterficatingObjects.Remove(c.Id);

                Packet pkg1 = new Packet(this, global.objects[c.Id], PacketType.DATA_AVAIL);
                pkg1.Data = starttime;
                SendPacketDirectly(scheduler.currentTime, pkg1);
            }
            if (IsPreFetchCertificate(c))
            {
                CertificateArg arg = new CertificateArg(c, CertificateMethod.REMOTE_RETR);
                Console.WriteLine("prefetch---------------------------------");
                Event.AddEvent(new Event(scheduler.currentTime + global.checkCertDelay, EventType.CHK_CERT, this, arg));
                this.prefetchingCertIds.Add(c.Id);
            }
        }


        public void CheckCertificate(CertificateArg arg)
        {
            Certificate c = arg.cert;
            CertificateMethod method = arg.method;
            //从CA中获得验证证书的结果
            string key = c.getStrPubKey();
            c.authedRSUId = this.Id;


            Console.WriteLine("fetched cert READER{0}---------------------------------{1}", this.Id ,method);
            if (this.prefetchingCertIds.Contains(c.Id))
                this.prefetchingCertIds.Remove(c.Id);

            //认证
            if (method != CertificateMethod.REMOTE_RETR)
            {
                //如果缓存本来就有证书，成功
                if (this.CertificateCache.ContainsKey(key))
                {
                    //认证完毕之后删除
                    float starttime = this.pendingCerterficatingObjects[c.Id];
                    this.pendingCerterficatingObjects.Remove(c.Id);

                    Packet pkg1 = new Packet(this, global.objects[c.Id], PacketType.CERTIFICATE_OK);
                    pkg1.Data = starttime;
                    SendPacketDirectly(scheduler.currentTime, pkg1);

                    if (method != CertificateMethod.LOCAL)
                        this.CertificateCache[key].time = (int)scheduler.currentTime;
                    //将该节点标记为已由自己认证
                    this.CertificateCache[key].authenticatedRSUId = this.Id;
                }
                //从ca取回的证书是正确的
                else if (c.IsValid())
                {
                    this.CertificateCache.Add(key, new CertificateCache(c, (int)scheduler.currentTime, this.Id));
                    //将该节点标记为已由自己认证
                    this.CertificateCache[key].authenticatedRSUId = this.Id;

                    //认证完毕之后删除
                    float starttime = this.pendingCerterficatingObjects[c.Id];
                    this.pendingCerterficatingObjects.Remove(c.Id);

                    Packet pkg1 = new Packet(this, global.objects[c.Id], PacketType.CERTIFICATE_OK);
                    pkg1.Data = starttime;
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                }
                //证书不正确
                else
                {
                    Packet pkg1 = new Packet(this, global.objects[c.Id], PacketType.CERTIFICATE_FAIL);
                    SendPacketDirectly(scheduler.currentTime, pkg1);
                    return;
                }
            }
            else
            {
                this.CertificateCache[key].time = (int)scheduler.currentTime;
                //将该节点标记为已由自己认证
                this.CertificateCache[key].authenticatedRSUId = this.Id;
            }


            //forward certificate cache
            if (global.vanetCaForward == true)
            {
                Packet pkg2 = new Packet(this, BroadcastNode.Node, PacketType.RSU_CA_FORWARD);
                pkg2.TTL = 5;
                pkg2.VANETCaForward = new VANETCAForwardField(this.IssuedCertificate, this.CertificateCache[key].cert, this.CertificateCache[key].time, pkg2.TTL, this.Id);
                SendPacketDirectly(scheduler.currentTime, pkg2);
            }
        }


        public float GetCheckCertificateDelay(Certificate cert)
        {
            string key = cert.getStrPubKey();
            //证书缓存中有该项
            if (this.CertificateCache.ContainsKey(key) && scheduler.currentTime - this.CertificateCache[key].time < 10)
                return 0;
            return global.checkCertDelay;
        }


        public bool IsPreFetchCertificate(Certificate cert)
        {
            //已经在取了，取消
            if (this.prefetchingCertIds.Contains(cert.Id))
                return false;
            string key = cert.getStrPubKey();
            //证书缓存中有该项
            //0.3f是一个较小的值
            if (this.CertificateCache.ContainsKey(key) && scheduler.currentTime - this.CertificateCache[key].time > 10-global.checkCertDelay-0.3f)
                return true;
            else
                return false;
        }

        public void RecvCertificateForward(Packet pkg)
        {
            if (!this.Neighbors.ContainsKey(pkg.Prev))
                return;


            if (pkg.SrcType != NodeType.READER)
            {
                Console.WriteLine("Wrong prev type!");
                return;
            }


            //no such a neighbor or not in the same backbone network, just ignore.
            if (!this.NeighborBackbones.ContainsKey(pkg.Prev) || this.gatewayEntities[-1].gateway != this.NeighborBackbones[pkg.Prev])
                return;
            Certificate rsuCert = pkg.VANETCaForward.rsuCA;
            Certificate objCert = pkg.VANETCaForward.objCA;
            int time = pkg.VANETCaForward.time;
            int src = pkg.VANETCaForward.src;
            int hops = pkg.VANETCaForward.hops;

            string key = objCert.getStrPubKey();
            if (this.CertificateCache.ContainsKey(key))
            {
                if (this.CertificateCache[key].time < time)
                {
                    this.CertificateCache[key].time = time;
                    this.CertificateCache[key].cert = objCert;
                    this.CertificateCache[key].authenticatedRSUId = src;
                }
                else
                    return;
            }
            else
                this.CertificateCache.Add(key, new CertificateCache(objCert, (int)scheduler.currentTime, src));

            if (hops - 1 < 0)
                return;
            Packet pkg1 = new Packet(this, BroadcastNode.Node, PacketType.RSU_CA_FORWARD);
            pkg1.TTL = hops - 1;
            pkg1.VANETCaForward = new VANETCAForwardField(pkg.VANETCaForward.rsuCA, pkg.VANETCaForward.objCA, time, pkg.VANETCaForward.hops - 1, src);
            SendPacketDirectly(scheduler.currentTime, pkg1);
        }

        public static void ComputeNetworkDetail()
        {
            Dictionary<int, int> networkCount = new Dictionary<int, int>();
            Scheduler scheduler = Scheduler.getInstance();
            Global global = Global.getInstance();
            foreach(VANETReader r in global.readers)
            {
                //孤立节点
                if (r.gatewayEntities.Count == 0)
                    continue;
                int gw = r.gatewayEntities[-1].gateway;
                if (networkCount.ContainsKey(gw))
                    networkCount[gw]++;
                else
                    networkCount.Add(gw, 1);
            }
            foreach (KeyValuePair<int, int> pair in networkCount)
            {
                Console.WriteLine("{0:F4} [{1}] READER{2}:{3}", scheduler.currentTime, "NETWORK_SIZE", pair.Key, pair.Value);
            }
        }

        public override void ProcessPacket(Packet pkg)
        {
            //I send the packet myself, ignore
            if (pkg.Prev == Id && pkg.PrevType == type)
            {
                return;
            }
            
            switch (pkg.Type)
            {
                case PacketType.BEACON:
                    RecvBeacon(pkg);
                    break;
                case PacketType.CERTIFICATE_REP:
                    RecvCertificate(pkg);
                    break;
                case PacketType.RSU_JOIN:
                    RecvRSUJoin(pkg);
                    break;
                case PacketType.RSU_NEW_BACKBONE_REQUEST:
                    RecvNewBackboneRequest(pkg);
                    break;
                case PacketType.RSU_NEW_BACKBONE_RESPONSE:
                    RecvNewBackboneResponse(pkg);
                    break;
                case PacketType.RSU_CA_FORWARD:
                    RecvCertificateForward(pkg);
                    break;
                //Some codes are hided in the base class.
                default:
                    base.ProcessPacket(pkg);
                    return;
            }
            pkg.TTL -= 1;
            if (pkg.TTL < 0)
                Drop(pkg);
        }
    }
}
