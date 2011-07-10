using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LogicalPath
{
    public class ReversePath
    {
        public int r;
        public int prev;
        public float time;

        public ReversePath(int r, int prev, float time)
        {
            this.r = r;
            this.prev = prev;
            this.time = time;
        }
    }

    class LogicalPathReader : Reader
    {
        private Dictionary<string, ReversePath> reversePathCache;

        new public static LogicalPathReader ProduceReader(int id, int org)
        {
            return new LogicalPathReader(id, org);
        }

        public LogicalPathReader(int id, int org)
            : base(id, org)
        {
            this.reversePathCache = new Dictionary<string, ReversePath>();
            Event.AddEvent(new Event(scheduler.currentTime, EventType.CHK_REV_PATH_CACHE, this, null));
        }

        public void ClearOldReverseCache()
        {
            List<string> temp = new List<string>();
            foreach (KeyValuePair<string, ReversePath> a in this.reversePathCache)
            {
                ReversePath p = a.Value;
                if (p.time < scheduler.currentTime - global.ReversePathCacheTimeout)
                    temp.Add(a.Key);
            }
            foreach (string s in temp)
                this.reversePathCache.Remove(s); 
        }


        public void CheckReversePathCache(Packet pkg)
        {
            int r = pkg.ObjectLogicalPathUpdate.r;
            int o = pkg.ObjectLogicalPathUpdate.obj;
            string key = r + "_" + o;
            int prev = pkg.Prev;
            if (this.reversePathCache.ContainsKey(key))
            {
                this.reversePathCache[key].time = scheduler.currentTime;
            }
            else
            {
                this.reversePathCache.Add(key, new ReversePath(r, prev, scheduler.currentTime));
            }
        }

        public override void Recv(AdHocBaseApp.Packet pkg)
        {
            if (pkg.PrevType == NodeType.OBJECT || pkg.PrevType == NodeType.READER)
            {
                Node node = Node.getNode(pkg.Prev, pkg.PrevType);
                if (Utility.Distance(this, (MobileNode)node) > global.nodeMaxDist)
                {
                    Console.WriteLine("{0:F4} [{1}] {2}{3} Drop data of {4}{5} due to out of space.", scheduler.currentTime, pkg.Type, this.type, this.Id, node.type, node.Id);
                    CheckPacketCount(pkg);
                    return;
                }
            }
            if (pkg.Next != Id || pkg.NextType != NodeType.READER)
                return;

            //Self, ignore
            if (pkg.Prev == Id && pkg.PrevType == type)
                return;
            
            switch (pkg.Type)
            {
                    //Some codes are hided in the base class.
                case PacketType.LOGICAL_PATH_UPDATE:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if(pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    if(global.CheckReverseRouteCache)
                        CheckReversePathCache(pkg);
                    RecvLogicalPathUpdate(pkg);
                    break;
                case PacketType.LOGICAL_PATH_REQUEST:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvLogicalPathQueryRequest(pkg);
                    break;
                case PacketType.LOGICAL_PATH_REPLY:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    if (pkg.PrevType == NodeType.READER)
                        CheckPacketCount(pkg);
                    RecvLogicalPathQueryReply(pkg);
                    break;
                default:
                    base.Recv(pkg);
                    return;
            }
            pkg.TTL -= 1;
            if (pkg.TTL < 0)
                Drop(pkg);
        }

        public void RecvLogicalPathUpdate(Packet pkg)
        {
            if (pkg.PrevType == NodeType.OBJECT)//this node is a direct reader
            {
                pkg.ObjectLogicalPathUpdate.r = Id;
            }
            if (this.IsGateway == true)
            {
                pkg.ObjectLogicalPathUpdate.g = Id;
                pkg.Next = pkg.Dst;
                pkg.NextType = pkg.DstType;
                SendPacketDirectly(scheduler.currentTime, pkg);
                return;
            }
            else
                RoutePacket(pkg);
        }

        public void RecvLogicalPathQueryRequest(Packet pkg)
        {
            ObjectLogicalPathQueryRequestField f = pkg.ObjectLogicalPathQueryReqeust;
            /*pkg.Src = pkg.Prev = id;
            pkg.SrcType = pkg.PrevType = */
            string key = f.reader + "_" + f.obj;
            if (this.Id == f.reader)
            {
                pkg.Next = pkg.Dst = f.obj;
                pkg.NextType = pkg.DstType = NodeType.OBJECT;
                SendPacketDirectly(scheduler.currentTime, pkg);
                return;
            } 
            else if (global.CheckReverseRouteCache && this.reversePathCache.ContainsKey(key))
            {
                pkg.Next = this.reversePathCache[key].prev;
                SendPacketDirectly(scheduler.currentTime, pkg);
                return;
            }
            else
                RoutePacket(pkg);
        }


        public void RecvLogicalPathQueryReply(Packet pkg)
        {
            ObjectLogicalPathQueryReplyField f = pkg.ObjectLogicalPathQueryReply;
            
            if (this.Id == f.gateway)
            {
                pkg.Next = pkg.Dst = f.querier;
                pkg.NextType = pkg.DstType = NodeType.QUERIER;
                SendPacketDirectly(scheduler.currentTime, pkg);
                return;
            }
            RoutePacket(pkg);
        }
    }
}
