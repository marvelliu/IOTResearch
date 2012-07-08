using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace AdHocBaseApp
{
    public class ObjectLocation
    {
        public int Id;
        public double X;
        public double Y;
        public double T;

        protected Global global = Global.getInstance();

        public ObjectLocation(int id, double x, double y, double t)
        {
            this.Id = id;
            this.X = x;
            this.Y = y;
            this.T = y;
        }
        public override string ToString()
        {
            return "(" + this.Id + "," + this.X + "," + this.Y + "," + this.T + ")";
        }
    }

    public delegate Server ServerConstructor();

    public class Server : Node
    {
        Dictionary<int, ObjectLocation> objectLocations;
        private Global global;

        protected static Server instance;

        protected Server():base(0)
        {
            this.global = Global.getInstance();
            this.type = NodeType.SERVER;
            this.objectLocations = new Dictionary<int, ObjectLocation>();
        }

        public static Server getInstance()
        {
            if(instance == null)
                instance = new Server();
            return instance;
        }

        ObjectLocation getLocation(int id)
        {            
            ObjectLocation location = this.objectLocations[id];
            return location;
        }

        void updateLocation(int id, double x, double y, double t)
        {
            ObjectLocation location = null;
            if (this.objectLocations.ContainsKey(id))
            {
                location = this.objectLocations[id];
                location.Id = id;
                location.X = x;
                location.Y = y;
                location.T = t;
            }
            else
            {
                location = new ObjectLocation(id, x, y, t);
                this.objectLocations.Add(id, location);
            }
                
        }

        public override void Recv(Packet pkg)
        {
            if (pkg.PrevType != NodeType.QUERIER &&
                pkg.PrevType != NodeType.READER)
            {
                Debug.Assert(false, scheduler.currentTime + " Server receive error type!");
            }

            if (pkg.PrevType == NodeType.READER)
            {
                switch (pkg.Type)
                {
                    case PacketType.LOCATION_UPDATE:
                        Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                        Console.WriteLine("{0:F4} [{1}] {2}{3} update location for {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, NodeType.OBJECT, pkg.ObjectUpdateLocation.obj);
                        updateLocation(pkg.ObjectUpdateLocation.obj, pkg.ObjectUpdateLocation.location.X,
                            pkg.ObjectUpdateLocation.location.Y, pkg.ObjectUpdateLocation.updateTime);
                        break;
                    case PacketType.DATA:
                        Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                        Console.WriteLine("{0:F4} [{1}] {2}{3} recv data from {4}{5}. total time: {6}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.SrcType, pkg.Src, scheduler.currentTime-pkg.beginSentTime);
                        break;

                }
            }
            else if (pkg.PrevType == NodeType.QUERIER)
            {
                switch (pkg.Type)
                {
                    case PacketType.LOCATION_QUERY:
                        Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                        SendObjectLocation(pkg);
                        break;
                }
            }
        }

        public void SendObjectLocation(Packet pkg)
        {
            Node querier = global.queriers[pkg.Src];
            Packet pkg1 = new Packet(this, querier, PacketType.LOCATION_QUERY);
            int obj = pkg.ObjectLocationRequest.obj;
            pkg1.ObjectLocationReply = new ObjectLocationQueryReplyField(obj, null);
            if (this.objectLocations.ContainsKey(obj))
                pkg1.ObjectLocationReply.location = this.objectLocations[obj];

            SendPacketDirectly(scheduler.currentTime, pkg1);
        }

        public override bool SendPacketDirectly(float time, Packet pkg)
        {
            pkg.Prev = Id;
            Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()));

            float recv_time = global.serverProcessDelay + global.internetDelay;
            if (pkg.Next == -1) //Broadcast
                return true;//No such a case.
            else
            {
                Node node = null;
                switch (pkg.NextType)
                {
                    case NodeType.READER:
                        node = global.readers[pkg.Next];
                        break;
                    case NodeType.QUERIER:
                        node = global.queriers[pkg.Next];
                        break;
                    case NodeType.OBJECT:
                        node = global.objects[pkg.Next];
                        break;
                    default:
                        Debug.Assert(false, "Error Next Type!");
                        break;
                }
                pkg.PrevType = type;
                pkg.Prev = Id;
                Event.AddEvent(
                    new Event(time + recv_time, EventType.RECV,
                        node, pkg));
            }
            return true;
        }
    }
}
