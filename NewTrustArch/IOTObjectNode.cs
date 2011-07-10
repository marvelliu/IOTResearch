using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.Diagnostics;

namespace NewTrustArch
{
    public class IOTObjectNode:ObjectNode
    {
        int dataSeq;

        Dictionary<int, float> nearReaderList;

        new public static IOTObjectNode ProduceObjectNode(int id)
        {
            return new IOTObjectNode(id);
        }


        protected IOTObjectNode(int id)
            : base(id)
        {
            this.dataSeq = 0;
            this.nearReaderList = new Dictionary<int, float>();
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

            if (this.nearReaderList.ContainsKey(pkg.Src))
                this.nearReaderList[pkg.Src] = scheduler.currentTime;
            else
                this.nearReaderList.Add(pkg.Src, scheduler.currentTime);

            Console.WriteLine("New near reader{0} for tag{1}", pkg.Src, Id);

            List<Packet> removeList = new List<Packet>();
            List<Packet> addList = new List<Packet>();
            foreach (Packet p in this.cachePackets)
            {
                if (this.cachePackets.Contains(pkg) && TrySendData(p))
                {
                    this.lastNearReader = this.global.readers[pkg.Src];
                    this.lastNearReaderTime = scheduler.currentTime;
                    removeList.Add(p);
                }
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

        int ChooseReader()
        {
            List<int> temp = new List<int>();
            List<int> readers = new List<int>();
            foreach (KeyValuePair<int, float> k in this.nearReaderList)
            {
                int r = k.Key;
                float time = k.Value;
                if (scheduler.currentTime - time > 3)
                    temp.Add(r);
                double x = global.readers[r].X - this.X;
                double y = global.readers[r].Y - this.Y;
                if (x < global.objectMaxDist && x > 0 - global.objectMaxDist
                    && y < global.objectMaxDist && x > 0 - global.objectMaxDist
                    && Utility.Distance((MobileNode)(global.readers[r]), (MobileNode)this) < global.objectMaxDist
                    )
                    readers.Add(r);
            }
            foreach (int r in temp)
                this.nearReaderList.Remove(r);
            int n = (int)Utility.U_Rand(readers.Count);
            if (n == 0)
                return -1;
            else
                return readers[n];
        }

        override public bool TrySendData(Packet pkg)
        {
            int lastReaderId = (this.lastNearReader == null) ? -1 : this.lastNearReader.Id;
            //这里，数据包的散列值直接设为0，表示符合，非0值表示不符合期望。
            pkg.DataInfo = new DataInfoField(lastReaderId, 0, this.dataSeq++);
            //if (this.lastNearReader != null && scheduler.CurrentTime - this.lastNearReaderTime < 1)
            //{

            //Console.WriteLine("choose readers num{0}-{1}", this.nearReaderList.Count, GetAllNearReaders(global.objectMaxDist).Count);
            int nextHop = ChooseReader();
            if (nextHop < 0)
                return false;
            pkg.Next = nextHop;
            pkg.NextType = NodeType.READER;
            SendPacketDirectly(scheduler.currentTime, pkg);
            return true;
            //}
            //return false;
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
