using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LogicalPath
{
    class LogicalPathObjectNode:ObjectNode
    {

        new public static LogicalPathObjectNode ProduceObjectNode(int id)
        {
            return new LogicalPathObjectNode(id);
        }

        protected LogicalPathObjectNode(int id)
            : base(id)
        { }


        public override void Recv(Packet pkg)
        {
            if (pkg.PrevType != NodeType.READER)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} drop from {4}{5} due to wrong node type", scheduler.currentTime, pkg.Type, this.type,this.Id, pkg.PrevType, pkg.Prev);
                return;
            }

            Reader node = global.readers[pkg.Prev];
            if (Utility.Distance(this, (MobileNode)node) > Global.getInstance().objectMaxDist)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} Drop {4}{5} due to out of space", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                return;
            }

            switch (pkg.Type)
            {
                case PacketType.LOGICAL_PATH_REQUEST:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    RecvQueryRequest(pkg);
                    break;
                case PacketType.DATA_AVAIL: //Override the link path update
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    UpdateLogicalPath(pkg);
                    break;
                default:
                    base.Recv(pkg);
                    return;
            }
            return;
        }


        public void UpdateLogicalPath(Packet pkg)
        {
            float interval = scheduler.currentTime - this.lastLocationUpdate;
            if (interval < global.minLocationUpdateInterval)
                return;

            Reader reader = this.global.readers[pkg.Src];
            //if (reader == lastNearReader && interval < global.maxLocationUpdateInterval)
            if (interval < global.maxLocationUpdateInterval)
                return;

            Packet pkg1 = new Packet(this, global.server, PacketType.LOGICAL_PATH_UPDATE);
            pkg1.ObjectLogicalPathUpdate = new ObjectLogicalPathUpdateField(Id);
            pkg1.ObjectLogicalPathUpdate.t = scheduler.currentTime;
            pkg1.ObjectLogicalPathUpdate.s = global.server.Id;
            pkg1.Next = reader.Id;
            pkg1.NextType = NodeType.READER;
            SendPacketDirectly(scheduler.currentTime, pkg1);
            this.lastLocationUpdate = scheduler.currentTime;
            this.lastNearReader = reader;
            //Console.WriteLine("{0:F4} [{1}] {2}{3} starts to update logical path", scheduler.CurrentTime,pkg1.Type, this.type, this.id);
        }


        public void RecvQueryRequest(Packet pkg)
        {
            ObjectLogicalPathQueryRequestField f = pkg.ObjectLogicalPathQueryReqeust;
            Reader gw = global.readers[f.gateway];
            Packet reply = new Packet(this, gw, PacketType.LOGICAL_PATH_REPLY);
            reply.NextType = NodeType.READER;
            reply.Next = pkg.Prev;
            reply.ObjectLogicalPathQueryReply = new ObjectLogicalPathQueryReplyField();
            reply.ObjectLogicalPathQueryReply.gateway = f.gateway;
            reply.ObjectLogicalPathQueryReply.obj = Id;
            reply.ObjectLogicalPathQueryReply.querier = pkg.Src;
            //TODO, x and y should be acquired from landmark reader...
            reply.ObjectLogicalPathQueryReply.shape = new PointShape(this.x, this.y);

            //TODO fill the shape.
            //do some stuf here
            SendPacketDirectly(scheduler.currentTime, reply);
        }
    }
}
