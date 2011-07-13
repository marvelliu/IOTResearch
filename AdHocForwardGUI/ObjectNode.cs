using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace AdHocBaseApp
{
    public delegate ObjectNode ObjectNodeConstructor(int id);

    public class ObjectNode:MobileNode
    {
        Location currentLandmarkLocation;
        protected float lastLocationUpdate;
        public Reader lastNearReader;
        protected float lastNearReaderTime;
        protected List<Packet> cachePackets;
        public int OrgId;
        protected int sentPacketCount = 0;
        private Global global;

        public static ObjectNode ProduceObjectNode(int id)
        {
            return new ObjectNode(id);
        }

        protected ObjectNode(int id):base(id)
        {
            this.type = NodeType.OBJECT;
            this.currentLandmarkLocation = new Location();
            this.lastLocationUpdate = 0;
            this.lastNearReader = null;
            this.lastNearReaderTime = -1;
            this.cachePackets = new List<Packet>();
            this.global = Global.getInstance();
        }

        public List<Reader> GetAllNearReaders(float distance)
        {
            Global global = Global.getInstance();
            List<Reader> list = new List<Reader>();
            for (int i = 0; i < global.readers.Length; i++)
            {
                double x = global.readers[i].X - this.X;
                double y = global.readers[i].Y - this.Y;
                if (x < distance && x > 0 - distance
                    && y < distance && x > 0 - distance
                    && Utility.Distance((MobileNode)(global.readers[i]), (MobileNode)this) < distance
                    )
                    list.Add(global.readers[i]);
            }
            return list;
        }

        public override void SendPacketDirectly(float time, Packet pkg)
        {
            float recv_time = 0;
            pkg.Prev = Id;
            Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()));


            //sentPacketCount和packetSeq是一样的
            this.packetSeq++;
            this.sentPacketCount++;

            if (pkg.Next == -1) //Broadcast
                return;//No such a case.
            else
            {
                pkg.PacketSeq = this.sentPacketCount;
                pkg.SenderSeq = this.packetSeq;

                MobileNode node = null;
                switch (pkg.NextType)
                {
                    case NodeType.READER:
                        node = global.readers[pkg.Next];
                        List<Reader> list = GetAllNearReaders(global.objectMaxDist);
                        if (!list.Contains(node))
                            list.Add((Reader)node);
                        for (int i = 0; i < list.Count; i++)
                        {
                            Packet pkg1 = pkg.Clone() as Packet;
                            pkg1.SenderSeq = this.packetSeq;
                            pkg1.DelPacketNode = list[0].Id;
                            if (pkg.Src == Id)
                                pkg1.PacketSeq = this.packetSeq;

                            node = list[i];
                            recv_time = global.processDelay + (float)(Utility.Distance(this, node) / global.lightSpeed);
                            Event.AddEvent(
                                new Event(time + recv_time, EventType.RECV, node, pkg1));
                            if (global.debug)
                                Console.WriteLine("[Debug] object{0} sends to reader{1}", Id, node.Id);
                        }
                        break;
                    default:
                        Debug.Assert(false, "Error Next Type!");
                        break;
                }
            }
        }

        public override void Recv(Packet pkg)
        {
            if (pkg.PrevType != NodeType.READER)
            {
                Debug.Assert(false, "Wrong packet src");
            }
            Node node = global.readers[pkg.Prev];
            if (Utility.Distance(this, (MobileNode)node) > Global.getInstance().nodeMaxDist)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} Drop from {4}{5} out of space", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                return;
            }
            switch (pkg.Type)
            {
                case PacketType.LANDMARK:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    SetCurrentLandmarkLocation(pkg);
                    break;
                case PacketType.DATA_AVAIL:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    UpdateLocation(pkg);
                    List<Packet> removeList = new List<Packet>();
                    foreach (Packet p in this.cachePackets)
                    {
                        if (this.cachePackets.Contains(pkg) && TrySendData(p))
                        {
                            this.lastNearReader = this.global.readers[pkg.Src];
                            this.lastNearReaderTime = scheduler.currentTime;
                            removeList.Add(p);
                        }
                        else if (!this.cachePackets.Contains(pkg))
                            this.cachePackets.Add(pkg);
                    }
                    foreach (Packet p in removeList)
                        this.cachePackets.Remove(p);
                    break;
                default:
                    break;
            }
            return;
        }

        public virtual bool TrySendData(Packet pkg)
        {
            if (this.lastNearReader != null && scheduler.currentTime - this.lastNearReaderTime < 1)
            {
                pkg.Next = this.lastNearReader.Id;
                pkg.NextType = NodeType.READER;
                SendPacketDirectly(scheduler.currentTime, pkg);
                return true;
            }
            return false;
        }

        public override bool SendData(Packet pkg)
        {
            if (TrySendData(pkg)==false)
            {
                //这里如果发送失败就不重新试了
                //if (!this.cachePackets.Contains(pkg))
                //    this.cachePackets.Add(pkg);
                return false;
            }
            this.lastNearReader = this.global.readers[pkg.Src];
            this.lastNearReaderTime = scheduler.currentTime;
            return true;
        }


        public void SetCurrentLandmarkLocation(Packet pkg)
        {
            this.currentLandmarkLocation.X = pkg.LandmarkNotification.location.X;
            this.currentLandmarkLocation.Y = pkg.LandmarkNotification.location.Y;
        }


        public void UpdateLocation(Packet pkg)
        {
            float interval = scheduler.currentTime - this.lastLocationUpdate;
            if (interval < global.minLocationUpdateInterval)
                return;

            Reader reader = this.global.readers[pkg.Src];
            //if(reader == lastNearReader && interval<global.maxLocationUpdateInterval)
            if (interval < global.maxLocationUpdateInterval)
                return;

            Packet pkg1 = new Packet(this, global.readers[pkg.Src], PacketType.LOCATION_UPDATE);
            pkg1.ObjectUpdateLocation = new ObjectUpdateLocationField(
                currentLandmarkLocation, scheduler.currentTime, 0, Id);
            Event.AddEvent(new Event(scheduler.currentTime, EventType.RECV,
                global.readers[pkg.Src], pkg1));
            this.lastLocationUpdate = scheduler.currentTime;
            Console.WriteLine("{0:F4} [LOCATION_UPDATE] {1}{2} starts to update location", scheduler.currentTime, this.type, this.Id);
        }


    }
}
