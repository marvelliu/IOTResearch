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
        public Certificate cert;
        public RSUEntity(int id, int hops, Certificate cert)
        {
            this.id = id;
            this.hops = hops;
            this.cert = cert;
        }
    }

    public class CertificateCache
    {
        public Certificate cert;
        public float time;
        public CertificateCache(Certificate cert, float time)
        {
            this.cert = cert;
            this.time = time;
        }
    }

    public class VANETReader : Reader
    {
        private Global global;

        new public static VANETReader ProduceReader(int id, int org)
        {
            return new VANETReader(id, org);
        }

        public VANETReader(int id, int org)
            : base(id, org)
        {
            this.global = Global.getInstance();
            int[] key = new int[32];
            key[0] = (int)NodeType.READER;
            key[1] = id;
            key[2] = 0;
            this.IssuedCertificate = new Certificate(id, key, Certificate.RootCA.CAId, Certificate.RootCA.CAPubKey);
            this.RSUCache = new Dictionary<int, RSUEntity>();
            this.CertificateCache = new Dictionary<string, CertificateCache>();
            this.NeighborBackbones = new Dictionary<int, int>();
            this.wiredNodeCache = new List<int>();
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
        
        private List<int> wiredNodeCache;



        public override void Recv(AdHocBaseApp.Packet pkg)
        {
            if (pkg.PrevType == NodeType.OBJECT || pkg.PrevType == NodeType.READER)
            {
                Node node = Node.getNode(pkg.Prev, pkg.PrevType);
                if (Utility.Distance(this, (MobileNode)node) > global.nodeMaxDist)
                {
                    if (pkg.Next == Id)
                        Console.WriteLine("{0:F4} [{1}] {2}{3} Drop data of {4}{5} due to out of space.", scheduler.currentTime, pkg.Type, this.type, this.Id, node.type, node.Id);
                    CheckPacketCount(pkg);
                    return;
                }
            }

            if (pkg.Next != Id || pkg.NextType != NodeType.READER)
                return;

            //Self, ignore
            if ((pkg.Next != Id && pkg.Next != Node.BroadcastNode.Id) || pkg.NextType != NodeType.READER)
                return;

            switch (pkg.Type)
            {
                case PacketType.BEACON:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvBeacon(pkg);
                    break;
                case PacketType.CERTIFICATE:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvCertificate(pkg);
                    break;
                case PacketType.RSU_JOIN:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvRSUJoin(pkg);
                    break;
                case PacketType.RSU_NEW_BACKBONE_REQUEST:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvNewBackboneRequest(pkg);
                    break;
                case PacketType.RSU_NEW_BACKBONE_RESPONSE:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvNewBackboneResponse(pkg);
                    break;
                case PacketType.RSU_CA_FORWARD:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvCertificateForward(pkg);
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

        public void RecvNewBackboneRequest(Packet pkg)
        {
            if (pkg.Dst == Id)
            {
                VANETNewBackboneRequestField req = pkg.VANETNewBbReq;
                if (req.backboneCert.CAId != Certificate.RootCA.CAId || req.backboneCert.CAPubKey != Certificate.RootCA.CAPubKey)
                {
                    Console.WriteLine("Auth error! NewBackboneRequest CA is ({0},{1})", req.backboneCert.CAId, req.backboneCert.CAPubKey);
                    return;
                }

                this.IsGateway = true;
                VANETServer server = VANETServer.getInstance();
                server.BackboneNodeDB.Add(this);
                server.BackboneNodeMapping[this.Id] = server.BackboneNodeDB.Count;
                this.gatewayEntities[-1] = new GatewayEntity(this.Id, this.Id, 0); //key is default 0

                Packet pkg1 = new Packet(this, global.readers[pkg.Src], PacketType.RSU_NEW_BACKBONE_RESPONSE);
                pkg1.VANETNewBbRsp = new VANETNewBackboneResponseField(this.IssuedCertificate);
                SendPacketDirectly(scheduler.currentTime, pkg1);
                return;
            }
            else
                RoutePacket(pkg);
        }


        public void RecvNewBackboneResponse(Packet pkg)
        {
            if (pkg.Dst == Id)
            {
                //TODO
                return;
            }
            else
                RoutePacket(pkg);
        }

        public void RecvRSUJoin(Packet pkg)
        {
            if (pkg.Dst == Id)
            {
                VANETRSUJoinField join = pkg.VANETRSUJoin;
                if (this.RSUCache.ContainsKey(join.id))
                    return;
                this.RSUCache.Add(join.id, new RSUEntity(join.id, join.hops, join.cert));
                if (join.isWired)
                    this.wiredNodeCache.Add(join.id); 
                                    
                if (this.RSUCache.Count > global.vanetNetworkSize)
                {
                    if (this.lastSentRSUNewBackboneRequest > 0 && scheduler.currentTime - this.lastSentRSUNewBackboneRequest < 2)//timeout is 3.
                        return;

                    int count = this.wiredNodeCache.Count;
                    if (count == 0)
                        return;

                    Reader r = global.readers[this.wiredNodeCache[count-1]];
                    this.wiredNodeCache.RemoveAt(count-1);

                    Packet pkg1 = new Packet(this, global.readers[r.Id], PacketType.RSU_NEW_BACKBONE_REQUEST);
                    pkg1.Next = pkg.Prev;
                    pkg1.VANETNewBbReq = new VANETNewBackboneRequestField(this.IssuedCertificate);
                    SendPacketDirectly(Scheduler.getInstance().currentTime, pkg1);
                    this.lastSentRSUNewBackboneRequest = scheduler.currentTime;
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
            if (pkg.VANETBeacon.backboneEntity != null)
                NeighborBackbones[node.Id] = pkg.VANETBeacon.backboneEntity.gateway;

            int tempbackbone = -1;
            if(gatewayEntities.Count>0)
                tempbackbone = this.gatewayEntities[-1].gateway;
            if (pkg.VANETBeacon != null && pkg.VANETBeacon.backboneEntity != null)
            {
                GatewayEntity entity = pkg.VANETBeacon.backboneEntity;
                if (this.gatewayEntities.Count==0)
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
                //Send a confirm to the gateway
                if (this.lastSentRSUJoinRequest > 0 && tempbackbone==this.gatewayEntities[-1].gateway)
                    return;
                Packet pkg1 = new Packet(this, global.readers[this.gatewayEntities[-1].gateway], PacketType.RSU_JOIN);
                pkg1.Next = pkg.Prev;
                pkg1.VANETRSUJoin = new VANETRSUJoinField(Id, this.gatewayEntities[-1].hops, IssuedCertificate, this.IsWired);
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
                Packet pkg = new Packet(this, obj, PacketType.CERTIFICATE);
                pkg.VANETCertificate = new Certificate(obj.Id);
                SendPacketDirectly(scheduler.currentTime, pkg);
                    
            }
        }



        public void RecvCertificate(Packet pkg)
        {
            if (pkg.SrcType != NodeType.OBJECT)
            {
                Console.WriteLine("Wrong prev type!");
                return;
            }

            string key = pkg.VANETCertificate.getStrPubKey();
            Certificate c = new Certificate(pkg.VANETCertificate.Id, pkg.VANETCertificate.PubKey, pkg.VANETCertificate.CAId, pkg.VANETCertificate.CAPubKey);
            float delay = GetCheckCertificateDelay(c);
            if (global.vanetCaForward == "none" || !this.CertificateCache.ContainsKey(key))
            {
                Event.AddEvent(new Event(scheduler.currentTime + delay, EventType.CHK_CERT, this, c));
            }
            else
            {

                Packet pkg1 = new Packet(this, global.objects[c.Id], PacketType.DATA_AVAIL);
                SendPacketDirectly(scheduler.currentTime, pkg1);
                return;
            }
        }

        public void CheckCertificate(Certificate c)
        {
            string key = c.getStrPubKey();
            if (this.CertificateCache.ContainsKey(key))
            {
                Packet pkg1 = new Packet(this, global.objects[c.Id], PacketType.DATA_AVAIL);
                SendPacketDirectly(scheduler.currentTime, pkg1);
                return;
            }
            else if (c.IsValid())
            {
                this.CertificateCache.Add(key, new CertificateCache(c, scheduler.currentTime));

                Packet pkg1 = new Packet(this, global.objects[c.Id], PacketType.DATA_AVAIL);
                SendPacketDirectly(scheduler.currentTime, pkg1);


                //forward certificate cache
                if (global.vanetCaForward == "none")
                    return;
                else
                {
                    Packet pkg2 = new Packet(this, Node.BroadcastNode, PacketType.RSU_CA_FORWARD);
                    pkg2.TTL = 1;
                    int hops = 10;//TODO
                    pkg2.VANETCaForward = new VANETCAForwardField(this.IssuedCertificate, this.CertificateCache[key].cert, hops);
                    SendPacketDirectly(scheduler.currentTime, pkg2);
                }
            }
            else
            {
                Packet pkg1 = new Packet(this, global.objects[c.Id], PacketType.CERTIFICATE_FAIL);
                SendPacketDirectly(scheduler.currentTime, pkg1);
            }
        }


        public float GetCheckCertificateDelay(Certificate cert)
        {
            if (this.CertificateCache.ContainsKey(cert.getStrPubKey()))
                return 0;
            return global.checkCertDelay;
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

            string key = objCert.getStrPubKey();
            if(this.CertificateCache.ContainsKey(key))
                return;
            this.CertificateCache.Add(key, new CertificateCache(objCert, scheduler.currentTime));

            if (pkg.VANETCaForward.hops-1 == 0)
                return;
            Packet pkg1 = new Packet(this, Node.BroadcastNode, PacketType.RSU_CA_FORWARD);
            pkg1.TTL = 1;
            pkg1.VANETCaForward = new VANETCAForwardField(pkg.VANETCaForward.rsuCA, pkg.VANETCaForward.objCA, pkg.VANETCaForward.hops - 1);
            SendPacketDirectly(scheduler.currentTime, pkg1);
        }
    }
}
