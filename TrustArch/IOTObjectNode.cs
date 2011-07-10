using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.Diagnostics;

namespace TrustArch
{
    public class IOTObjectNode:ObjectNode
    {
        new public static IOTObjectNode ProduceObjectNode(int id)
        {
            return new IOTObjectNode(id);
        }


        protected IOTObjectNode(int id)
            : base(id)
        {
        }
        

        public override void Recv(Packet pkg)
        {
            if (pkg.PrevType != NodeType.READER)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} drop from {4}{5} due to wrong node type", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
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
                case PacketType.TAG_HEADER:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    RecvTagHeaderRequest(pkg);
                    break;
                case PacketType.DATA_AVAIL: 
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    RecvAuthorization(pkg);
                    break;
                default:
                    base.Recv(pkg);
                    return;
            }
            return;
        }

        public void RecvAuthorization(Packet pkg)
        {
            int key = pkg.Authorization.keys[0];
            if (key != Id)
            {
                Console.WriteLine("Object{0} key not match: {1}.", Id, key);
                return;
            }
            this.lastNearReader = this.global.readers[pkg.Src];
            this.lastNearReaderTime = scheduler.currentTime;

            List<Packet> removeList = new List<Packet>();
            List<Packet> addList = new List<Packet>();
            foreach (Packet p in this.cachePackets)
            {
                if (TrySendData(p) && this.cachePackets.Contains(pkg))
                    removeList.Add(p);
                else if (!this.cachePackets.Contains(pkg))
                    addList.Add(pkg);
            }
            //我们就不测重试失败的例子了
            /* 
            foreach (Packet p in addList)
                this.cachePackets.Add(p);
             **/
            foreach (Packet p in removeList)
                this.cachePackets.Remove(p);
        }

        public void RecvTagHeaderRequest(Packet pkg)
        {
            Packet pkg1 = new Packet(this, global.orgs[this.OrgId], PacketType.TAG_HEADER);
            pkg1.ObjectTagHeader = new ObjectTagHeaderField(this.Id, this.OrgId);
            pkg1.Next = global.readers[pkg.Src].Id;
            pkg1.NextType = NodeType.READER;
            SendPacketDirectly(scheduler.currentTime, pkg1);            
        }

        public void GeneratePacket()
        {
            Packet pkg = new Packet(this, this, PacketType.DATA);
            this.cachePackets.Add(pkg);
        }



    }
}
