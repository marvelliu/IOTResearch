using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace AdHocBaseApp
{
    public delegate Querier QuerierConstructor(int id);
    
    public class Querier:Node
    {
        public static Querier ProduceQuerier(int id)
        {
            return new Querier(id);
        }
        public Querier(int id)
            : base(id)
        {
            this.global = Global.getInstance();
            this.type = NodeType.QUERIER;
        }
        protected float queryingTime = -1;
        private Global global;

        public override void SendPacketDirectly(float time, Packet pkg)
        {
            float recv_time = global.processDelay + global.internetDelay;
            pkg.Prev = Id;
            Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()));

            if (pkg.Next == -1) //Broadcast
                return;//No such a case.
            else
            {
                Node node = null;
                switch (pkg.NextType)
                {
                    case NodeType.READER:
                        node = global.readers[pkg.Next];
                        break;
                    case NodeType.SERVER:
                        node = global.server;
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
        }

        public override void Recv(Packet pkg)
        {
            if (pkg.PrevType != NodeType.SERVER )
            {
                System.Console.WriteLine("Server receive error type!");
                return;
            }
            else if (pkg.PrevType == NodeType.SERVER)
            {
                switch (pkg.Type)
                {
                    case PacketType.LOCATION_QUERY:
                        Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}, total time:{6}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev, scheduler.currentTime-this.queryingTime);
                        GetQueryResponse(pkg);
                        break;
                }
            }
        }

        public void SendQueryRequest(int obj)
        {
            Packet pkg = new Packet(this, global.server, PacketType.LOCATION_QUERY);
            pkg.ObjectLocationRequest = new ObjectLocationQueryRequestField(obj);
            SendPacketDirectly(scheduler.currentTime, pkg);
            this.queryingTime = scheduler.currentTime;
            //Console.WriteLine("{0:F4} [LOCATION_QUERY] {1}{2} starts to query the server.", scheduler.CurrentTime, this.type, this.id);
        }
        public void GetQueryResponse(Packet pkg)
        {
            if (pkg.ObjectLocationReply == null)
                Console.WriteLine("{0:F4} [LOCATION_QUERY] {1}{2} Querying result: Object {3} not found.", scheduler.currentTime, this.type, this.Id, pkg.ObjectLocationReply.obj);
            else
                Console.WriteLine("{0:F4} [LOCATION_QUERY] {1}{2} Querying result: Object {3}.", scheduler.currentTime, this.type, this.Id, pkg.ObjectLocationReply.ToString());
            this.queryingTime = -1;
        }
    }
}
