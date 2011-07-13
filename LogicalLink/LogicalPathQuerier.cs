using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LogicalPath
{
    class LogicalPathQuerier:Querier
    {
        private Global global;
        new public static LogicalPathQuerier ProduceQuerier(int id)
        {
            return new LogicalPathQuerier(id);
        }

        public LogicalPathQuerier(int id)
            : base(id)
        {
            this.global = Global.getInstance();
        }

        public override void Recv(Packet pkg)
        {
            Node node = Node.getNode(pkg.Prev, pkg.PrevType);
            if (node.type == NodeType.SERVER) 
            {
                switch (pkg.Type)
                {
                    case PacketType.LOCATION_QUERY:
                        Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                        GetQueryResponseFromServer(pkg);
                        break;
                }
            }
            else if (node.type == NodeType.READER)
            {
                switch (pkg.Type)
                {
                    case PacketType.LOGICAL_PATH_REPLY:
                        GetQueryResponseFromReader(pkg);
                        break;
                }
            }
        }

        public void GetQueryResponseFromServer(Packet pkg)
        {
            if (pkg.ObjectLogicalPathQueryServerReply == null)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} Querying result: Object not found.", scheduler.currentTime, pkg.Type, this.type, this.Id);
                return;
            }
            else
                Console.WriteLine("{0:F4} [{1}] {2}{3} Querying result: Object {4}.", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.ObjectLogicalPathQueryServerReply.ToString());
            
            ObjectLogicalPathQueryServerReplyField f = pkg.ObjectLogicalPathQueryServerReply;
            if (scheduler.currentTime - f.time > global.maxLocationUpdateInterval * 2)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} Query abort: Object {4} move time out.", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.ObjectLogicalPathQueryServerReply.obj);
                return;
            }
            Packet pkg1 = new Packet(this, global.readers[f.reader], PacketType.LOGICAL_PATH_REQUEST);
            pkg1.Next = f.gateway;
            pkg1.NextType = NodeType.READER;
            pkg1.ObjectLogicalPathQueryReqeust = new ObjectLogicalPathQueryRequestField();
            pkg1.ObjectLogicalPathQueryReqeust.gateway = f.gateway;
            pkg1.ObjectLogicalPathQueryReqeust.reader = f.reader;
            pkg1.ObjectLogicalPathQueryReqeust.obj = f.obj;
            SendPacketDirectly(scheduler.currentTime, pkg1);
        }


        public void GetQueryResponseFromReader(Packet pkg)
        {
            if (pkg.ObjectLogicalPathQueryReply == null)
                Console.WriteLine("{0:F4} [{1}] {2}{3} Querying result: Object not found.", scheduler.currentTime, pkg.Type, this.type, this.Id);
            else
                Console.WriteLine("{0:F4} [{1}] {2}{3} Querying result: Object {4}, total time:{5}.", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.ObjectLogicalPathQueryReply, scheduler.currentTime - this.queryingTime);
            this.queryingTime = -1;

        }
    }
}
